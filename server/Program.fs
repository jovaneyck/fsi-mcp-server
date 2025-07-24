module Program

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

type Program() =
    static member ConfigureServices(builder: WebApplicationBuilder) =
        // Configure logging - redirect ASP.NET logs away from console to keep FSI I/O clean
        builder.Logging.ClearProviders() |> ignore
        builder.Logging.AddDebug() |> ignore
        builder.Logging.SetMinimumLevel(LogLevel.Warning) |> ignore
        
        builder.Services.AddSingleton<FsiService.FsiService>()
        |> ignore

        builder.Services.AddSingleton<FsiMcpTools.FsiTools>(fun serviceProvider ->
            let fsiService = serviceProvider.GetRequiredService<FsiService.FsiService>()
            new FsiMcpTools.FsiTools(fsiService)
        ) |> ignore
        
        builder
            .Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<FsiMcpTools.FsiTools>()
        |> ignore

        builder.WebHost.UseUrls("http://0.0.0.0:5020")
        |> ignore

    static member ConfigureApp(app: WebApplication) =
        // Configure middleware pipeline
        app.UseDeveloperExceptionPage() |> ignore

        // Map MCP endpoints first (they use /mcp path prefix)
        app.MapMcp() |> ignore
            
        app.MapGet("/health", Func<string>(fun () -> "Ready to work!"))
        |> ignore

    static member CreateWebApplication(args: string[]) =
        let builder = WebApplication.CreateBuilder(args)
        Program.ConfigureServices(builder)
        let app = builder.Build()
        Program.ConfigureApp(app)
        app

let createApp (args: string[]) =
    let app = Program.CreateWebApplication(args)
    
    // Start FSI service
    let fsiService = app.Services.GetRequiredService<FsiService.FsiService>()
    let fsiProcess = fsiService.StartFsi(args |> Array.filter _.StartsWith("fsi:") |> Array.map _.Replace("fsi:",""))
    
    // Setup cleanup on shutdown
    let lifetime = app.Lifetime
        
    lifetime.ApplicationStopping.Register(fun () -> 
        fsiService.Cleanup()
    ) |> ignore
    
    Console.CancelKeyPress.Add (fun _ ->
        fsiService.Cleanup()
        Environment.Exit(0))
    
    let status =
        [ "🚀 FSI.exe with MCP Server"
          ""
          "🛠️  MCP Tools Available:"
          "   - SendFSharpCode: Execute F# code"
          "   - LoadFSharpScript: Load .fsx files"
          "   - GetFsiEventStream: Access FSI resource"
          "   - GetFsiStatus: Get session info"
          ""
          "💡 Usage Modes:"
          "   💬 Console: Type F# commands (streams via both MCP + SSE)"
          "   🤖 MCP: Use tools (streams via both MCP + SSE)"
    ]
    status |> Seq.iter (printfn "%s")
    printfn "Press Ctrl+C to stop"
    printfn ""
    
    // Start console input forwarding in background
    let inputChannel = Channel.CreateUnbounded<string>()

    let startConsoleProducer (logger: ILogger) (cts: CancellationToken) =
        Task.Run(fun () ->
            logger.LogInformation("Console producer started")
            while not cts.IsCancellationRequested do
                let line = Console.ReadLine()
                if not (isNull line) then
                    inputChannel.Writer.TryWrite line |> ignore
        , cts)
    
    let startFsiConsumer (fsiSvc: FsiService.FsiService) (logger: ILogger) (cts: CancellationToken) =
        Task.Run(fun () ->
            logger.LogInformation("FSI consumer started")
            task {
                while! inputChannel.Reader.WaitToReadAsync(cts) do
                    let! line = inputChannel.Reader.ReadAsync(cts)
                    match fsiSvc.SendToFsi(line, FsiService.FsiInputSource.Console) with
                    | Ok _       -> ()
                    | Error msg  -> logger.LogError("Console input error: {Msg}", msg)
            } :> Task
        , cts)
    
    let logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ConsoleBridge")
    
    use cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping)
    let prodTask = startConsoleProducer logger cts.Token
    let consTask = startFsiConsumer fsiService logger cts.Token
    
    app, Task.WhenAll [| prodTask; consTask |]

[<EntryPoint>]
let main args =
    let (app, consoleTask) = createApp args
    let appTask = app.RunAsync()
    Task.WaitAll([| appTask; consoleTask |])

    0
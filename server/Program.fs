module Program

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Serilog

type Program() =
    static member ConfigureServices(builder: WebApplicationBuilder, sessionId: string) =
        // Use Serilog for logging - global logger already configured in main()
        builder.Host.UseSerilog() |> ignore
        
        builder.Services.AddSingleton<FsiService.FsiService>(fun serviceProvider ->
            let logger = serviceProvider.GetRequiredService<ILogger<FsiService.FsiService>>()
            new FsiService.FsiService(logger, sessionId)
        )
        |> ignore

        builder.Services.AddSingleton<FsiMcpTools.FsiTools>(fun serviceProvider ->
            let fsiService = serviceProvider.GetRequiredService<FsiService.FsiService>()
            let logger = serviceProvider.GetRequiredService<ILogger<FsiMcpTools.FsiTools>>()
            new FsiMcpTools.FsiTools(fsiService, logger)
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

    static member CreateWebApplication(args: string[], sessionId: string) =
        let builder = WebApplication.CreateBuilder(args)
        Program.ConfigureServices(builder, sessionId)
        let app = builder.Build()
        Program.ConfigureApp(app)
        app

let createApp (args: string[]) (sessionId: string) =
    let (regArgs, fsiArgs) = args |> Array.partition (fun arg -> arg.StartsWith("fsi-mcp:") || arg.StartsWith("--contentRoot") || arg.StartsWith("--environment") || arg.StartsWith("--applicationName"))
    
    let app = Program.CreateWebApplication(regArgs |> Array.map _.Replace("fsi-mcp:",""), sessionId)
    
    // Start FSI service
    let fsiService = app.Services.GetRequiredService<FsiService.FsiService>()
    let fsiProcess = fsiService.StartFsi(fsiArgs)
    
    // Setup cleanup on shutdown
    let lifetime = app.Lifetime
        
    lifetime.ApplicationStopping.Register(fun () -> 
        fsiService.Cleanup()
    ) |> ignore
    
    Console.CancelKeyPress.Add (fun _ ->
        fsiService.Cleanup()
        Environment.Exit(0))
    
    let status =
        [ $"🚀 FSI.exe with MCP Server (Session: {sessionId})"
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

    let startConsoleProducer (logger: Microsoft.Extensions.Logging.ILogger) (cts: CancellationToken) =
        Task.Run(fun () ->
            logger.LogInformation("Console producer started")
            while not cts.IsCancellationRequested do
                let line = Console.ReadLine()
                if not (isNull line) then
                    inputChannel.Writer.TryWrite line |> ignore
        , cts)
    
    let startFsiConsumer (fsiSvc: FsiService.FsiService) (logger: Microsoft.Extensions.Logging.ILogger) (cts: CancellationToken) =
        Task.Run(fun () ->
            logger.LogInformation("FSI consumer started")
            logger.LogDebug("CONSOLE-CONSUMER: FSI consumer task started")
            task {
                while! inputChannel.Reader.WaitToReadAsync(cts) do
                    logger.LogDebug("CONSOLE-CONSUMER: Reading from input channel...")
                    let! line = inputChannel.Reader.ReadAsync(cts)
                    logger.LogDebug("CONSOLE-CONSUMER: Got line from channel: {Line}", line)
                    match fsiSvc.SendToFsi(line, FsiService.FsiInputSource.Console) with
                    | Ok _       -> ()
                    | Error msg  -> logger.LogError("Console input error: {Msg}", msg)
            } :> Task
        , cts)
    
    let logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ConsoleBridge")

    // Diagnostic: Log console state at startup
    logger.LogDebug("=== CONSOLE STATE DIAGNOSTICS ===")
    logger.LogDebug("Console.IsInputRedirected: {IsInputRedirected}", Console.IsInputRedirected)
    logger.LogDebug("Console.IsOutputRedirected: {IsOutputRedirected}", Console.IsOutputRedirected)
    logger.LogDebug("Console.IsErrorRedirected: {IsErrorRedirected}", Console.IsErrorRedirected)
    logger.LogDebug("=================================")

    use cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping)

    // Start tasks (they will wait internally for FSI ready)
    let prodTask = startConsoleProducer logger cts.Token
    let consTask = startFsiConsumer fsiService logger cts.Token
    
    app, Task.WhenAll [| prodTask; consTask |]

[<EntryPoint>]
let main args =
    // Generate session ID early for Serilog configuration
    let sessionId = System.Guid.NewGuid().ToString("N")[..7]
    
    // Configure Serilog early - before any other logging
    Log.Logger <- LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File($"c:/tmp/fsi-mcp-debugging-{sessionId}.log")
        .CreateLogger()
    
    Log.Information("FSI MCP Server starting with session ID: {SessionId}", sessionId)
    
    try
        let (app, consoleTask) = createApp args sessionId
        let appTask = app.RunAsync()
        Task.WaitAll([| appTask; consoleTask |])
        0
    finally
        Log.CloseAndFlush()
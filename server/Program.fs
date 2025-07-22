open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    // Configure logging - redirect ASP.NET logs away from console to keep FSI I/O clean
    builder.Logging.ClearProviders() |> ignore
    builder.Logging.AddDebug() |> ignore
    builder.Logging.SetMinimumLevel(LogLevel.Warning) |> ignore
    
    // Add services
    builder.Services.AddSingleton<FsiService.FsiService>()
    |> ignore


    // Add MCP server services with HTTP transport for streaming
    builder
        .Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<FsiMcpTools.FsiTools>()
    |> ignore

    let app = builder.Build()

    // Start FSI service
    let fsiService = app.Services.GetRequiredService<FsiService.FsiService>()
    let fsiProcess = fsiService.StartFsi(args)

    // Configure middleware pipeline
    app.UseDeveloperExceptionPage() |> ignore

    // Map MCP endpoints first (they use /mcp path prefix)
    app.MapMcp() |> ignore

    let status =
        [ "🚀 FSI Server with MCP Integration"
          ""
          "🛠️  MCP Tools Available:"
          "   - SendFSharpCode: Execute F# code in the active FSI process"
          "   - LoadFSharpScript: Load entire .fsx files into the active FSI process"
          "   - GetFsiStatus: Get server information"
          ""
          "💡 Usage Modes:"
          "   💬 Console Mode: Type F# commands directly into console, this app acts as a wrapper around fsi.exe"
          "   🤖 MCP Mode: Use MCP client tools" ]

    app.MapGet("/health", Func<string>(fun () -> "Up and running"))
    |> ignore
    // Add a simple status page
    app.MapGet("/", Func<string>(fun () -> String.concat "\n" status))
    |> ignore

    // Setup cleanup on shutdown
    let lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>()

    lifetime.ApplicationStopping.Register(fun () -> fsiService.Cleanup())
    |> ignore

    Console.CancelKeyPress.Add (fun _ ->
        fsiService.Cleanup()
        Environment.Exit(0))

    // Print startup information
    status |> Seq.iter (printfn "%s")
    printfn "Press Ctrl+C to stop"
    printfn ""

    // Start console input forwarding in background
    let consoleTask = Task.Run(fun () ->
        try
            printfn "💬 Console input forwarding started. Type F# commands directly:"
            while true do
                let line = Console.ReadLine()
                if not (isNull line) then
                    match fsiService.SendToFsi(line, FsiService.FsiInputSource.Console) with
                    | Ok _ -> ()
                    | Error msg -> printfn $"Console input error: {msg}"
        with
        | ex -> printfn $"Console input service error: {ex.Message}")

    // Run the application
    app.Run()

    0
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
    
    let app = builder.Build()
    
    // Start FSI service
    let fsiService = app.Services.GetRequiredService<FsiService.FsiService>()
    let fsiProcess = fsiService.StartFsi(args)
    
    // Configure middleware pipeline
    app.UseDeveloperExceptionPage() |> ignore

    // Map MCP endpoints first (they use /mcp path prefix)
    app.MapMcp() |> ignore

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
        
    app.MapGet("/health", Func<string>(fun () -> "Ready to work!"))
    |> ignore
    
    // Add a simple status page
    app.MapGet("/status", Func<string>(fun () -> String.concat "\n" status))
    |> ignore

    // Setup cleanup on shutdown
    let lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>()

    lifetime.ApplicationStopping.Register(fun () -> 
        fsiService.Cleanup()
    ) |> ignore

    Console.CancelKeyPress.Add (fun _ ->
        fsiService.Cleanup()
        Environment.Exit(0))

    status |> Seq.iter (printfn "%s")
    printfn "Press Ctrl+C to stop"
    printfn ""

    // Start console input forwarding in background
    let _ = Task.Run(fun () ->
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
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open ModelContextProtocol.Server

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
    
    // Add the FSI event buffer service (simplified approach)
    builder.Services.AddSingleton<FsiEventNotificationService.FsiEventNotificationService>()
    |> ignore
    
    builder.Services.AddHostedService(fun serviceProvider ->
        serviceProvider.GetRequiredService<FsiEventNotificationService.FsiEventNotificationService>()
    ) |> ignore
    
    builder.Services.AddHostedService(fun serviceProvider ->
        serviceProvider.GetRequiredService<FsiMcpResourceService.FsiMcpResourceService>()
    ) |> ignore
    
    // Add FSI Server-Sent Events service for real-time streaming
    builder.Services.AddSingleton<FsiSseService.FsiSseService>()
    |> ignore
    
    // Add FSI MCP Resource service for proper MCP resource subscriptions
    builder.Services.AddSingleton<FsiMcpResourceService.FsiMcpResourceService>()
    |> ignore


    // Register FsiTools with dependencies  
    builder.Services.AddSingleton<FsiMcpTools.FsiTools>(fun serviceProvider ->
        let fsiService = serviceProvider.GetRequiredService<FsiService.FsiService>()
        let resourceService = serviceProvider.GetRequiredService<FsiMcpResourceService.FsiMcpResourceService>()
        new FsiMcpTools.FsiTools(fsiService, resourceService)
    ) |> ignore
    
    // Add MCP server services with HTTP transport and resource subscriptions
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
    
    // Start SSE streaming service (temporarily disabled)
    // let sseService = app.Services.GetRequiredService<FsiSseService.FsiSseService>()
    // sseService.StartStreaming()
    
    // Start MCP Resource service 
    let resourceService = app.Services.GetRequiredService<FsiMcpResourceService.FsiMcpResourceService>()
    // TODO: Connect to MCP server for notifications once the API is figured out
    // Resource service will start automatically as a hosted service
    
    // Start SSE streaming service
    let sseService = app.Services.GetRequiredService<FsiSseService.FsiSseService>()
    sseService.StartStreaming()

    // Configure middleware pipeline
    app.UseDeveloperExceptionPage() |> ignore

    // Map MCP endpoints first (they use /mcp path prefix)
    app.MapMcp() |> ignore

    let status =
        [ "🚀 FSI Server with MCP Resource Subscription + Server-Sent Events"
          ""
          "🔗 MCP RESOURCE SUBSCRIPTION (RECOMMENDED):"
          "   - Resource URI: fsi://events/stream"
          "   - Subscribe via MCP Inspector or MCP client"
          "   - Receives notifications/resources/updated on FSI activity"
          "   - Standard MCP protocol - works with all MCP clients"
          ""
          "🌊 SERVER-SENT EVENTS (DIRECT ACCESS):"
          "   - GET /fsi/stream - Real-time SSE endpoint"
          "   - Events: 'connected', 'fsi_event'"
          "   - Direct browser/curl access for testing"
          "   - JSON payloads with full FSI event data"
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
          "   🔍 Inspector: Subscribe to fsi://events/stream resource"
          "   🌊 Browser: Connect to /fsi/stream for live events" ]

    app.MapGet("/health", Func<string>(fun () -> "Up and running"))
    |> ignore
    
    // Add FSI Server-Sent Events endpoint for real-time streaming
    app.MapGet("/fsi/stream", Func<HttpContext, Task>(fun context ->
        let sseService = app.Services.GetRequiredService<FsiSseService.FsiSseService>()
        sseService.HandleSseConnection(context)
    )) |> ignore
    
    // Add FSI Server-Sent Events endpoint for real-time streaming (temporarily disabled)
    // app.MapGet("/fsi/stream", Func<HttpContext, Task>(fun context ->
    //     let sseService = app.Services.GetRequiredService<FsiSseService.FsiSseService>()
    //     sseService.HandleSseConnection(context)
    // )) |> ignore
    
    // Add FSI events API endpoint for MCP clients (fallback)
    app.MapGet("/fsi/events", Func<string>(fun () ->
        let eventService = app.Services.GetRequiredService<FsiEventNotificationService.FsiEventNotificationService>()
        let events = eventService.GetEventStream()
        if events.Length = 0 then
            "{\"events\": [], \"message\": \"No FSI events yet\"}"
        else
            let result = {| events = events; total = events.Length |}
            System.Text.Json.JsonSerializer.Serialize(result)
    )) |> ignore
    
    // Add a simple status page
    app.MapGet("/", Func<string>(fun () -> String.concat "\n" status))
    |> ignore

    // Setup cleanup on shutdown
    let lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>()

    lifetime.ApplicationStopping.Register(fun () -> 
        fsiService.Cleanup()
        sseService.StopStreaming()
    ) |> ignore

    Console.CancelKeyPress.Add (fun _ ->
        fsiService.Cleanup()
        sseService.StopStreaming()
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
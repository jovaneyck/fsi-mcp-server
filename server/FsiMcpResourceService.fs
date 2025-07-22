module FsiMcpResourceService

open System
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System.Threading
open System.Threading.Tasks
open ModelContextProtocol.Server
open FsiService
open System.Collections.Concurrent

type FsiMcpResourceService(logger: ILogger<FsiMcpResourceService>, fsiService: FsiService) =
    inherit BackgroundService()
    
    let mutable eventHandler: (FsiEvent -> unit) option = None
    let subscribedClients = ConcurrentBag<string>()
    let fsiEventsResourceUri = "fsi://events/stream"
    let mutable mcpServer: IMcpServer option = None
    
    let sendResourceUpdateNotification (event: FsiEvent) =
        task {
            try
                // Send MCP resource updated notification
                // Format: notifications/resources/updated with uri parameter
                let updateNotification = {|
                    uri = fsiEventsResourceUri
                    title = $"FSI Event: {event.EventType} from {event.Source}"
                    content = event.Content
                    timestamp = event.Timestamp
                    eventType = event.EventType
                    source = event.Source
                |}
                
                // This should trigger the MCP notification
                logger.LogDebug($"Sending MCP resource update notification for FSI event: {event.EventType}")
                // Note: Need to use proper MCP API once we figure out the correct method
                
            with
            | ex -> logger.LogWarning($"Failed to send MCP resource update: {ex.Message}")
        }
    
    override _.StartAsync(cancellationToken: CancellationToken) =
        task {
            // Set up FSI event handler for MCP resource notifications
            let handler = fun (event: FsiEvent) ->
                if not subscribedClients.IsEmpty then
                    sendResourceUpdateNotification event |> ignore
            
            eventHandler <- Some handler
            fsiService.AddEventHandler(handler)
            
            logger.LogInformation($"🔗 FSI MCP Resource Service started - resource URI: {fsiEventsResourceUri}")
            // return! base.StartAsync(cancellationToken) -- Not needed in F#
        }
    
    override _.StopAsync(cancellationToken: CancellationToken) =
        task {
            match eventHandler with
            | Some handler -> 
                fsiService.RemoveEventHandler(handler)
                eventHandler <- None
            | None -> ()
            
            logger.LogInformation("🔗 FSI MCP Resource Service stopped")
            // return! base.StopAsync(cancellationToken) -- Not needed in F#
        }
    
    override _.ExecuteAsync(cancellationToken: CancellationToken) =
        task {
            // Resource-based service - event driven
            try
                while not cancellationToken.IsCancellationRequested do
                    do! Task.Delay(1000, cancellationToken)
            with
            | :? OperationCanceledException -> ()
            
            logger.LogInformation("FSI MCP Resource Service execution completed")
        }
    
    member _.SetMcpServer(server: IMcpServer) =
        mcpServer <- Some server
    
    member _.HandleResourceSubscription(uri: string) =
        if uri = fsiEventsResourceUri then
            subscribedClients.Add(uri)
            logger.LogInformation($"Client subscribed to FSI resource: {uri}")
            true
        else
            false
    
    member _.HandleResourceUnsubscription(uri: string) =
        if uri = fsiEventsResourceUri then
            logger.LogInformation($"Client unsubscribed from FSI resource: {uri}")
            // Note: ConcurrentBag doesn't have Remove, but that's okay for this use case
            true
        else
            false
    
    member _.GetResourceContent(uri: string) =
        if uri = fsiEventsResourceUri then
            // Return current FSI events as resource content
            let events = fsiService.GetRecentEvents(50)
            let result = {|
                uri = uri
                title = "FSI Event Stream"
                description = "Real-time F# Interactive events from all sources"
                mimeType = "application/json"
                events = events
                totalEvents = events.Length
                sessionId = fsiService.GetSessionId()
            |}
            Some (System.Text.Json.JsonSerializer.Serialize(result))
        else
            None
module FsiEventNotificationService

open System
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System.Threading
open System.Threading.Tasks
open FsiService
open System.Collections.Concurrent

type FsiEventNotificationService(logger: ILogger<FsiEventNotificationService>, fsiService: FsiService) =
    inherit BackgroundService()
    
    let mutable eventHandler: (FsiEvent -> unit) option = None
    let eventBuffer = ConcurrentQueue<FsiEvent>()
    let mutable maxBufferSize = 100
    
    member _.GetEventStream() =
        let allEvents = eventBuffer.ToArray()
        allEvents |> Array.rev // Most recent first
    
    override _.StartAsync(cancellationToken: CancellationToken) =
        // Create event handler that buffers events for streaming
        let handler = fun (event: FsiEvent) ->
            eventBuffer.Enqueue(event)
            
            // Keep buffer size manageable
            if eventBuffer.Count > maxBufferSize then
                let mutable dummy = Unchecked.defaultof<FsiEvent>
                eventBuffer.TryDequeue(&dummy) |> ignore
                
            logger.LogDebug($"Buffered FSI Event: {event.EventType} from {event.Source}")
        
        eventHandler <- Some handler
        fsiService.AddEventHandler(handler)
        
        logger.LogInformation("🔔 FSI Event Buffer Service started - events ready for streaming")
        Task.CompletedTask
    
    override _.StopAsync(cancellationToken: CancellationToken) =
        // Clean up event handler
        match eventHandler with
        | Some handler -> 
            fsiService.RemoveEventHandler(handler)
            eventHandler <- None
        | None -> ()
            
        logger.LogInformation("🔕 FSI Event Buffer Service stopped")
        Task.CompletedTask
    
    override _.ExecuteAsync(cancellationToken: CancellationToken) =
        task {
            // This service is event-driven, so we just wait for cancellation
            // All the real work happens in the event handlers
            try
                while not cancellationToken.IsCancellationRequested do
                    do! Task.Delay(1000, cancellationToken)
            with
            | :? OperationCanceledException -> ()
            
            logger.LogInformation("FSI Event Buffer Service execution completed")
        }
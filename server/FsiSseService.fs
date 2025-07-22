module FsiSseService

open System
open System.IO
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open FsiService

type SseClient = {
    Id: string
    Response: HttpResponse
    Writer: StreamWriter
    CancellationToken: CancellationToken
}

type FsiSseService(logger: ILogger<FsiSseService>, fsiService: FsiService) =
    let clients = ConcurrentDictionary<string, SseClient>()
    let mutable eventHandler: (FsiEvent -> unit) option = None
    
    let sendSseMessage (client: SseClient) (eventType: string) (data: string) =
        task {
            try
                if not client.CancellationToken.IsCancellationRequested then
                    do! client.Writer.WriteLineAsync($"event: {eventType}")
                    do! client.Writer.WriteLineAsync($"data: {data}")
                    do! client.Writer.WriteLineAsync("")
                    do! client.Writer.FlushAsync()
                    logger.LogDebug($"Sent SSE event {eventType} to client {client.Id}")
            with
            | ex -> 
                logger.LogWarning($"Failed to send SSE to client {client.Id}: {ex.Message}")
                let mutable removedClient = Unchecked.defaultof<SseClient>
                clients.TryRemove(client.Id, &removedClient) |> ignore
        }
    
    let broadcastFsiEvent (event: FsiEvent) =
        let eventJson = System.Text.Json.JsonSerializer.Serialize(event)
        let clientList = clients.Values |> Seq.toList
        
        if not (List.isEmpty clientList) then
            // Send to each client asynchronously but don't await
            for client in clientList do
                sendSseMessage client "fsi_event" eventJson |> ignore
            
            logger.LogDebug($"Broadcasting FSI event to {clientList.Length} SSE clients")
    
    member _.StartStreaming() =
        // Set up FSI event handler for real-time streaming
        let handler = fun (event: FsiEvent) ->
            broadcastFsiEvent event
        
        eventHandler <- Some handler
        fsiService.AddEventHandler(handler)
        
        logger.LogInformation("🌊 FSI SSE Streaming Service started")
    
    member _.StopStreaming() =
        match eventHandler with
        | Some handler -> 
            fsiService.RemoveEventHandler(handler)
            eventHandler <- None
        | None -> ()
        
        // Close all client connections
        for client in clients.Values do
            try
                client.Writer.Close()
            with | _ -> ()
        
        clients.Clear()
        logger.LogInformation("🛑 FSI SSE Streaming Service stopped")
    
    member _.HandleSseConnection(context: HttpContext) =
        task {
            let clientId = Guid.NewGuid().ToString("N")[..7]
            let response = context.Response
            
            // Set SSE headers
            response.ContentType <- "text/event-stream"
            response.Headers.Add("Cache-Control", "no-cache")
            response.Headers.Add("Connection", "keep-alive")
            response.Headers.Add("Access-Control-Allow-Origin", "*")
            response.Headers.Add("Access-Control-Allow-Headers", "Cache-Control")
            
            let writer = new StreamWriter(response.Body, bufferSize = 1)
            let client = {
                Id = clientId
                Response = response
                Writer = writer
                CancellationToken = context.RequestAborted
            }
            
            // Add client to active connections
            clients.TryAdd(clientId, client) |> ignore
            
            try
                try
                    // Send initial connection message
                    do! sendSseMessage client "connected" $"{{\"clientId\": \"{clientId}\", \"message\": \"Connected to FSI event stream\"}}"
                    
                    logger.LogInformation($"SSE client {clientId} connected")
                    
                    // Keep connection alive until client disconnects
                    while not context.RequestAborted.IsCancellationRequested do
                        do! Task.Delay(1000, context.RequestAborted)
                        
                with
                | :? OperationCanceledException -> ()
                | ex -> logger.LogWarning($"SSE connection error for client {clientId}: {ex.Message}")
            finally
                // Clean up when client disconnects
                let mutable removedClient = Unchecked.defaultof<SseClient>
                clients.TryRemove(clientId, &removedClient) |> ignore
                try
                    writer.Close()
                with | _ -> ()
                
                logger.LogInformation($"SSE client {clientId} disconnected")
        }
module FsiService

open System
open System.IO
open System.Diagnostics
open Microsoft.Extensions.Logging
open System.Collections.Concurrent

type FsiInputSource = 
    | Console 
    | Api of string
    | FileSync of string

type FsiEvent = {
    EventType: string
    Source: string 
    Content: string
    Timestamp: string
    SessionId: string
}

type FsiService(logger: ILogger<FsiService>, sessionId: string) =
    let mutable fsiProcess: Process option = None
    let eventQueue = ConcurrentQueue<FsiEvent>()

    let getSourceName = function
        | Console -> "console"
        | Api source -> $"api:{source}"
        | FileSync path -> $"file:{path}"
    
    let createFsiEvent eventType source content =
        {
            EventType = eventType
            Source = source
            Content = content 
            Timestamp = DateTime.UtcNow.ToString("O")
            SessionId = sessionId
        }
    
    let addEvent (event: FsiEvent) =
        eventQueue.Enqueue(event)

    member _.StartFsi(args: string[]) =
        logger.LogDebug("FSI-START: StartFsi called with {ArgsCount} args", args.Length)
        let fsiArgs =
            if args.Length > 1 then
                let userArgs = args |> Array.skip 1 |> String.concat " "
                $"fsi {userArgs}"
            else
                "fsi"

        logger.LogDebug("FSI-START: Starting FSI with args: {FsiArgs}", fsiArgs)
        let psi = ProcessStartInfo()
        psi.FileName <- "dotnet"
        psi.Arguments <- fsiArgs
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true

        let proc = Process.Start(psi)
        fsiProcess <- Some proc
        logger.LogDebug("FSI-START: FSI process started with PID {ProcessId}", proc.Id)
        
        // Stream all fsi output
        async {
            try
                while not proc.HasExited do
                    let! line = proc.StandardOutput.ReadLineAsync() |> Async.AwaitTask
                    if not (isNull line) then
                        Console.WriteLine(line)  // Pipe FSI output to console as normal                        
                        createFsiEvent "output" "fsi" line |> addEvent //Fill event queue so MCP clients can follow along
            with
            | ex -> logger.LogError($"Output monitoring error: %s{ex.Message}")
        } |> Async.Start
        
        // Log FSI errors and show on console
        async {
            try
                while not proc.HasExited do
                    let! line = proc.StandardError.ReadLineAsync() |> Async.AwaitTask
                    if not (isNull line) then
                        logger.LogDebug("FSI-STDERR: {Line}", line)
                        Console.WriteLine $"ERROR: %s{line}"
                        Console.Out.Flush()  // Flush immediately for redirected stdout (Rider)

                        // Add error event to queue
                        let event = createFsiEvent "error" "fsi" line
                        addEvent event
            with
            | ex ->
                logger.LogError(ex, "FSI-ERROR: Error monitoring error")
        } |> Async.Start
        
        logger.LogInformation("🚀 FSI started with input/output interception!")
        logger.LogInformation($"⚙️  FSI command: dotnet {fsiArgs}")
        
        proc

    member _.SendToFsi(code: string, source: FsiInputSource) =
        let sourceName = getSourceName source
        logger.LogDebug("FSI-INPUT: SendToFsi from {Source}: {Code}", sourceName, code.Trim())
        match fsiProcess with
        | Some proc when not proc.HasExited ->
            if source <> Console then //Don't pipe CLI input back to CLI.
                Console.WriteLine $"(%s{sourceName})> %s{code.Trim()}"
                Console.Out.Flush()  // Flush immediately for redirected stdout (Rider)

            // Add input event to queue
            let event = createFsiEvent "input" sourceName (code.Trim())
            addEvent event
            //Forward input to wrapped fsi process - send raw input
            logger.LogDebug("FSI-FORWARD: Writing to FSI stdin: {Code}", code.Trim())
            proc.StandardInput.WriteLine(code)
            proc.StandardInput.Flush()
            logger.LogDebug("FSI-FORWARD: Flushed stdin")

            Ok "Code sent to FSI and logged"
        | _ ->
            logger.LogDebug("FSI-ERROR: SendToFsi called but FSI not running")
            Error "FSI not running"

    member this.SyncFileToFsi(filePath: string) =
        if File.Exists(filePath) then
            let content = File.ReadAllText(filePath)
            
            let statements = 
                content.Split([|";;"|], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> s.Trim() + ";;")
                |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace(s)) && not (s.TrimStart().StartsWith("//")))
            
            let fileName = Path.GetFileName filePath
            for stmt in statements do
                match this.SendToFsi(stmt, FileSync fileName) with
                | Ok _ -> ()
                | Error msg -> logger.LogError($"Error syncing statement: {msg}")
            
            Ok $"Synced %d{statements.Length} statements from %s{fileName}"
        else
            Error $"File not found: %s{filePath}"

    member _.GetRecentEvents(count: int) =
        //TODO: performance, takeLast
        eventQueue.ToArray()
        |> Array.rev
        |> Array.take (min count eventQueue.Count)
        |> Array.rev
    
    member _.GetAllEvents() =
        eventQueue.ToArray()
    
    member _.GetSessionId() = sessionId

    member _.Cleanup() =
        match fsiProcess with
        | Some proc when not proc.HasExited ->
            proc.Kill()
            proc.WaitForExit(5000) |> ignore
        | _ -> ()

    interface IDisposable with
        member this.Dispose() = this.Cleanup()
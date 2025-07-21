//#r "nuget: Suave"

open System
open System.IO
open System.Diagnostics
open System.Text
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors

let workdir = "c:/tmp/"
let claudeDir = workdir + "fsi-claude/"
let pendingDir = claudeDir + "pending/"
let processingDir = claudeDir + "processing/"
let completedDir = claudeDir + "completed/"
let responsesDir = claudeDir + "responses/"

let outputFile = workdir+"fsi-session.log"

let ensureDirectories() =
    Directory.CreateDirectory(claudeDir) |> ignore
    Directory.CreateDirectory(pendingDir) |> ignore
    Directory.CreateDirectory(processingDir) |> ignore
    Directory.CreateDirectory(completedDir) |> ignore
    Directory.CreateDirectory(responsesDir) |> ignore

let startFsiWithInterception() =
    File.WriteAllText(outputFile, "")
    ensureDirectories()
    
    // Get CLI arguments passed to this program and forward them to FSI
    let args = Environment.GetCommandLineArgs()
    let fsiArgs = 
        if args.Length > 1 then
            // Skip the first argument (program name) and pass the rest to FSI
            let userArgs = args |> Array.skip 1 |> String.concat " "
            $"fsi {userArgs}"
        else
            "fsi"
    
    let psi = ProcessStartInfo()
    psi.FileName <- "dotnet"
    psi.Arguments <- fsiArgs
    psi.RedirectStandardInput <- true
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    
    let proc = Process.Start(psi)
    
    // Stream all output to log file
    let logWriter = new StreamWriter(outputFile, true)
    logWriter.AutoFlush <- true
    
    // Log FSI output and show on console
    async {
        try
            while not proc.HasExited do
                let! line = proc.StandardOutput.ReadLineAsync() |> Async.AwaitTask
                if not (isNull line) then
                    let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                    let logLine = $"[%s{timestamp}] %s{line}"
                    logWriter.WriteLine(logLine)
                    Console.WriteLine(line)  // Show FSI output on console
                    
                    // Also append to most recent Claude response file if it exists
                    try
                        let responseFiles = Directory.GetFiles(responsesDir, "*.log")
                        if responseFiles.Length > 0 then
                            let mostRecent = responseFiles |> Array.maxBy (fun f -> FileInfo(f).LastWriteTime)
                            File.AppendAllText(mostRecent, $"[%s{timestamp}] OUTPUT: %s{line}\n")
                    with
                    | _ -> () // Ignore errors writing to response files
        with
        | ex -> printfn $"Output monitoring error: %s{ex.Message}"
    } |> Async.Start
    
    // Log FSI errors and show on console
    async {
        try
            while not proc.HasExited do
                let! line = proc.StandardError.ReadLineAsync() |> Async.AwaitTask
                if not (isNull line) then
                    let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                    let logLine = $"[%s{timestamp}] ERROR: %s{line}"
                    logWriter.WriteLine(logLine)
                    Console.WriteLine $"ERROR: %s{line}"
                    
                    // Also append to most recent Claude response file if it exists
                    try
                        let responseFiles = Directory.GetFiles(responsesDir, "*.log")
                        if responseFiles.Length > 0 then
                            let mostRecent = responseFiles |> Array.maxBy (fun f -> FileInfo(f).LastWriteTime)
                            File.AppendAllText(mostRecent, $"[%s{timestamp}] ERROR: %s{line}\n")
                    with
                    | _ -> () // Ignore errors writing to response files
        with
        | ex -> printfn $"Error monitoring error: %s{ex.Message}"
    } |> Async.Start
    
    // Forward console input to FSI
    async {
        try
            while not proc.HasExited do
                let! line = Console.In.ReadLineAsync() |> Async.AwaitTask
                if not (isNull line) then
                    let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                    let inputLog = $"[%s{timestamp}] INPUT (console): %s{line}"
                    logWriter.WriteLine(inputLog)
                    logWriter.Flush()
                    
                    proc.StandardInput.WriteLine(line)
                    proc.StandardInput.Flush()
        with
        | ex -> printfn $"Console input forwarding error: %s{ex.Message}"
    } |> Async.Start
    
    printfn "🚀 FSI started with input/output interception!"
    printfn $"📁 Session log: %s{Path.GetFullPath outputFile}"
    printfn $"⚙️  FSI command: dotnet {fsiArgs}"
    printfn "📡 Send commands via HTTP API - all input/output logged"
    printfn "💬 Type F# commands directly in this console - they will be forwarded to FSI"
    printfn ""
    
    (proc, logWriter)

let (fsiProcess, writer) = startFsiWithInterception()

// File-based Claude command processing
let processClaudeCommand (filePath: string) =
    let fileName = Path.GetFileName(filePath)
    let processingPath = Path.Combine(processingDir, fileName)
    let completedPath = Path.Combine(completedDir, fileName)
    let responseFile = Path.Combine(responsesDir, Path.GetFileNameWithoutExtension(fileName) + ".log")
    
    try
        // Move to processing
        File.Move(filePath, processingPath)
        
        // Read command content
        let content = File.ReadAllText(processingPath)
        
        // Create response file
        let responseWriter = new StreamWriter(responseFile, false)
        responseWriter.AutoFlush <- true
        
        let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
        let inputLog = $"[%s{timestamp}] INPUT (claude-file): %s{content.Trim()}"
        
        // Log to main session log
        writer.WriteLine(inputLog)
        writer.Flush()
        
        // Log to response file
        responseWriter.WriteLine(inputLog)
        
        // Echo to console
        Console.WriteLine $"(claude-file)> %s{content.Trim()}"
        
        // Send to FSI
        if not fsiProcess.HasExited then
            fsiProcess.StandardInput.WriteLine(content)
            fsiProcess.StandardInput.Flush()
        
        responseWriter.WriteLine($"[%s{timestamp}] STATUS: sent-to-fsi")
        responseWriter.Close()
        
        // Move to completed
        File.Move(processingPath, completedPath)
        
    with
    | ex -> 
        printfn $"Error processing Claude command file %s{fileName}: %s{ex.Message}"
        // Try to move back to pending on error
        try
            if File.Exists(processingPath) then
                File.Move(processingPath, filePath)
        with
        | _ -> ()

// File watcher for Claude commands
let startClaudeFileWatcher() =
    let watcher = new FileSystemWatcher(pendingDir, "*.fsx")
    watcher.EnableRaisingEvents <- true
    watcher.Created.Add(fun e -> 
        // Small delay to ensure file is fully written
        async {
            do! Async.Sleep(100)
            processClaudeCommand e.FullPath
        } |> Async.Start
    )
    watcher

let claudeWatcher = startClaudeFileWatcher()

// HTTP API for sending commands
let app = 
    choose [
        POST >=> path "/send" >=> request (fun r -> 
            try
                let code = Encoding.UTF8.GetString(r.rawForm)
                let source = 
                    match r.queryParam "source" with
                    | Choice1Of2 s -> s
                    | Choice2Of2 _ -> "api"
                
                match fsiProcess with
                | proc when not proc.HasExited ->
                    // Log the input we're sending
                    let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                    let inputLog = $"[%s{timestamp}] INPUT (%s{source}): %s{code.Trim()}"
                    
                    // Write to log file
                    writer.WriteLine(inputLog)
                    writer.Flush()

                    // Echo API input to console so user can see what was sent
                    Console.WriteLine $"(%s{source})> %s{code.Trim()}"

                    // Send to FSI
                    proc.StandardInput.WriteLine(code)
                    proc.StandardInput.Flush()
                    
                    OK "Code sent to FSI and logged"
                | _ -> 
                    BAD_REQUEST "FSI not running"
            with
            | ex -> BAD_REQUEST $"Error: %s{ex.Message}"
        )
        
        // Sync entire file to FSI
        POST >=> path "/sync-file" >=> request (fun r ->
            try
                match r.queryParam "file" with
                | Choice1Of2 filePath ->
                    if File.Exists(filePath) then
                        let content = File.ReadAllText(filePath)
                        
                        // Parse F# statements (simple approach)
                        let statements = 
                            content.Split([|";;"|], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.map (fun s -> s.Trim() + ";;")
                            |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace(s)) && not (s.TrimStart().StartsWith("//")))
                        
                        // Send each statement
                        for stmt in statements do
                            let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                            let inputLog = $"[%s{timestamp}] INPUT (file-sync:%s{Path.GetFileName filePath}): %s{stmt}"
                            
                            
                            writer.WriteLine(inputLog)
                            writer.Flush()
    
                            if not fsiProcess.HasExited then
                                fsiProcess.StandardInput.WriteLine(stmt)
                                fsiProcess.StandardInput.Flush()
                        
                        OK $"Synced %d{statements.Length} statements from %s{Path.GetFileName filePath}"
                    else
                        BAD_REQUEST $"File not found: %s{filePath}"
                | Choice2Of2 _ -> 
                    BAD_REQUEST "Missing 'file' parameter"
            with
            | ex -> BAD_REQUEST $"Error: %s{ex.Message}"
        )
        
        NOT_FOUND "Resource not found"
    ]

// Cleanup on exit
let cleanup() =
    claudeWatcher.Dispose()
    writer.Close()
   
    if not fsiProcess.HasExited then
        fsiProcess.Kill()
        fsiProcess.WaitForExit(5000) |> ignore

Console.CancelKeyPress.Add(fun _ -> cleanup())

// Start HTTP server
let config = { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 8080 ] }

printfn "🌐 FSI HTTP API running on http://localhost:8080"
printfn ""
printfn "📝 Usage Modes:"
printfn "   💬 Console Mode: Type F# commands directly in this console"
printfn "   🌐 API Mode: Send commands via HTTP endpoints"
printfn "   📁 Claude File Mode: Drop .fsx files in pending directory"
printfn "   🔄 Hybrid Mode: Mix all modes simultaneously"
printfn ""
printfn "🔌 HTTP Endpoints:"
printfn "   POST /send?source=<name>    - Send code to FSI"
printfn "   POST /sync-file?file=<path> - Sync .fsx file to FSI"
printfn ""
printfn "📁 Claude File Protocol:"
printfn $"   Drop .fsx files in: %s{Path.GetFullPath pendingDir}"
printfn $"   Responses appear in: %s{Path.GetFullPath responsesDir}"
printfn $"   Completed files move to: %s{Path.GetFullPath completedDir}"
printfn ""
printfn "💡 Example usage:"
printfn "   CLI: fsi-server --nologo --load:script.fsx (passes args to FSI)"
printfn "   Console: Just type F# code below and press Enter"
printfn "   API: curl -X POST 'http://localhost:8080/send?source=claude' -d 'let x = 42;;'"
printfn "   Sync: curl -X POST 'http://localhost:8080/sync-file?file=script.fsx'"
printfn $"   Claude: Write .fsx file to %s{Path.GetFullPath pendingDir}"
printfn ""
printfn $"📁 Session log file: %s{Path.GetFullPath outputFile}"
printfn $"📁 Watch file changes: tail -f %s{Path.GetFullPath outputFile}"
printfn ""
printfn "⚡ All console input, HTTP API calls, and Claude file commands are logged with timestamps"
printfn "⚡ FSI output appears here in real-time and in Claude response files"
printfn ""
printfn "Press Ctrl+C to stop everything"

startWebServer config app
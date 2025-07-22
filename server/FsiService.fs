module FsiService

open System
open System.IO
open System.Diagnostics
open System.Text
open Microsoft.Extensions.Logging

type FsiInputSource = 
    | Console 
    | Api of string
    | FileSync of string

type FsiService(logger: ILogger<FsiService>) =  
    let mutable fsiProcess: Process option = None

    let getSourceName = function
        | Console -> "console"
        | Api source -> source
        | FileSync path -> path

    member _.StartFsi(args: string[]) =
        let fsiArgs = 
            if args.Length > 1 then
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
        fsiProcess <- Some proc
        
        // Stream all fsi output
        async {
            try
                while not proc.HasExited do
                    let! line = proc.StandardOutput.ReadLineAsync() |> Async.AwaitTask
                    if not (isNull line) then
                        let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                        let logLine = $"[%s{timestamp}] %s{line}"
                        Console.WriteLine(line)  // Show FSI output on console as normal
                        //TODO: mcp output streaming
            with
            | ex -> logger.LogError($"Output monitoring error: %s{ex.Message}")
        } |> Async.Start
        
        // Log FSI errors and show on console
        async {
            try
                while not proc.HasExited do
                    let! line = proc.StandardError.ReadLineAsync() |> Async.AwaitTask
                    if not (isNull line) then
                        let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                        let logLine = $"[%s{timestamp}] ERROR: %s{line}"
                        Console.WriteLine $"ERROR: %s{line}"
                        //TODO: mcp output streaming
            with
            | ex -> logger.LogError($"Error monitoring error: %s{ex.Message}")
        } |> Async.Start
        
        logger.LogInformation("🚀 FSI started with input/output interception!")
        logger.LogInformation($"⚙️  FSI command: dotnet {fsiArgs}")
        
        proc

    member _.SendToFsi(code: string, source: FsiInputSource) =
        match fsiProcess with
        | Some proc when not proc.HasExited ->
            let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
            let sourceName = getSourceName source
            let inputLog = $"[%s{timestamp}] INPUT (%s{sourceName}): %s{code.Trim()}"
            // Console.WriteLine $"(%s{sourceName})> %s{code.Trim()}"
            //TODO: mcp input streaming
            proc.StandardInput.WriteLine(code)
            proc.StandardInput.Flush()
            
            Ok "Code sent to FSI and logged"
        | _ -> Error "FSI not running"

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

    member _.Cleanup() =        
        match fsiProcess with
        | Some proc when not proc.HasExited ->
            proc.Kill()
            proc.WaitForExit(5000) |> ignore
        | _ -> ()

    interface IDisposable with
        member this.Dispose() = this.Cleanup()
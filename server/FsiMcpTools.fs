module FsiMcpTools

open System.ComponentModel
open ModelContextProtocol.Server
//to test: npx @modelcontextprotocol/inspector
//this uses SSE transport over http://localhost:5000/sse
type FsiTools(fsiService: FsiService.FsiService) =
    
    [<McpServerTool>]
    [<Description("Send F# code to the FSI (F# Interactive) session for execution.")>]
    member _.SendFSharpCode(code: string) : string = 
        match fsiService.SendToFsi(code, FsiService.FsiInputSource.Api "mcp") with
        | Ok result -> result
        | Error msg -> $"Error: {msg}"
    
    [<McpServerTool>]
    [<Description("Load and execute an F# script file (.fsx) in the FSI session. The file is parsed and statements are sent individually.")>]
    member _.LoadFSharpScript(filePath: string) : string = 
        match fsiService.SyncFileToFsi(filePath) with
        | Ok result -> result
        | Error msg -> $"Error: {msg}"
    
    [<McpServerTool>]
    [<Description("Get information about the FSI service status, directories, and session log file location.")>]
    member _.GetFsiStatus() : string = 
        let status = [
            "🚀 FSI Server Status:"
            ""
            "💡 Usage:"
            "- Use SendFSharpCode to execute F# expressions"
            "- Use LoadFSharpScript to load .fsx files"
            "- All input/output is logged with timestamps"
            "- FSI output appears in real-time"
        ]
        String.concat "\n" status
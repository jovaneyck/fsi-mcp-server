module FsiMcpTools
            
open System.ComponentModel
open ModelContextProtocol.Server

//to test: npx @modelcontextprotocol/inspector
//this uses SSE transport over http://localhost:5000/sse
type FsiTools(fsiService: FsiService.FsiService) =
    [<McpServerTool(Name=McpToolNames.SendFSharpCode)>]
    [<Description("Send F# code to the FSI (F# Interactive) session for execution.")>]
    member _.SendFSharpCode(agentName: string, code: string) : string = 
        match fsiService.SendToFsi(code, FsiService.FsiInputSource.Api agentName) with
        | Ok result -> result
        | Error msg -> $"Error: {msg}"
    
    [<McpServerTool>]
    [<Description("Load and execute an F# script file (.fsx) in the FSI session. The file is parsed and statements are sent individually.")>]
    member _.LoadFSharpScript(filePath: string) : string = 
        match fsiService.SyncFileToFsi(filePath) with
        | Ok result -> result
        | Error msg -> $"Error: {msg}"
    
    [<McpServerTool(Name=McpToolNames.GetRecentFsiEvents)>]
    [<Description("Get recent FSI events.")>]
    member _.GetRecentFsiEvents(count: int option) : string = 
        let eventCount = defaultArg count 10
        let events = fsiService.GetRecentEvents(eventCount)
        
        if events.Length = 0 then
            "No FSI events available yet. Execute some F# code first."
        else
            let eventStrings = events |> Array.map (fun e -> 
                $"[{e.Timestamp}] {e.EventType.ToUpper()} ({e.Source}): {e.Content}")
            String.concat "\n" eventStrings
    
    [<McpServerTool(Name=McpToolNames.GetFsiStatus)>]
    [<Description("Get information about the FSI service status.")>]
    member _.GetFsiStatus() : string =
        
        let sessionId = fsiService.GetSessionId()
        let totalEvents = fsiService.GetAllEvents().Length
        
        let status = [
            $"🚀 FSI Server Status (Session: {sessionId}):"
            ""
            $"📈 Event Statistics: {totalEvents} total events captured"
            ""
            "💡 Available Tools:"
            "- SendFSharpCode: Execute F# code (triggers real-time notifications)"
            "- LoadFSharpScript: Load .fsx files (triggers real-time notifications)"  
            "- GetFsiStatus: Get session info and statistics"
            ""
            "🔄 Multi-source Support:"
            "- Console input (direct typing)"
            "- MCP API calls (from agents)"
            ""
        ]
        String.concat "\n" status
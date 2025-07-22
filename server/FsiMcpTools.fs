module FsiMcpTools

open System.ComponentModel
open ModelContextProtocol.Server
open System.Text.Json
//to test: npx @modelcontextprotocol/inspector
//this uses SSE transport over http://localhost:5000/sse
type FsiTools(fsiService: FsiService.FsiService, resourceService: FsiMcpResourceService.FsiMcpResourceService) =
    
    [<McpServerTool>]
    [<Description("Send F# code to the FSI (F# Interactive) session for execution. Use GetFsiEvents to monitor real-time input/output.")>]
    member _.SendFSharpCode(agentName: string, code: string) : string = 
        match fsiService.SendToFsi(code, FsiService.FsiInputSource.Api agentName) with
        | Ok result -> result
        | Error msg -> $"Error: {msg}"
    
    [<McpServerTool>]
    [<Description("Load and execute an F# script file (.fsx) in the FSI session. The file is parsed and statements are sent individually. Use GetFsiEvents to monitor real-time input/output.")>]
    member _.LoadFSharpScript(filePath: string) : string = 
        match fsiService.SyncFileToFsi(filePath) with
        | Ok result -> result
        | Error msg -> $"Error: {msg}"
    
    [<McpServerTool>]
    [<Description("Get recent FSI events for debugging. Real-time events are automatically pushed via notifications/fsi_event.")>]
    member _.GetRecentFsiEvents(count: int option) : string = 
        let eventCount = defaultArg count 10
        let events = fsiService.GetRecentEvents(eventCount)
        
        if events.Length = 0 then
            "No FSI events available yet. Execute some F# code first."
        else
            let eventStrings = events |> Array.map (fun e -> 
                $"[{e.Timestamp}] {e.EventType.ToUpper()} ({e.Source}): {e.Content}")
            String.concat "\n" eventStrings
    
    [<McpServerTool>]
    [<Description("Get information about the FSI service status, session info, and real-time event monitoring.")>]
    member _.GetFsiStatus() : string = 
        let sessionId = fsiService.GetSessionId()
        let totalEvents = fsiService.GetAllEvents().Length
        
        let status = [
            $"🚀 FSI Server Status (Session: {sessionId}):"
            ""
            $"📈 Event Statistics: {totalEvents} total events captured"
            ""
            "🔔 REAL-TIME PUSH NOTIFICATIONS:"
            "- All FSI activity automatically pushed via 'notifications/fsi_event'"
            "- NO POLLING REQUIRED - events stream in real-time"
            "- Event types: 'input', 'output', 'error'"
            "- Input sources: 'console', 'api:agentName', 'file:filename'"
            ""
            "💡 Available Tools:"
            "- SendFSharpCode: Execute F# code (triggers real-time notifications)"
            "- LoadFSharpScript: Load .fsx files (triggers real-time notifications)"  
            "- GetRecentFsiEvents: Get recent events for debugging"
            "- GetFsiStatus: Get session info and statistics"
            ""
            "🔄 Multi-source Support:"
            "- Console input (direct typing)"
            "- MCP API calls (from agents)"
            "- File synchronization (.fsx loading)"
            "- ALL ACTIVITY PUSHED TO MCP CLIENTS IMMEDIATELY"
            ""
            "🔗 MCP Resource Subscription (RECOMMENDED):"
            "- Resource URI: 'fsi://events/stream'"
            "- Subscribe via MCP Inspector or MCP client"
            "- Automatic push notifications on FSI activity"
            "- Uses standard MCP notifications/resources/updated protocol"
        ]
        String.concat "\n" status
    
    [<McpServerResource>]
    [<Description("FSI Event Stream - Subscribe to fsi://events/stream for real-time push notifications.")>]
    member _.GetFsiEventStream(uri: string) : string =
        match resourceService.GetResourceContent(uri) with
        | Some content -> content
        | None -> "{\"error\": \"Unknown FSI resource URI\"}"
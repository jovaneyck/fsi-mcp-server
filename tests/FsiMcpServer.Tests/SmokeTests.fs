module SmokeTests

module Utilities =
    let public pollWithExponentialBackoff (predicate: unit -> bool) (maxTotalMs: int) =
        async {
            let mutable delay = 100 // Start with 100ms
            let mutable totalElapsed = 0
            let mutable found = false
            
            while not found && totalElapsed < maxTotalMs do
                if predicate() then
                    found <- true
                else
                    do! Async.Sleep delay
                    totalElapsed <- totalElapsed + delay
                    delay <- min (delay * 2) 2000 // Cap at 2 seconds max delay
            
            return found
        }

module ConsoleIOSmokeTests =
    open System
    open System.IO
    open System.Threading.Tasks
    open Xunit
    open Swensen.Unquote

    // Import the server's Program module functions
    open Program

    [<Fact>]
    let ``Basic F# code is evaluated`` () =
        async {
            let originalOut, originalIn = Console.Out, Console.In
            use output = new StringWriter()
            Console.SetOut output

            let script = "let foo = 1 + 3;;" + Environment.NewLine
            use input = new StringReader(script)
            Console.SetIn input

            try
                let (app, consoleTask) = createApp [||]
                consoleTask |> ignore
                let appTask = Task.Run(fun () -> app.Run())

                let expectedOutput = "val foo: int = 4"

                // Poll for FSI output with exponential backoff
                let! outputFound = 
                    Utilities.pollWithExponentialBackoff 
                        (fun () -> output.ToString().Contains(expectedOutput))
                        15000 // 15 seconds total timeout (increased for parallel test runs)
                
                let finalOut = output.ToString()
                test <@ outputFound @>
                test <@ finalOut.Contains(expectedOutput) @>

                do! app.DisposeAsync().AsTask() |> Async.AwaitTask
            finally
                Console.SetOut originalOut
                Console.SetIn originalIn
        }
        |> Async.RunSynchronously

module McpHttpSmokeTests =

    open Microsoft.AspNetCore.Mvc.Testing
    open ModelContextProtocol.Client
    open Xunit
    open Swensen.Unquote
    open Program
    open Xunit.Abstractions

    type McpHttpSmokeTests(output: ITestOutputHelper) =
        [<Fact>]
        let ``MCP server responds to status tool calls`` () =
            async {
                use factory = new WebApplicationFactory<Program>()
                use client = factory.CreateClient()

                let clientTransport =
                    new SseClientTransport(
                        new SseClientTransportOptions(Endpoint = client.BaseAddress),
                        client,
                        null,
                        false
                    )

                let! mcpClient =
                    McpClientFactory.CreateAsync(clientTransport)
                    |> Async.AwaitTask

                let! tools =
                    mcpClient.ListToolsAsync().AsTask()
                    |> Async.AwaitTask

                tools
                |> Seq.iter (fun tool -> output.WriteLine($"{tool.Name}"))

                test
                    <@ tools
                       |> Seq.exists (fun tool -> tool.Name = McpToolNames.GetFsiStatus) @>

                let! statusResult =
                    (mcpClient.CallToolAsync(McpToolNames.GetFsiStatus, null))
                        .AsTask()
                    |> Async.AwaitTask

                let statusText =
                    (statusResult.Content[0] :?> ModelContextProtocol.Protocol.TextContentBlock)
                        .Text

                test <@ statusText.Contains("🚀 FSI Server Status") @>
            }
            |> Async.StartAsTask

        [<Fact>]
        let ``MCP HTTP round trip - SendFSharpCode and GetRecentFsiEvents`` () =
            async {
                use factory = new WebApplicationFactory<Program>()
                use client = factory.CreateClient()

                let clientTransport =
                    new SseClientTransport(
                        new SseClientTransportOptions(Endpoint = client.BaseAddress),
                        client,
                        null,
                        false
                    )

                let! mcpClient =
                    McpClientFactory.CreateAsync(clientTransport)
                    |> Async.AwaitTask

                let! sendCodeResult =
                    (mcpClient.CallToolAsync(
                        McpToolNames.SendFSharpCode,
                        [ ("agentName", "smoketest" :> obj)
                          ("code", "let roundTripTest = 42 + 8;;" :> obj) ]
                        |> Map.ofSeq
                    ))
                        .AsTask()
                    |> Async.AwaitTask

                let sendCodeResultText =
                    (sendCodeResult.Content[0] :?> ModelContextProtocol.Protocol.TextContentBlock)
                        .Text

                test <@ sendCodeResultText.Contains("Code sent to FSI and logged") @>
                
                // Poll for FSI events with exponential backoff
                let mutable cachedResult = ""
                let! eventsFound = 
                    Utilities.pollWithExponentialBackoff 
                        (fun () -> 
                            try
                                let getLatestEventsResult =
                                    (mcpClient.CallToolAsync(
                                        McpToolNames.GetRecentFsiEvents,
                                        [ ("count", Option.Some 10 :> obj) ] |> Map.ofSeq
                                    ))
                                        .AsTask()
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let getLatestEventsResultText =
                                    (getLatestEventsResult.Content[0] :?> ModelContextProtocol.Protocol.TextContentBlock)
                                        .Text
                                cachedResult <- getLatestEventsResultText
                                getLatestEventsResultText.Contains("val roundTripTest: int = 50")
                            with
                            | _ -> false)
                        10000 // 10 seconds total timeout (increased from 2s)
                
                test <@ eventsFound @>
                test <@ cachedResult.Contains("val roundTripTest: int = 50") @>
                return ()
            }
            |> Async.StartAsTask

module HybridSmokeTests =

    open System
    open System.IO
    open Microsoft.AspNetCore.Mvc.Testing
    open ModelContextProtocol.Client
    open Xunit
    open Swensen.Unquote
    open Program
    open Xunit.Abstractions

    type HybridSmokeTests(output: ITestOutputHelper) =
        [<Fact>]
        let ``MCP SendFSharpCode appears in console output`` () =
            async {
                // Setup console redirection
                let originalOut, originalIn = Console.Out, Console.In
                use consoleOutput = new StringWriter()
                Console.SetOut consoleOutput
                use consoleInput = new StringReader("")
                Console.SetIn consoleInput

                try
                    // Setup WebApplicationFactory for MCP client
                    use factory = new WebApplicationFactory<Program>()
                    use client = factory.CreateClient()

                    let clientTransport =
                        new SseClientTransport(
                            new SseClientTransportOptions(Endpoint = client.BaseAddress),
                            client,
                            null,
                            false
                        )

                    let! mcpClient =
                        McpClientFactory.CreateAsync(clientTransport)
                        |> Async.AwaitTask

                    // Send F# code via MCP
                    let testCode = "let hybridTest = 100 + 23;;"

                    let! sendCodeResult =
                        (mcpClient.CallToolAsync(
                            McpToolNames.SendFSharpCode,
                            [ ("agentName", "hybridtest" :> obj)
                              ("code", testCode :> obj) ]
                            |> Map.ofSeq
                        ))
                            .AsTask()
                        |> Async.AwaitTask

                    let sendCodeResultText =
                        (sendCodeResult.Content[0] :?> ModelContextProtocol.Protocol.TextContentBlock)
                            .Text

                    test <@ sendCodeResultText.Contains("Code sent to FSI and logged") @>
                    
                    // Poll for FSI to process and output the result with exponential backoff
                    let! outputFound = 
                        Utilities.pollWithExponentialBackoff 
                            (fun () -> consoleOutput.ToString().Contains("val hybridTest: int = 123"))
                            10000 // 10 seconds total timeout
                    
                    // Verify the code execution result appears in console output
                    let consoleText = consoleOutput.ToString()
                    
                    test <@ outputFound @>
                    test <@ consoleText.Contains("val hybridTest: int = 123") @>

                finally
                    Console.SetOut originalOut
                    Console.SetIn originalIn
            }
            |> Async.StartAsTask

        [<Fact>]
        let ``Console input appears in MCP events`` () =
            async {
                // Setup console redirection BEFORE starting WebApplicationFactory
                let originalOut, originalIn = Console.Out, Console.In
                use consoleOutput = new StringWriter()
                Console.SetOut consoleOutput
                
                // Setup console input with F# code
                let testCode = "let consoleTest = 42 + 8;;" + Environment.NewLine
                use consoleInput = new StringReader(testCode)
                Console.SetIn consoleInput

                try
                    // Setup WebApplicationFactory (this should pick up the redirected console streams)
                    use factory = new WebApplicationFactory<Program>()
                    use client = factory.CreateClient()

                    let clientTransport =
                        new SseClientTransport(
                            new SseClientTransportOptions(Endpoint = client.BaseAddress),
                            client,
                            null,
                            false
                        )

                    let! mcpClient =
                        McpClientFactory.CreateAsync(clientTransport)
                        |> Async.AwaitTask

                    // Wait for the FSI server to start and process console input
                    do! Async.Sleep 2000

                    // Check if console output shows the evaluation result
                    let consoleText = consoleOutput.ToString()
                    output.WriteLine($"Console output: {consoleText}")

                    // Poll for MCP events to capture the console input
                    let mutable cachedEvents = ""
                    let! eventsFound = 
                        Utilities.pollWithExponentialBackoff 
                            (fun () -> 
                                try
                                    let getEventsResult =
                                        (mcpClient.CallToolAsync(
                                            McpToolNames.GetRecentFsiEvents,
                                            [ ("count", Option.Some 10 :> obj) ] |> Map.ofSeq
                                        ))
                                            .AsTask()
                                        |> Async.AwaitTask
                                        |> Async.RunSynchronously
                                    let eventsText =
                                        (getEventsResult.Content[0] :?> ModelContextProtocol.Protocol.TextContentBlock)
                                            .Text
                                    cachedEvents <- eventsText
                                    output.WriteLine($"MCP Events: {eventsText}")
                                    // Check for both the input and the evaluation result
                                    eventsText.Contains("let consoleTest = 42 + 8") && 
                                    eventsText.Contains("val consoleTest: int = 50")
                                with
                                | ex -> 
                                    output.WriteLine($"Error getting events: {ex.Message}")
                                    false)
                            10000 // 10 seconds timeout

                    // Verify console input appears in MCP event history
                    test <@ eventsFound @>
                    test <@ cachedEvents.Contains("let consoleTest = 42 + 8") @>
                    test <@ cachedEvents.Contains("val consoleTest: int = 50") @>

                finally
                    Console.SetOut originalOut
                    Console.SetIn originalIn
            }
            |> Async.StartAsTask

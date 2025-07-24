module SmokeTests

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
            use input  = new StringReader(script)
            Console.SetIn  input

            try
                let (app, consoleTask) = createApp [||]
                consoleTask |> ignore
                let appTask = Task.Run(fun () -> app.Run())

                let expectedOutput = "val foo: int = 4"
                let rec wait n =
                    async {
                        if n = 50 then return output.ToString()
                        elif output.ToString().Contains(expectedOutput) then
                            return output.ToString()
                        else
                            do! Async.Sleep 200
                            return! wait (n + 1)
                    }
                let! finalOut = wait 0
                test <@ finalOut.Contains(expectedOutput) @>

                do! app.DisposeAsync().AsTask() |> Async.AwaitTask
            finally
                Console.SetOut originalOut
                Console.SetIn  originalIn
        } |> Async.RunSynchronously

module McpHttpSmokeTests =

    open System
    open System.Threading.Tasks
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
                do! Task.Delay(2000) |> Async.AwaitTask //Allow fsi.exe to eval and print output
                let! getLatestEventsResult =
                    (mcpClient.CallToolAsync(
                        McpToolNames.GetRecentFsiEvents,
                        [ ("count", Option.Some 10 :> obj)]
                        |> Map.ofSeq
                    ))
                        .AsTask()
                    |> Async.AwaitTask
                
                let getLatestEventsResultText =
                    (getLatestEventsResult.Content[0] :?> ModelContextProtocol.Protocol.TextContentBlock)
                        .Text
                test <@ getLatestEventsResultText.Contains("val roundTripTest: int = 50") @>
                return ()
            }
            |> Async.StartAsTask

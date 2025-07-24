module McpHttpSmokeTests

open System.Net.Http
open System.Text
open System.Text.Json
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

    //[<Fact>]
    let ``MCP HTTP round trip - SendFSharpCode and GetRecentFsiEvents`` () =
        async {
            use factory = new WebApplicationFactory<Program>()
            use client = factory.CreateClient()

            // Step 1: Execute F# code via SendFSharpCode
            let codeRequest =
                {| method = "tools/call"
                   params =
                    {| name = "send_fsharp_code"
                       arguments =
                        {| agentName = "TestAgent"
                           code = "let roundTripTest = 42 + 8;;" |}
                       _meta = {| progressToken = 0 |} |}
                   jsonrpc = "2.0"
                   id = 1 |}

            let codeJson = JsonSerializer.Serialize(codeRequest)
            let codeContent = new StringContent(codeJson, Encoding.UTF8, "application/json")

            // Debug: Print request
            printfn "Request JSON: %s" codeJson

            let! codeResponse =
                client.PostAsync("/message", codeContent)
                |> Async.AwaitTask

            let! codeResponseString =
                codeResponse.Content.ReadAsStringAsync()
                |> Async.AwaitTask

            // Debug: Print response for troubleshooting
            printfn "Response Status: %d %s" (int codeResponse.StatusCode) (codeResponse.ReasonPhrase)
            printfn "Response Content: %s" codeResponseString

            // Verify code execution
            test <@ codeResponse.IsSuccessStatusCode @>

            // Step 2: Retrieve the events via GetRecentFsiEvents
            let eventsRequest =
                {| method = "tools/call"
                   params =
                    {| name = "get_recent_fsi_events"
                       arguments = {| count = 5 |}
                       _meta = {| progressToken = 1 |} |}
                   jsonrpc = "2.0"
                   id = 2 |}

            let eventsJson = JsonSerializer.Serialize(eventsRequest)
            let eventsContent = new StringContent(eventsJson, Encoding.UTF8, "application/json")

            let! eventsResponse =
                client.PostAsync("/message", eventsContent)
                |> Async.AwaitTask

            let! eventsResponseString =
                eventsResponse.Content.ReadAsStringAsync()
                |> Async.AwaitTask

            // Verify events contain the executed code
            test <@ eventsResponse.IsSuccessStatusCode @>
            test <@ eventsResponseString.Contains("roundTripTest") @>
            test <@ eventsResponseString.Contains("TestAgent") @>
            test <@ eventsResponseString.Contains("API") @>

        }
        |> Async.RunSynchronously
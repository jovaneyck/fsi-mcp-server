//How to test: https://github.com/modelcontextprotocol/inspector
//λ npm i -g @modelcontextprotocol/server-everything
//npx @modelcontextprotocol/inspector

open System
open System.ComponentModel
open System.Linq
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open ModelContextProtocol.Server

[<McpServerToolType>]
type EchoTool() =
   
    [<McpServerTool>]
    [<Description("Echoes the message back to the client.")>]
    static member Echo(message: string) : string = 
        $"Hello from F#: {message}"
    
    [<McpServerTool>]
    [<Description("Echoes in reverse the message sent by the client.")>]
    static member ReverseEcho(message: string) : string = 
        message.ToCharArray() |> Array.rev |> String

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    // Add MCP server services with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<EchoTool>()
    |> ignore

    let app = builder.Build()

    // Configure the HTTP request pipeline
    app.UseDeveloperExceptionPage() |> ignore

    // Map MCP endpoints
    app.MapMcp() |> ignore

    // Add a simple home page
    app.MapGet("/status", Func<string>(fun () -> "MCP Server - Ready for use with HTTP transport"))
    |> ignore

    app.Run()
    0
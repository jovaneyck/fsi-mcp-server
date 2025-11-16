//How to test: https://github.com/modelcontextprotocol/inspector
//λ npm i -g @modelcontextprotocol/server-everything
//npx @modelcontextprotocol/inspector

open System
open System.ComponentModel
open System.Linq
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.HttpOverrides
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
        .AddCors()
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<EchoTool>()
    |> ignore

    let app = builder.Build()

    // Configure the HTTP request pipeline
    app.UseDeveloperExceptionPage() |> ignore
    
    // Add CORS for external access from WSL2
    app.UseCors(fun policy -> 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod() |> ignore
    ) |> ignore

    // Disable strict host header checking for WSL2 access
    let forwardedHeadersOptions = ForwardedHeadersOptions()
    forwardedHeadersOptions.ForwardedHeaders <- ForwardedHeaders.XForwardedHost ||| ForwardedHeaders.XForwardedProto
    app.UseForwardedHeaders(forwardedHeadersOptions) |> ignore

    // Map MCP endpoints
    app.MapMcp() |> ignore

    // Add a simple home page
    app.MapGet("/status", Func<string>(fun () -> "F# MCP Server - Ready for use with HTTP transport"))
    |> ignore

    app.Run()
    0
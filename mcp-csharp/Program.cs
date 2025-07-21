//How to test: https://github.com/modelcontextprotocol/inspector
//λ npm i -g @modelcontextprotocol/server-everything
//npx @modelcontextprotocol/inspector dotnet run

using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add MCP server services with HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTool>();

var app = builder.Build();

// Configure the HTTP request pipeline

app.UseDeveloperExceptionPage();

// Map MCP endpoints
app.MapMcp();

// Add a simple home page
app.MapGet("/status", () => "MCP Server - Ready for use with HTTP transport");

app.Run();

[McpServerToolType]
public class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"Hello from C#: {message}";

    [McpServerTool, Description("Echoes in reverse the message sent by the client.")]
    public static string ReverseEcho(string message) => new string(message.Reverse().ToArray());
}
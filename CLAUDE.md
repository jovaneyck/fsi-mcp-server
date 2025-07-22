# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FSI MCP Server is a drop-in replacement for F# Interactive (`fsi.exe`) that adds Model Context Protocol capabilities. It wraps the standard F# Interactive process while maintaining full CLI compatibility and provides programmatic access via MCP tools.

## Build and Development Commands

### Essential Commands
```bash
# Build the entire solution
dotnet build

# Run the main FSI MCP Server (starts on http://0.0.0.0:5020)
dotnet run --project server

# Run with FSI arguments (all fsi.exe args are supported)
dotnet run --project server -- --nologo --define:DEBUG

# Build specific project
dotnet build server/fsi-mcp-server.fsproj

# Clean build artifacts
dotnet clean
```

### Example Projects
```bash
# Run C# MCP example client
dotnet run --project mcp-csharp

# Run F# MCP example client  
dotnet run --project mcp-fsharp
```

### Testing and Debugging
```bash
# Test with MCP Inspector tool
npx @modelcontextprotocol/inspector

# Use scratch.fsx for manual F# Interactive testing
dotnet fsi scratch.fsx
```

## Architecture Overview

### Core Components

1. **FsiService** (`server/FsiService.fs`): Core service that wraps `dotnet fsi` process, manages I/O interception, and handles event queuing for both console and MCP inputs.

2. **FsiMcpTools** (`server/FsiMcpTools.fs`): Exposes MCP tools that provide programmatic access to FSI sessions:
   - `SendFSharpCode`: Execute F# code in FSI session
   - `LoadFSharpScript`: Load and execute .fsx files
   - `GetRecentFsiEvents`: Access FSI event history. No streaming supported yet.
   - `GetFsiStatus`: Get session information

3. **Program.fs**: ASP.NET Core host that serves MCP endpoints with HTTP transport on `http://0.0.0.0:5020`

### Key Features

- **Hybrid Usage**: Supports simultaneous console input and MCP API calls
- **CLI Compatibility**: Complete drop-in replacement for `fsi.exe`
- **Event System**: Real-time streaming of FSI input/output via MCP (Server-Sent Events transport)
- **Multi-source Tracking**: Tracks input sources (console, API, file sync)
- **Session State**: Maintains persistent session state and event history

### MCP Integration

The server exposes FSI functionality through MCP tools that can be called by AI assistants or other MCP clients. All FSI interactions are captured as events and can be retrieved programmatically.

## Development Notes

### Target Framework
- .NET 9.0
- Requires `dotnet fsi` available in PATH

### Key Dependencies
- `ModelContextProtocol` (v0.3.0-preview.3): Core MCP functionality
- `ModelContextProtocol.AspNetCore`: ASP.NET Core MCP integration
- `Microsoft.Extensions.Hosting`: .NET hosting infrastructure

### Project Structure
- `server/`: Main FSI MCP Server implementation
- `mcp-csharp/`: C# example MCP client
- `mcp-fsharp/`: F# example MCP client
- `scratch.fsx`: F# script for manual testing

### Network Configuration
- Default endpoint: `http://0.0.0.0:5020`
- MCP transport: HTTP SSE
- Current limitation: hardcoded IP/ports (future enhancement needed)

### Integration Use Cases
- JetBrains Rider F# Interactive replacement
- Claude Code/AI agent integration for F# development through MCP server
- Programmatic access to F# Interactive session IO
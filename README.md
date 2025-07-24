# FSI mcp Server

A drop-in replacement for `fsi.exe` that adds MCP capabilities to F# Interactive sessions. Perfect for AI-assisted development workflows where you need programmatic access to F# Interactive.

## Overview

FSI Server wraps the standard F# Interactive process while maintaining full CLI compatibility. It intercepts all FSI input/output to provide:

- **Hybrid Usage**: Mix console input and MCP calls in the same session
- **Full CLI Compatibility**: Use as a drop-in replacement for `fsi.exe`. Only arguments prefixed with `fsi:` are passed to the underlying FSI process

## Quick Start

### Building the Project

```bash
# Clone and build
git clone <repository-url>
cd fsi-mcp-server
dotnet build

# Run as FSI replacement  
dotnet run -- fsi:--nologo fsi:--load:script.fsx
```

### Basic Usage

```bash
# Start FSI Server (runs on http://0.0.0.0:5020)
dotnet run
```

## JetBrains Rider Integration

### Preferred: Replace F# Interactive (Recommended)

Replace Rider's built-in F# Interactive with FSI Server for seamless AI integration:

1. Open **File → Settings → Languages & Frameworks → F# → F# Interactive**
2. In the **F# Interactive executable** field, replace the default path with:
   ```
   C:\path\to\fsi-mcp-server.exe
   ```
3. Click **OK** to save

**Benefits:**
- Use Rider's F# Interactive normally (Send to F# Interactive, etc.)
- FSI sessions interactions automatically get AI integration capabilities through an integrated MCP server.
- No workflow changes required - just enhanced functionality

### Alternative: External Tool

1. Open **File → Settings → Tools → External Tools**
2. Click **+** to add a new tool:
   - **Name**: `FSI Server`
   - **Program**: `dotnet`
   - **Arguments**: `run --project path/to/fsi-mcp-server`
   - **Working directory**: `$ProjectFileDir$`
3. Click **OK** to save

### Alternative: Terminal Integration

1. Open Rider's integrated terminal
2. Navigate to your fsi-server directory
3. Run: `dotnet run`
4. FSI Server starts and accepts both console input and API calls

### AI Assistant Integration

Many AI coding assistants can now interact with FSI Server's MCP API.

#### Setting Up Claude Code Integration

For optimal Claude Code integration, create a system prompt file ([fsi-server-claude.md](fsi-server-claude.md)) that instructs Claude how to interact with your FSI Server:
Then, install the mcp server:

native:
```shell
 claude mcp add --transport sse fsi-server  http://localhost:5020/sse
```

wsl (TODO clean this up):
```shell
 claude mcp add --transport sse fsi-server  http://G1SGSG3.mshome.net:5020/sse
```

**Key Integration Features:**
- **Dual-action workflow**: Code is both executed in FSI and saved to collaborative script files
- **Silent execution**: Claude works as a silent partner, not echoing FSI results
- **Collaborative scripting**: Maintains persistent `.fsx` files for session state
- **Coding style consistency**: Follows your project's F# conventions and testing patterns

**Setup Steps:**
1. Create `fsi-server-claude.md` with your FSI Server endpoint and workflow preferences
2. Configure Claude Code to use this as a system prompt for F# projects
3. Start FSI Server: `dotnet run`
4. Claude will automatically send F# code to your FSI session while building collaborative scripts

This creates a seamless experience where you can work with F# interactively while Claude assists by executing code, running tests, and maintaining collaborative scripts.

## Typical Programming Session Workflow

### 1. Start FSI Server
```bash
cd your-fsharp-project
dotnet run --project path/to/fsi-server
```

### 2. Load Your Project Context
```fsharp
// In console or via API
#r "path/to/your/project.dll";;
#load "YourModule.fs";;
```

### 3. Hybrid Development
- **Console**: Direct F# experimentation and REPL interaction
- **MCP**: AI assistant sends code snippets for evaluation and reads fsi.exe output to close the loop.

## Advanced Configuration

None yet, even the local ip/ports are hardcoded for now 😂 
It was a bit tricky to get this setup running in a Windows + wsl2 setup where claude running in wsl needs to call out to the mcp server running in the windows host.
If you have experience with this of wsl networking stuff, please contact me!

### CLI Pass-through
FSI command-line arguments must be prefixed with `fsi:` to be passed to the underlying FSI process:
```bash
dotnet run -- fsi:--nologo fsi:--define:DEBUG fsi:--load:setup.fsx
```

## Requirements

- .NET 6.0 or later
- F# Interactive (`dotnet fsi`) available in PATH

## Contributing

This project follows standard F# coding conventions.

## License

See LICENSE file for details.
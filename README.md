# FSI Server

A drop-in replacement for `fsi.exe` that adds HTTP API capabilities and enhanced logging to F# Interactive sessions. Perfect for AI-assisted development workflows where you need programmatic access to F# Interactive.

## Overview

FSI Server wraps the standard F# Interactive process while maintaining full CLI compatibility. It intercepts all FSI input/output to provide:

- **HTTP API**: Send F# code to FSI via REST endpoints
- **Session Logging**: All interactions logged with timestamps to `c:/tmp/fsi-session.log`
- **Hybrid Usage**: Mix console input and API calls in the same session
- **Full CLI Compatibility**: Use as a drop-in replacement for `fsi.exe`

## Quick Start

### Building the Project

```bash
# Clone and build
git clone <repository-url>
cd fsi-server
dotnet build

# Run as FSI replacement
dotnet run -- --nologo --load:script.fsx
```

### Basic Usage

```bash
# Start FSI Server (runs on http://0.0.0.0:8080)
dotnet run

# Send code via API
curl -X POST 'http://localhost:8080/send?source=claude' -d 'let x = 42;;'

# Sync an F# script file
curl -X POST 'http://localhost:8080/sync-file?file=script.fsx'
```

## JetBrains Rider Integration

### Preferred: Replace F# Interactive (Recommended)

Replace Rider's built-in F# Interactive with FSI Server for seamless AI integration:

1. Open **File → Settings → Languages & Frameworks → F# → F# Interactive**
2. In the **F# Interactive executable** field, replace the default path with:
   ```
   C:\path\to\fsi-server.exe
   ```
3. Click **OK** to save

**Benefits:**
- Use Rider's F# Interactive normally (Send to F# Interactive, etc.)
- All FSI interactions automatically get AI integration capabilities
- Session logging captures both manual and AI interactions
- No workflow changes required - just enhanced functionality

### Alternative: External Tool

1. Open **File → Settings → Tools → External Tools**
2. Click **+** to add a new tool:
   - **Name**: `FSI Server`
   - **Program**: `dotnet`
   - **Arguments**: `run --project path/to/fsi-server`
   - **Working directory**: `$ProjectFileDir$`
3. Click **OK** to save

### Alternative: Terminal Integration

1. Open Rider's integrated terminal
2. Navigate to your fsi-server directory
3. Run: `dotnet run`
4. FSI Server starts and accepts both console input and API calls

### AI Assistant Integration

Many AI coding assistants can now interact with FSI Server's HTTP API:

```bash
# Example: Send code from AI assistant
curl -X POST 'http://localhost:8080/send?source=ai-assistant' \
  -H 'Content-Type: text/plain' \
  -d 'let fibonacci n = 
    let rec fib a b n =
      if n = 0 then a
      else fib b (a + b) (n - 1)
    fib 0 1 n;;'
```

#### Setting Up Claude Code Integration

For optimal Claude Code integration, create a system prompt file ([fsi-server-claude.md](fsi-server-claude.md)) that instructs Claude how to interact with your FSI Server:

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

**Example Workflow:**
```bash
# Claude sends code to FSI
curl -X POST 'http://localhost:8080/send?source=claude' -d 'let result = 42 * 2;;'

# Claude simultaneously appends to collaborative script (without ;;)
# File: scratch.fsx
let result = 42 * 2
```

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
- **API**: AI assistant sends code snippets for evaluation
- **File Sync**: Sync entire .fsx files with `;;` delimited statements

### 4. Session Logging
All interactions are logged to `c:/tmp/fsi-session.log`:
```
[2024-01-15 14:30:25] (console) let x = 42;;
[2024-01-15 14:30:26] (api:claude) let y = x * 2;;
[2024-01-15 14:30:27] (file-sync:test.fsx) let z = x + y;;
```

## API Reference

### POST /send
Send F# code to the FSI session.

**Parameters:**
- `source` (query): Source identifier (e.g., "claude", "copilot", "manual")

**Body:** F# code as plain text

**Example:**
```bash
curl -X POST 'http://localhost:8080/send?source=claude' \
  -H 'Content-Type: text/plain' \
  -d 'let greeting = "Hello, World!";;'
```

### POST /sync-file
Parse and execute an F# script file.

**Parameters:**
- `file` (query): Path to .fsx file

**Features:**
- Splits code on `;;` delimiters
- Filters out comments and empty lines
- Executes each statement sequentially

**Example:**
```bash
curl -X POST 'http://localhost:8080/sync-file?file=examples/demo.fsx'
```

## Use Cases

### AI-Assisted Development
- AI assistants can send code snippets for immediate evaluation
- Session logging provides audit trail of AI interactions
- Hybrid console/API usage for maximum flexibility

### Interactive Development
- Use console for exploration and debugging
- Use API for automated testing and validation
- File sync for batch processing of script files

### Educational/Training
- Students can experiment via console
- Instructors can send examples via API
- All interactions logged for review

## Advanced Configuration

### Custom Log Location
Modify `Program.fs:15` to change log file location:
```fsharp
let logPath = "your/custom/path/fsi-session.log"
```

### Port Configuration
Modify `Program.fs` to change server port:
```fsharp
let config = { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 9090 ] }
```

### CLI Pass-through
All FSI command-line arguments are supported:
```bash
dotnet run -- --nologo --define:DEBUG --load:setup.fsx
```

## Requirements

- .NET 6.0 or later
- F# Interactive (`dotnet fsi`) available in PATH
- Suave web framework (included in project dependencies)

## Contributing

This project follows standard F# coding conventions. The entire application logic is contained in `Program.fs` for simplicity.

## License

See LICENSE file for details.
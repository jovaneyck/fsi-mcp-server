# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an F# Interactive (FSI) wrapper that provides a drop-in replacement for `fsi.exe` with enhanced logging and HTTP API capabilities. It intercepts all FSI input/output to a session log file while maintaining full CLI compatibility.

## Key Architecture

- **Main Entry Point**: `Program.fs` - Contains the entire application logic
- **Process Management**: Spawns a `dotnet fsi` process with CLI argument pass-through
- **HTTP API**: Built with Suave web framework, provides REST endpoints for FSI interaction
- **Session Logging**: All FSI input/output is logged with timestamps to `c:/tmp/fsi-session.log`
- **Asynchronous Monitoring**: Uses async workflows to monitor FSI process output/error streams
- **Console Forwarding**: Bidirectional console I/O forwarding for transparent FSI usage

## Common Commands

### Build and Run
```bash
# Build the project
dotnet build

# Run as FSI replacement with CLI args
dotnet run -- --nologo --load:script.fsx

# Run with console interaction
dotnet run
```

### API Integration
```bash
# Send code to FSI
curl -X POST 'http://localhost:8080/send?source=claude' -d 'let x = 42;;'

# Sync an F# script file to FSI
curl -X POST 'http://localhost:8080/sync-file?file=script.fsx'
```

## API Endpoints

- `POST /send?source=<name>` - Send F# code to FSI session
- `POST /sync-file?file=<path>` - Parse and sync an .fsx file to FSI (splits on `;;` delimiters)

## Key Features

### CLI Compatibility
- All command-line arguments are passed through to the FSI process (Program.fs:21-28)
- Can be used as a drop-in replacement for `fsi.exe`
- Maintains full FSI functionality while adding logging and HTTP API

### Usage Modes
- **Console Mode**: Direct F# command input in console
- **API Mode**: HTTP endpoints for programmatic interaction
- **Hybrid Mode**: Mix both console input and API calls simultaneously

### Session Logging
- All interactions (console + API) are logged with timestamps
- Log file: `c:/tmp/fsi-session.log`
- Input sources are tracked: `(console)`, `(api)`, `(file-sync:filename)`

## Important Notes

- The server runs on `http://0.0.0.0:8080` by default
- FSI process cleanup is handled automatically on Ctrl+C
- API inputs are echoed to console for visibility
- The `/sync-file` endpoint parses F# statements using `;;` delimiters and filters out comments
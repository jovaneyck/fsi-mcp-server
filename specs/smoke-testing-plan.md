# FSI MCP Server Smoke Testing Plan

## Overview

This document outlines the smoke testing strategy for the FSI MCP Server project using xUnit and ASP.NET Core's WebApplicationFactory for in-memory testing.

## Current Architecture Issues

### Testing Gaps Identified
- **No test coverage** - Zero tests for critical FSI process management
- **Tight coupling** - FsiService directly manages process lifecycle and event queuing
- **Hard-coded dependencies** - Direct Console.WriteLine calls mixed with business logic
- **No error boundary testing** - Process failures could leave system in undefined state
- **Concurrency concerns** - ConcurrentQueue without proper synchronization testing

## Smoke Test Strategy

### Primary Focus: End-to-End Smoke Tests
Using xUnit with ASP.NET Core TestServer and WebApplicationFactory for in-memory testing.

### Test Categories

#### 1. Console I/O Smoke Test
**Objective**: Verify FSI process can receive console input and produce output

**Test Setup & Infrastructure**
- Create custom `WebApplicationFactory<Program>` that starts the FSI MCP server in-memory
- Override console input/output streams with testable `StringWriter`/`StringReader` 
- Use dependency injection to replace `Console` with test doubles during testing
- Set up proper cleanup to kill FSI processes after each test

**Test Scenarios**
- **Basic F# Execution**: Send `"1 + 1"` via console, verify `"val it : int = 2"` appears in output
- **Multi-line Statements**: Test `"let x = 5\nx * 2"` and verify proper execution
- **Error Handling**: Send invalid F# code, verify error messages captured in stderr
- **Command Line Args**: Test FSI startup with `--nologo`, `--define:DEBUG` arguments

**Technical Approach**
- **Console Redirection**: Use `Console.SetIn()/SetOut()/SetError()` to redirect streams for testing
- **Async Output Capture**: Monitor FSI output streams with timeout-based assertions
- **Event Queue Verification**: Check that console inputs appear in `FsiService.GetRecentEvents()`
- **Process Health**: Verify FSI process starts successfully and remains alive during test

**Key Challenges to Address**
- **Timing Issues**: FSI output is async - need proper `Task.Delay()` or polling for output
- **Stream Isolation**: Ensure tests don't interfere with each other's console streams
- **Process Cleanup**: Guarantee FSI processes are killed even if tests fail
- **Output Parsing**: Handle FSI's complex output format (prompts, results, errors)

#### 2. MCP HTTP Endpoint Smoke Test
**Objective**: Verify MCP tools work via HTTP API calls
- Test `/mcp/tools/SendFSharpCode` endpoint
- Test `/mcp/tools/LoadFSharpScript` endpoint  
- Test `/mcp/tools/GetRecentFsiEvents` endpoint
- Test `/mcp/tools/GetFsiStatus` endpoint
- Validate JSON request/response formats
- Verify error handling for invalid inputs

#### 3. Hybrid Mode Smoke Test
**Objective**: Ensure simultaneous console + MCP API usage works
- Start FSI session via console
- Execute F# code via MCP API while console session active
- Verify both input sources appear in event stream
- Validate proper source attribution (console vs api)

#### 4. FSI Process Lifecycle Smoke Test
**Objective**: Test process management robustness
- Test graceful startup and shutdown
- Test behavior when FSI process crashes
- Test cleanup on application shutdown
- Verify session state persistence during normal operations

## Test Infrastructure Requirements

### Test Project Structure
```
tests/
├── FsiMcpServer.Tests/
│   ├── FsiMcpServer.Tests.fsproj
│   ├── TestData.fs
│   └── FsiMcpServerTests.fs
```

### Key Dependencies
- `Microsoft.AspNetCore.Mvc.Testing` - WebApplicationFactory support
- `xunit` - Test framework
- `FluentAssertions` - Better assertion syntax
- Project reference to main FSI MCP Server

### Test Data
- Pre-built F# test scripts (.fsx files)
- Sample input/output scenarios
- Error condition test cases

## Implementation Approach

1. **WebApplicationFactory Setup**: In-memory ASP.NET Core testing
2. **FSI Process Mocking**: Consider test doubles for FSI process interaction
3. **HTTP Client Testing**: Direct API calls to MCP endpoints
4. **Event Stream Validation**: Verify proper event queuing and retrieval
5. **Cleanup Verification**: Ensure no resource leaks in tests

## Success Criteria

- All smoke tests pass consistently
- Tests complete within reasonable time (< 30 seconds total)
- No process leaks or resource cleanup issues
- Tests can run in parallel without interference
- Clear failure messages when issues occur

## Future Considerations

Once smoke tests are stable:
- Add integration tests for complex scenarios
- Add unit tests for individual components
- Consider performance testing for high-load scenarios
- Add CI/CD pipeline integration
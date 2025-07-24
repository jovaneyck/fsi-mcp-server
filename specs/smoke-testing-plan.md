# FSI MCP Server Smoke Testing Plan

## Overview

This document outlines the smoke testing strategy for the FSI MCP Server project using xUnit and ASP.NET Core's WebApplicationFactory for in-memory testing.

## Task list
* Tests cannot run concurrently
* 2 app startup paradigms in smoketests, consolidate

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

**Status**: ✅ **COMPLETE** - Console I/O smoke test fully working with proper input/output synchronization

**Test Setup & Infrastructure**
- ✅ Created test project with direct Program module access (no WebApplicationFactory needed)
- ✅ Console stream redirection working with `StringWriter`/`StringReader`
- ✅ Direct FSI service integration via extracted `createApp()` function
- ✅ Proper cleanup with FSI process termination

**Test Scenarios**
- ✅ **Basic F# Execution**: Successfully detects FSI startup ("F# Interactive version")
- ✅ **Input Processing**: Successfully sends input and captures output ("val it : int = 2")
- ✅ **Threading Synchronization**: Fixed multithreading coordination issues
- ❌ **Multi-line Statements**: Not yet implemented
- ❌ **Error Handling**: Not yet implemented  
- ❌ **Command Line Args**: Not yet implemented

**Technical Approach**
- ✅ **Console Redirection**: `Console.SetIn()/SetOut()` working correctly
- ✅ **FSI Process Management**: Direct access to FsiService for process control
- ✅ **Async Coordination**: Threading issues resolved through CLI integration
- ✅ **Process Health**: FSI starts successfully and captures initial output

**Issues Resolved**
- ✅ **Threading Coordination**: Fixed through CLI integration approach
- ✅ **Output Timing**: Proper synchronization between sending input and capturing output
- ✅ **Stream Buffering**: Console output buffering issues resolved
- ✅ **Process Cleanup**: Working correctly with proper disposal

**Next Steps**
- Implement remaining test scenarios (multi-line statements, error handling, command line args)
- Move to MCP HTTP endpoint smoke tests
- Implement hybrid mode testing

#### 2. MCP HTTP Endpoint Smoke Test
**Objective**: Verify MCP tools work via HTTP API calls

**Status**: ✅ **COMPLETE** - MCP HTTP smoke tests fully working with proper SSE transport and tool integration

**Test Setup & Infrastructure**
- ✅ WebApplicationFactory setup for in-memory ASP.NET Core testing
- ✅ SSE (Server-Sent Events) client transport integration
- ✅ MCP client factory and tool discovery working
- ✅ Proper async/await patterns throughout test suite

**Test Scenarios**
- ✅ **Tool Discovery**: Successfully lists available MCP tools including GetFsiStatus
- ✅ **GetFsiStatus Tool**: Validates FSI server status response format
- ✅ **SendFSharpCode Tool**: Successfully sends F# code via MCP API
- ✅ **GetRecentFsiEvents Tool**: Retrieves and validates FSI evaluation results
- ✅ **Round-trip Integration**: Full cycle from code submission to result retrieval
- ❌ **LoadFSharpScript Tool**: Not yet implemented
- ❌ **Error Handling**: Invalid input validation not yet implemented

**Technical Approach**
- ✅ **SSE Transport**: `SseClientTransport` working correctly with WebApplicationFactory
- ✅ **MCP Client Integration**: `McpClientFactory.CreateAsync()` functioning properly
- ✅ **Tool Parameter Passing**: Map-based parameter passing to MCP tools
- ✅ **Content Block Parsing**: Proper parsing of TextContentBlock responses
- ✅ **Timing Coordination**: 2-second delay allows FSI evaluation to complete

**Issues Resolved**  
- ✅ **Transport Configuration**: SSE client transport setup with WebApplicationFactory
- ✅ **Async Coordination**: Proper async/await usage throughout MCP client calls
- ✅ **Response Parsing**: Successful extraction of tool response content
- ✅ **Test Isolation**: Each test runs independently without interference

**Next Steps**
- Implement LoadFSharpScript tool testing
- Add error handling test scenarios
- Move to hybrid mode testing (console + MCP simultaneous usage)

#### 3. Hybrid Mode Smoke Test
**Objective**: Ensure simultaneous console + MCP API usage works

**Status**: 🔄 **IN PROGRESS** - Ready to implement hybrid scenario testing

**Test Setup & Infrastructure**
- 🔄 Combine console I/O redirection with WebApplicationFactory
- 🔄 Dual-source input coordination (console streams + MCP HTTP calls)
- 🔄 Event stream monitoring for multi-source attribution
- 🔄 Timing coordination between console and API interactions

**Test Scenarios**
- ✅ **MCP-to-Console Verification**: Send F# code via MCP, verify output appears in console stream
- ❌ **Console-to-MCP Verification**: Execute console commands, verify events captured via GetRecentFsiEvents
- ❌ **Simultaneous Operations**: Concurrent console input and MCP API calls
- ❌ **Source Attribution**: Validate proper tagging of input sources (console vs api vs file)
- ❌ **Event Ordering**: Verify chronological ordering of events from multiple sources

**Technical Approach**
- ✅ **Dual Transport Setup**: Console redirection + SSE client transport in same test
- ❌ **Event Stream Monitoring**: Real-time validation of FSI event queue
- ✅ **Cross-Source Validation**: Verify MCP input appears in console output
- ✅ **Timing Coordination**: Proper async coordination between console and HTTP operations
- ✅ **Session State Consistency**: Validate shared FSI session state across input sources

**Implementation Plan**
1. ✅ **First Scenario**: Send F# code via MCP SendFSharpCode, verify evaluation result appears in console output
2. ❌ **Bidirectional Test**: Console input visible via MCP GetRecentFsiEvents  
3. ❌ **Concurrent Access**: Multiple input sources operating simultaneously
4. ❌ **Event Stream Validation**: Comprehensive event ordering and attribution testing

**Success Criteria**
- ✅ F# code sent via MCP API appears in console output stream
- ❌ Console commands appear in MCP event history
- ❌ Proper source attribution maintained throughout
- ❌ No interference between input sources
- ✅ Session state remains consistent across all access methods

**Implementation Details**
- ✅ **Test Created**: `HybridSmokeTests.``MCP SendFSharpCode appears in console output```
- ✅ **Console Redirection**: Successfully captures FSI output using StringWriter
- ✅ **MCP Integration**: WebApplicationFactory + SSE transport working in hybrid mode
- ✅ **Code Verification**: Test sends `let hybridTest = 100 + 23;;` and verifies `val hybridTest: int = 123` in console output
- ✅ **Timing Coordination**: 2-second delay ensures FSI evaluation completes before verification

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
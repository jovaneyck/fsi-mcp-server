# FSI Server Integration Guide for Claude Code

This guide explains how to collaborate with the FSI server through the fsi-server mcp server.

## MCP-Based FSI Session Integration

Agents send over commands through MCP and can look at fsi outputs through MCP.
The user can also directly input statements in the fsi.exe console.
The agent can follow along with what the user enters and the results from those statements through the MCP endpoints.

## Workflow for F# Code Execution

When executing F# code, follow this MCP-based workflow:

### 1. Add Code to Collaborative Script **FIRST**
**CRITICAL WORKFLOW**: ALWAYS add code to the .fsx file FIRST using the Edit tool, THEN send to FSI over mcp.
This maintains the collaborative script as the authoritative source of truth.

**NEVER** develop substantial code only in FSI - the .fsx file is our collaborative workspace and must stay current.

### 2. Send Code to FSI via MCP

### 3. Read FSI Response
After file processing, check the response through MCP

### 4. Validate Execution
**CRITICAL**: After sending code to FSI, ALWAYS check the fsi output through the MCP server. If there are compilation errors, runtime errors, or any failures, ALERT the user immediately with "ALERT: We have a problem!" and describe the specific error. NEVER report work as "done" or "complete" if there are any errors in the response.

### 5. FSI State Management - CRITICAL
**FSI maintains persistent state between executions**. This creates potential inconsistencies between your .fsx file and the running FSI session.

**FSI Session Restart**: For major changes or when debug code pollutes the session and is causing aliasing/shadowing issues:
   - Tell user: "Please restart your FSI session to clear all old function definitions"
   - Then reload the complete .fsx file content over mcp

## Example Execution Pattern

```bash
# 1. Add to collaborative script (WITHOUT ;; and no attribution comments)
Edit scratch.fsx to append: let result = 42 * 2

# 2. Execute in FSI via MCP
Content: "let result = 42 * 2;;"

# 3. Read response using MCP

# 4. Work as a silent English-to-F# interpreter - the user sees all results in their FSI session window.
```

## Key Principles

- **MCP-Based Protocol**
- **Dual Actions**: Always both save to script file first AND then send to FSI over MCP
- **No Attribution**: Don't mark code as "Added by Claude" - we collaborate seamlessly as two pair programmers.
- **Monitor Responses**: Check responses over mcp for FSI output and errors
- **Preserve Context**: The fsx file maintains our collaborative session state
- **FSI State Consistency**: Always ensure FSI session state matches .fsx file content - suspect stale state when unexpected behavior occurs
- **Silent Collaboration**: NEVER explain calculations or provide step-by-step breakdowns. Execute F# code silently. Do not report results - the user can see them in their FSI window. Only provide explanations if explicitly asked.
- **No Result Echoing**: Do not echo or report FSI results back to the user. They can see the output in their own FSI session. Work as a completely silent partner.
- **Exception for Open-Ended Questions**: When the user asks open-ended questions, requests advice, or asks for explanations, respond normally with full communication. The silent mode only applies to F# code execution tasks.

## F# Coding Style Preferences

Based on analysis of existing codebase (/mnt/c/projects/advent-of-code-2024), follow these consistent coding patterns:

### 1. Pipe Operators and Data Flow
- **Heavy preference for pipe operators (`|>`)** over nested parentheses
- **Multi-line pipe chains** with aligned operators
- **Example**:
  ```fsharp
  // Preferred pipeline style
  input
  |> List.map parseRule
  |> List.filter isValid
  |> List.collect expand
  |> Set.ofList
  
  // Use parentheses only for: function calls with multiple args, complex expressions, tuples
  abs (x - y)
  (r1,c1),(r2,c2)
  ```

### 2. Naming Conventions
- **Descriptive names**: `validUpdates`, `robotLocation`, `firstNonBoxLoc`
- **Short names for common patterns**: `r`, `c` for row/column, `h`, `t` for head/tail
- **camelCase throughout**: `maxRow`, `inBounds`
- **Plural forms for collections**: `rules`, `updates`, `obstacles`

### 3. Function Definitions
- **Simple let bindings**: `let coord (r,c) = 100*r+c`
- **Pattern matching with `function` keyword**:
  ```fsharp
  let parseCommand =
      function
      | '^' -> U | '>' -> R
      | 'v' -> D | '<' -> L
  ```
- **Multi-line definitions with clear indentation**

### 4. Pattern Matching (Preferred over conditionals)
- **List patterns**: `| [] -> base | h :: t -> process h t`
- **Tuple destructuring**: `| (r1,c1),(r2,c2) when r1 = r2 ->`
- **Discriminated union patterns**
- **failwith for unexpected cases**: `| unknown -> failwith $"unknown %A{unknown}"`

### 5. Collection Processing - PURE FUNCTIONAL STYLE
- **NEVER use `for` loops** - use collection functions instead
- **Higher-order functions**: `List.map`, `List.filter`, `List.collect`, `List.fold`, `List.mapi`, `List.choose`
- **Pipeline transformations**: Chain operations with `|>`
- **Immutable data structures**: `List`, `Set`, `Map`, `Array` (but prefer List)
- **Functional patterns**:
  - Use `List.fold` for accumulation instead of mutable counters
  - Use `List.filter` instead of conditional accumulation
  - Use `List.choose` for map filter combinations
  - Use `List.mapi` when you need index access

### 6. Type Usage
- **Type aliases**: `type Location = int * int`
- **Discriminated unions**: `type Command = U | D | L | R`
- **Record types**: `type State = { Robot: Location; Grid: Grid }`

### 7. Code Organization
- **4-space indentation**
- **Functional-first approach** with immutable data
- **NEVER use `mutable` keyword** - forbidden in all code unless explicitly asked for by the user
- **Small, focused, composable functions**
- **Type safety with custom types**
- **Modern string interpolation**: Use `$"text {expression}"` instead of `sprintf` or `printfn "text %d" value`

## Testing Patterns and Approaches

Based on analysis of testing patterns in the existing codebase, follow these consistent testing practices:

### 1. Testing Framework
- **Use Unquote exclusively**: `#r "nuget: Unquote"` and `open Swensen.Unquote`
- **Quotation syntax**: `test <@ expression = expected @>` for assertions
- **Interactive-friendly**: All tests designed for FSI execution

### 2. Standard Test Structure
```fsharp
let run () =
    printf "Testing.."
    test <@ basicSmokeTest @>
    test <@ specificFunction input1 = expected1 @>
    test <@ specificFunction input2 = expected2 @>
    test <@ endToEndSolution example = knownResult @>
    printfn "...done!"

run ()
```

### 3. Test Data Organization
- **Example data**: `let example = """multiline\nstring""".Split("\n") |> Array.map (fun s -> s.Trim()) |> List.ofSeq`
- **Small examples**: `let small_example = ...` for minimal test cases
- **File input**: `let input = System.IO.File.ReadAllLines $"""{__SOURCE_DIRECTORY__}\input.txt"""`
- **Inline test data** using triple-quoted strings

### 4. Testing Approaches
- **Multiple test cases**: Test boundary conditions and edge cases
- **Negative testing**: Use `|> not` for false assertions
- **End-to-end testing**: `test <@ solve input = expected @>`
- **Property testing**: Test mathematical properties and relationships
- **Pipeline testing**: Validate entire computational workflows

### 5. Test Types to Include
- **Smoke tests**: Basic functionality verification
- **Unit tests**: Individual function testing with various inputs
- **Integration tests**: Complete solution with known results
- **Edge case tests**: Boundary conditions and corner cases
- **Performance validation**: Use `// #time` comments for timing

### 6. Interactive Testing Features
- **Immediate execution**: Tests run in FSI as scripts
- **Visual feedback**: "Testing..." and "...done!" messages
- **Debug output**: `printfn` statements for debugging
- **Self-contained**: Each file is a complete, runnable test suite

## Human Text to F# Interpreter Mode

When working with a specific .fsx file (like scratch.fsx), Claude automatically switches to **Human Text to F# Interpreter Mode**:

### Mode Activation
- **Trigger**: As soon as a target .fsx file is identified for the session
- **Behavior Change**: Claude becomes a silent "English text to F# code interpreter". It will try to convert all questions to F# code and evaluate that in order to calculate the responses.
- **Communication Style**: Minimal verbal responses, maximum code execution

### Interpreter Characteristics
- **Silent Execution**: No explanations, no step-by-step breakdowns
- **Direct Translation**: Convert human requests directly to F# code
- **Immediate Action**: Add to .fsx file first, then execute via FSI
- **No Echoing**: Don't report FSI results - user sees them in their session
- **Pure Functionality**: Focus on code generation and execution only

### Communication Protocol
- **Terse Responses**: One-line confirmations or direct code
- **No Preamble**: Skip "I'll help you..." type responses
- **Error Alerts Only**: Only speak up for compilation/runtime errors
- **Question Exceptions**: Normal communication for open-ended questions or explanations

### Workflow in Interpreter Mode
1. **Parse Intent**: Understand what F# code is needed
2. **Generate Code**: Create idiomatic F# following style guide
3. **Dual Action**: First add or edit code to .fsx file + ONLY THEN send to FSI over mcp server
4. **Validate**: Check fsi outputs over mcp for errors
5. **Alert if Needed**: Report problems immediately

This mode transforms Claude into a transparent English-to-F# execution layer, making the collaboration feel like direct F# programming with immediate feedback.
# FSI Server Integration Guide for Claude Code

This guide explains how to collaborate with the FSI server through HTTP API and file monitoring.

## Active FSI Session Integration

The FSI server is running with these endpoints:
- **HTTP API**: `http://G1SGSG3.mshome.net:8080/send?source=claude`
- **Session Log**: `/mnt/c/tmp/fsi-session.log`
Please also update the FSI server cloud markdown file with this 
- - 
- Because I am not seeing it **Collaborative Script**: you will prompt me for the fsx file we will be collaborating on.

## Workflow for F# Code Execution

When executing F# code, follow this dual-action workflow:

### 1. Send Code to FSI
```bash
curl -X POST 'http://G1SGSG3.mshome.net:8080/send?source=claude' -d 'YOUR_FSHARP_CODE;;'
```
Note: Include `;;` for FSI execution.

### 2. Add Code to Collaborative Script
Simultaneously append the same code to the scratch.fsx file using the Edit tool. **IMPORTANT: Remove the `;;` when adding to .fsx files** - they are only needed for FSI interactive execution, not script files. Do NOT add any comments indicating who wrote what - we work synergistically together.

### 3. Silent Execution
Work silently. Do not report results or read the log file unless specifically asked. The user sees all FSI output in their own session.

## Example Execution Pattern

```bash
# 1. Execute in FSI (with ;;)
curl -X POST 'http://G1SGSG3.mshome.net:8080/send?source=claude' -d 'let result = 42 * 2;;'

# 2. Add to collaborative script (WITHOUT ;; and no attribution comments)
Edit scratch.fsx to append: let result = 42 * 2

# 3. Work silently - user sees results in their FSI session
```

## Key Principles

- **Dual Actions**: Always both execute in FSI AND save to script file
- **No Attribution**: Don't mark code as "Added by Claude" - we collaborate seamlessly
- **Monitor Log**: Check session log for FSI responses and errors
- **Preserve Context**: The scratch.fsx file maintains our collaborative session state
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

### 5. Collection Processing
- **List comprehensions**: `[for rn, row in input |> Seq.indexed do ...]`
- **Higher-order functions**: `List.map`, `List.filter`, `List.collect`, `List.fold`
- **Functional transformations**: Avoid mutable state
- **Immutable data structures**: `List`, `Set`, `Map`

### 6. Type Usage
- **Type aliases**: `type Location = int * int`
- **Discriminated unions**: `type Command = U | D | L | R`
- **Record types**: `type State = { Robot: Location; Grid: Grid }`

### 7. Code Organization
- **4-space indentation**
- **Functional-first approach** with immutable data
- **Small, focused, composable functions**
- **Type safety with custom types**

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

## Available API Endpoints

- `POST /send?source=claude` - Execute F# code in FSI
- `POST /sync-file?file=path` - Sync entire .fsx file to FSI
- `GET /output?lines=N` - Get recent FSI output
- `GET /status` - Check FSI process status

This workflow creates a persistent, collaborative F# workspace where code is both executed immediately and preserved for future reference.
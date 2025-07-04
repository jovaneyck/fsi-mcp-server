# FSI Server Integration Guide for Claude Code

This guide explains how to collaborate with the FSI server through HTTP API and file monitoring.

## Active FSI Session Integration

**Configuration**: Set your FSI server hostname:
```bash
FSI_HOST="http://G1SGSG3.mshome.net:8080"  # Replace with your FSI server host
```

The FSI server endpoints:
- **HTTP API**: `${FSI_HOST}/send?source=claude`
- **Session Log**: `/mnt/c/tmp/fsi-session.log`
- **Collaborative Script**: you will prompt me for the fsx file we will be collaborating on.

## Workflow for F# Code Execution

When executing F# code, follow this dual-action workflow:

### 1. Add Code to Collaborative Script **FIRST**
**CRITICAL WORKFLOW**: ALWAYS add code to the .fsx file FIRST using the Edit tool, THEN send to FSI. This maintains the collaborative script as the authoritative source.

**For major code changes** (new functions, substantial rewrites, debugging functions):
1. **FIRST**: Add to .fsx file using Edit tool
2. **THEN**: Send to FSI for testing
3. **Remove `;;`** when adding to .fsx files - only needed for FSI execution

**For minor troubleshooting** (quick tests, single expressions):
- OK to send directly to FSI without updating .fsx

**NEVER** develop substantial code only in FSI - the .fsx file is our collaborative workspace and must stay current.

### 2. Send Code to FSI
```bash
curl -X POST "${FSI_HOST}/send?source=claude" -d $'YOUR_FSHARP_CODE;;'
```

**CRITICAL PIPE HANDLING**: Always use `$'...'` syntax and escape ALL pipe characters as `\u007C` (Unicode escape) to prevent shell interpretation:
- **Pipeline operators**: `\u007C>` instead of `|>`
- **Pattern matching**: `\u007C 1 \u007C 2` instead of `| 1 | 2`
- **Function definitions**: `function \u007C '^' -> Up` instead of `function | '^' -> Up`

The shell interprets literal `|` as pipe operators, causing FSI syntax errors. Use Unicode escapes for ALL pipe characters in curl commands.


### 3. Validate Execution
**CRITICAL**: After sending code to FSI, ALWAYS check the FSI session log for errors before reporting completion. Use `tail -n 20 /mnt/c/tmp/fsi-session.log` to verify the code executed successfully. If there are compilation errors, runtime errors, or any failures, ALERT the user immediately with "ALERT: We have a problem!" and describe the specific error. NEVER report work as "done" or "complete" if there are any errors in the log.

## Example Execution Pattern

```bash
# 1. Add to collaborative script (WITHOUT ;; and no attribution comments)
Edit scratch.fsx to append: let result = 42 * 2

# 2. Execute in FSI (with ;; and Unicode escapes for pipes)
curl -X POST "${FSI_HOST}/send?source=claude" -d $'let result = 42 * 2;;'

# Pipeline operators:
curl -X POST "${FSI_HOST}/send?source=claude" -d $'[1;2;3] \u007C> List.map (fun x -> x * 2);;'

# Pattern matching:
curl -X POST "${FSI_HOST}/send?source=claude" -d $'match x with \u007C 1 \u007C 2 -> "small" \u007C _ -> "large";;'

# Multi-line function definitions:
curl -X POST "${FSI_HOST}/send?source=claude" -d $'let findGuard (grid: Grid) =
    grid
    \u007C> List.mapi (fun r row ->
        row \u007C> Seq.mapi (fun c cell -> (r, c), cell)
        \u007C> Seq.filter (fun (_, cell) -> cell <> \'.\'))
    \u007C> List.collect id \u007C> List.head;;'

# 3. Work silently - user sees results in their FSI session
```

## Key Principles

- **Dual Actions**: Always both execute in save to script file AND send to FSI
- **No Attribution**: Don't mark code as "Added by Claude" - we collaborate seamlessly
- **Monitor Log**: Check session log for FSI responses and errors
- **Preserve Context**: The fsx file maintains our collaborative session state
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

## Available API Endpoints

- `POST ${FSI_HOST}/send?source=claude` - Execute F# code in FSI
- `POST ${FSI_HOST}/sync-file?file=path` - Sync entire .fsx file to FSI
- `GET ${FSI_HOST}/output?lines=N` - Get recent FSI output
- `GET ${FSI_HOST}/status` - Check FSI process status

This workflow creates a persistent, collaborative F# workspace where code is both executed immediately and preserved for future reference.
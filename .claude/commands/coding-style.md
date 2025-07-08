
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
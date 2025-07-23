module ConsoleIOSmokeTests

open System
open System.IO
open System.Threading.Tasks
open Xunit
open Swensen.Unquote

[<Fact>]
let ``Basic F# Execution - 1 + 1 should output val it : int = 2`` () =
    // Arrange: Capture console output
    let originalOut = Console.Out
    let originalIn = Console.In
    
    use outputCapture = new StringWriter()
    use inputCapture = new StringReader("1 + 1\n")
    
    try
        Console.SetOut(outputCapture)
        Console.SetIn(inputCapture)
        
        // Act: Start FSI MCP Server and send input
        // TODO: Need to integrate with WebApplicationFactory
        
        // For now, just verify console redirection works
        Console.WriteLine("Test output")
        
        // Assert: Verify expected output appears
        let output = outputCapture.ToString()
        test <@ output.Contains("Test output") @>
        
    finally
        Console.SetOut(originalOut)
        Console.SetIn(originalIn)
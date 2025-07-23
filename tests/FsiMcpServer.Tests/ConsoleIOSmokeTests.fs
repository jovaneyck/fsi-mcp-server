module ConsoleIOSmokeTests

open System
open System.IO
open System.Threading.Tasks
open Xunit
open Swensen.Unquote

// Import the server's Program module functions
open Program
[<Fact>]
let ``Basic F# code is evaluated`` () =
    async {
        let originalOut, originalIn = Console.Out, Console.In
        use output = new StringWriter()
        Console.SetOut output

        let script = "let foo = 1 + 3;;" + Environment.NewLine
        use input  = new StringReader(script)
        Console.SetIn  input

        try
            let (app, consoleTask) = createApp [||]
            consoleTask |> ignore
            let appTask = Task.Run(fun () -> app.Run())

            let expectedOutput = "val foo: int = 4"
            let rec wait n =
                async {
                    if n = 50 then return output.ToString()
                    elif output.ToString().Contains(expectedOutput) then
                        return output.ToString()
                    else
                        do! Async.Sleep 200
                        return! wait (n + 1)
                }
            let! finalOut = wait 0
            test <@ finalOut.Contains(expectedOutput) @>

            do! app.DisposeAsync().AsTask() |> Async.AwaitTask
        finally
            Console.SetOut originalOut
            Console.SetIn  originalIn
    } |> Async.RunSynchronously

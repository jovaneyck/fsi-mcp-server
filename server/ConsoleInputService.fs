module ConsoleInputService

open System
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System.Threading
open System.Threading.Tasks

type ConsoleInputService(fsiService: FsiService.FsiService, logger: ILogger<ConsoleInputService>) =
    inherit BackgroundService()
    
    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        task {
            logger.LogInformation("💬 Console input forwarding started. Type F# commands directly:")
            
            // Forward console input to FSI
            try
                while not stoppingToken.IsCancellationRequested do
                    let! line = Console.In.ReadLineAsync(stoppingToken)
                    if not (isNull line) then
                        match fsiService.SendToFsi(line, FsiService.FsiInputSource.Console) with
                        | Ok _ -> ()
                        | Error msg -> logger.LogError($"Console input forwarding error: {msg}")
            with
            | :? OperationCanceledException -> ()
            | ex -> logger.LogError($"Console input service error: {ex.Message}")
        }
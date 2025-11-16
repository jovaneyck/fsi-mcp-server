module FileLogger

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging

type FileLogger(categoryName: string, filePath: string) =
    let lockObj = obj()

    let ensureDirectory() =
        let dir = Path.GetDirectoryName(filePath)
        if not (String.IsNullOrEmpty(dir)) && not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore

    interface ILogger with
        member _.BeginScope<'TState>(state: 'TState) = null

        member _.IsEnabled(logLevel: LogLevel) =
            logLevel >= LogLevel.Debug

        member _.Log<'TState>(logLevel: LogLevel, eventId: EventId, state: 'TState, ``exception``: exn, formatter: Func<'TState, exn, string>) =
            if logLevel >= LogLevel.Debug then
                lock lockObj (fun () ->
                    try
                        ensureDirectory()
                        let timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                        let message = formatter.Invoke(state, ``exception``)
                        let logLine = $"[{timestamp}] [{categoryName}] {message}"
                        File.AppendAllText(filePath, logLine + Environment.NewLine)
                    with
                    | ex ->
                        Console.Error.WriteLine($"FILE LOGGER FAILED: {ex.Message}")
                )

type FileLoggerProvider(filePath: string) =
    interface ILoggerProvider with
        member _.CreateLogger(categoryName: string) =
            FileLogger(categoryName, filePath) :> ILogger

        member _.Dispose() = ()

[<Extension>]
type FileLoggerExtensions =
    [<Extension>]
    static member AddFileLogger(builder: ILoggingBuilder, filePath: string) =
        builder.AddProvider(new FileLoggerProvider(filePath))
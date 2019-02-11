namespace MsbuildUtilityHelpers

open System
open System.IO
open System.Threading
open System.Diagnostics
open Microsoft.FSharp.Collections
open Microsoft.Build.Utilities
open System.Runtime.InteropServices

type ReturnCode =
   | Ok = 0
   | Timeout = 1
   | NokAppSpecific = 2

[<AllowNullLiteral>]
type ICommandExecutor = 
  abstract member GetStdOut : list<string>
  abstract member GetStdError : list<string>
  abstract member GetErrorCode : ReturnCode
  abstract member CancelExecution : ReturnCode
  abstract member ResetData : unit -> unit
  abstract member GetProcessIdsRunning : string -> Process []


  // no redirection of output
  abstract member ExecuteCommand : string * string * Map<string, string> * string -> int
  abstract member ExecuteCommandWait : string * string * Map<string, string> * string -> int

  // with redirection of output
  abstract member ExecuteCommand : string * string * Map<string, string> * (DataReceivedEventArgs -> unit) * (DataReceivedEventArgs -> unit) * string -> int


type CommandExecutor(logger : TaskLoggingHelper, timeout : int64) =
    let addEnvironmentVariable (startInfo:ProcessStartInfo) a b =
        if not(startInfo.EnvironmentVariables.ContainsKey(a)) then
            startInfo.EnvironmentVariables.Add(a, b)

    member val Logger = logger
    member val proc : Process  = new Process() with get, set
    member val output : string list = [] with get, set
    member val error : string list = [] with get, set
    member val returncode : ReturnCode = ReturnCode.Ok with get, set
    member val cancelSignal : bool = false with get, set

    member val Program : string = "" with get, set

    member this.ProcessErrorDataReceived(e : DataReceivedEventArgs) =
        if not(String.IsNullOrWhiteSpace(e.Data)) then
            this.error <- this.error @ [e.Data]
            System.Diagnostics.Debug.WriteLine("ERROR:" + e.Data)
        ()

    member this.ProcessOutputDataReceived(e : DataReceivedEventArgs) =
        if not(String.IsNullOrWhiteSpace(e.Data)) then
            this.output <- this.output @ [e.Data]
            System.Diagnostics.Debug.WriteLine(e.Data)
        ()

    interface ICommandExecutor with

        member this.GetProcessIdsRunning(processName : string) =
            System.Diagnostics.Process.GetProcessesByName(processName)


            
        member this.GetStdOut =
            this.output

        member this.GetStdError =
            this.error

        member this.GetErrorCode =
            this.returncode

        member this.CancelExecution =
            if this.proc.HasExited = false then
                this.proc.Kill()
            this.cancelSignal <- true
            ReturnCode.Ok

        member this.ResetData() =
            this.error <- []
            this.output <- []
            ()

        member this.ExecuteCommand(program, args, env, wd) =
            this.Program <- program
            let startInfo = ProcessStartInfo(FileName = program,
                                             Arguments = args,
                                             WindowStyle = ProcessWindowStyle.Normal,
                                             UseShellExecute = false,
                                             RedirectStandardOutput = true,
                                             RedirectStandardError = true,
                                             RedirectStandardInput = true,
                                             CreateNoWindow = true,
                                             WorkingDirectory = wd)
            env |> Map.iter (addEnvironmentVariable startInfo)

            this.proc <- new Process(StartInfo = startInfo)
            this.proc.ErrorDataReceived.Add(this.ProcessErrorDataReceived)
            this.proc.OutputDataReceived.Add(this.ProcessOutputDataReceived)

            this.proc.EnableRaisingEvents <- true
            let ret = this.proc.Start()

            this.proc.BeginOutputReadLine()
            this.proc.BeginErrorReadLine()
            this.proc.WaitForExit()
            this.cancelSignal <- true
            this.proc.ExitCode


        member this.ExecuteCommand(program, args, env, outputHandler, errorHandler, workingDir) =        
            this.Program <- program       
            let startInfo = ProcessStartInfo(FileName = program,
                                             Arguments = args,
                                             WindowStyle = ProcessWindowStyle.Normal,
                                             UseShellExecute = false,
                                             RedirectStandardOutput = true,
                                             RedirectStandardError = true,
                                             RedirectStandardInput = true,
                                             CreateNoWindow = true,
                                             WorkingDirectory = workingDir)
            env |> Map.iter (addEnvironmentVariable startInfo)

            this.proc <- new Process(StartInfo = startInfo,
                                     EnableRaisingEvents = true)
            this.proc.OutputDataReceived.Add(outputHandler)
            this.proc.ErrorDataReceived.Add(errorHandler)
            let ret = this.proc.Start()

            this.proc.BeginOutputReadLine()
            this.proc.BeginErrorReadLine()
            this.proc.WaitForExit()
            this.cancelSignal <- true
            this.proc.ExitCode

        member this.ExecuteCommandWait(program, args, env, wd) =
            this.Program <- program
            let startInfo = ProcessStartInfo(FileName = program,
                                             Arguments = args,
                                             CreateNoWindow = true,
                                             WindowStyle = ProcessWindowStyle.Hidden,
                                             WorkingDirectory = wd)

            env |> Map.iter (addEnvironmentVariable startInfo)

            this.proc <- new Process(StartInfo = startInfo)
            let ret = this.proc.Start()
            this.proc.WaitForExit()
            this.cancelSignal <- true
            this.proc.ExitCode



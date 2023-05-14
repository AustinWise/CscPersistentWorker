using Blaze.Worker;
using Google.Protobuf;
using System.Diagnostics;

namespace PersistentWorker;

public class PersistentWorker<T> where T : IPersistentWorkerCustomization
{
    const string PERSISTENT_WORKER_FLAG = "--persistent_worker";

    private readonly string _executable;
    private readonly string _compiler;

    private readonly Stream _stdIn;
    private readonly Stream _stdOut;

    static int RunStandaloneCompile(List<string> args)
    {
        if (args.Count < 2)
        {
            throw new Exception("Expected at least two arguments.");
        }

        var psi = new ProcessStartInfo(args[0]);
        for (int i = 1; i < args.Count; i++)
        {
            psi.ArgumentList.Add(args[i]);
        }
        foreach (string arg in T.GetExtraArguments(args[1]))
        {
            psi.ArgumentList.Add(arg);
        }

        var p = Process.Start(psi);
        if (p == null)
            throw new Exception("Process.Start returned null for " + args[0]);
        p.WaitForExit();
        return p.ExitCode;
    }

    public static int RunMain(string[] args)
    {
        bool isPersistentWorker = false;
        List<string> interestingArgs = new List<string>();
        foreach (var arg in args)
        {
            if (arg == PERSISTENT_WORKER_FLAG)
            {
                isPersistentWorker = true;
            }
            else
            {
                interestingArgs.Add(arg);
            }
        }

        if (isPersistentWorker)
        {
            var worker = new PersistentWorker<T>(interestingArgs);
            worker.Run();
            return 0;
        }
        else
        {
            return RunStandaloneCompile(interestingArgs);
        }
    }

    public PersistentWorker(List<string> args)
    {
        if (args.Count != 2)
            throw new Exception("Unexpected argument count");
        _executable = args[0];
        _compiler = args[1];
        _stdIn = Console.OpenStandardInput();
        _stdOut = Console.OpenStandardOutput();
    }

    public void Run()
    {
        while (true)
        {
            var request = WorkRequest.Parser.ParseDelimitedFrom(_stdIn);

            if (request.Cancel)
            {
                //TODO: implement cancellation
                continue;
            }

            if (!string.IsNullOrEmpty(request.SandboxDir))
            {
                var response = new WorkResponse();
                response.RequestId = request.RequestId;
                response.ExitCode = 1;
                response.Output = "supports-multiplex-sandboxing not implemented";

                lock (this)
                {
                    response.WriteDelimitedTo(_stdOut);
                }
                continue;
            }

            Task.Run(async () =>
            {
                WorkResponse response;
                try
                {
                    response = await RunRequestAsync(request);
                }
                catch (Exception ex)
                {
                    response = new WorkResponse();
                    response.RequestId = request.RequestId;
                    response.ExitCode = 1;
                    response.Output = ex.ToString();
                }

                lock (this)
                {
                    response.WriteDelimitedTo(_stdOut);
                }
            });
        }
    }

    private async Task<WorkResponse> RunRequestAsync(WorkRequest request)
    {
        var psi = new ProcessStartInfo(_executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        psi.ArgumentList.Add(_compiler);
        foreach (var arg in request.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        foreach (string arg in T.GetExtraArguments(_compiler))
        {
            psi.ArgumentList.Add(arg);
        }

        var p = Process.Start(psi);
        if (p == null)
        {
            throw new Exception("Process.Start returned null for " + _executable);
        }

        p.StandardInput.Close();
        var stdOutTask = p.StandardOutput.ReadToEndAsync();
        var stdErrTask = p.StandardError.ReadToEndAsync();

        string[] outputs = await Task.WhenAll(stdOutTask, stdErrTask);

        await p.WaitForExitAsync();

        var response = new WorkResponse();
        response.RequestId = request.RequestId;
        response.ExitCode = p.ExitCode;
        response.Output = outputs[0] + (outputs[1].Length == 0 ? string.Empty : ("\n" + outputs[1]));
        return response;
    }
}

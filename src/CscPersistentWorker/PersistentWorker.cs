using Blaze.Worker;
using Google.Protobuf;
using System.Diagnostics;

namespace CscPersistentWorker;

internal class PersistentWorker
{
    private readonly string _executable;
    private readonly string _compiler;

    private readonly Stream _stdIn;
    private readonly Stream _stdOut;

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
        psi.ArgumentList.Add(Program.CreatePathMapArg(Environment.CurrentDirectory));

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

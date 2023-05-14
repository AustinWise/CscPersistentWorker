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

            Task.Run(() => RunRequestAsync(request));
        }
    }

    private async Task RunRequestAsync(WorkRequest request)
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
        psi.ArgumentList.Add(Program.CreatePathMapArg(request.SandboxDir ?? Environment.CurrentDirectory));

        var p = Process.Start(psi);
        if (p == null)
            throw new Exception("Process.Start returned null for " + _executable);

        p.StandardInput.Close();
        var stdOut = await p.StandardOutput.ReadToEndAsync();
        var stdErr = await p.StandardError.ReadToEndAsync();

        await p.WaitForExitAsync();

        var response = new WorkResponse();
        response.RequestId = request.RequestId;
        response.ExitCode = p.ExitCode;
        response.Output = stdOut + (stdErr.Length == 0 ? string.Empty : ("\n" + stdErr));

        lock (this)
        {
            response.WriteDelimitedTo(_stdOut);
        }
    }
}

using System.Diagnostics;

namespace CscPersistentWorker;

internal class Program
{
    static async Task<int> RunStandaloneCompile(List<string> args)
    {
        if (args.Count < 2)
        {
            throw new Exception("Expected at least two arguments.");
        }

        string pathMapArg = "-pathmap:";
        // Needed because unfortunately the F# compiler uses a different flag name
        if (args[1].EndsWith("fsc.dll"))
        {
            pathMapArg = "--pathmap";
        }
        pathMapArg = $"{pathMapArg}{Environment.CurrentDirectory}=.";

        args.Add(pathMapArg);

        var psi = new ProcessStartInfo(args[0])
        {
        };

        for (int i = 1;i < args.Count; i++)
        {
            psi.ArgumentList.Add(args[i]);
        }

        var p = Process.Start(psi);
        if (p == null)
            throw new Exception("Process.Start returned null for " + args[0]);
        await p.WaitForExitAsync();
        return p.ExitCode;
    }

    static async Task<int> Main(string[] args)
    {
        bool isPersistentWorker = false;
        List<string> interestingArgs = new List<string>();
        foreach (var arg in args)
        {
            if (arg == "--persistent_worker")
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
            throw new NotImplementedException();
        }
        else
        {
            return await RunStandaloneCompile(interestingArgs);
        }
    }
}
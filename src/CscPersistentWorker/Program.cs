using System.Diagnostics;

namespace CscPersistentWorker;

internal class Program
{
    public static string CreatePathMapArg(string compilerArg)
    {
        return CreatePathMapArg(compilerArg, Environment.CurrentDirectory);
    }

    public static string CreatePathMapArg(string compilerArg, string sandboxDir)
    {
        string pathMapArg = "-pathmap";
        // Needed because unfortunately the F# compiler uses a different flag name
        if (compilerArg.EndsWith("fsc.dll"))
        {
            pathMapArg = "--pathmap";
        }
        pathMapArg = $"{pathMapArg}:{sandboxDir}=.";
        return pathMapArg;
    }

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
        psi.ArgumentList.Add(CreatePathMapArg(args[1]));

        var p = Process.Start(psi);
        if (p == null)
            throw new Exception("Process.Start returned null for " + args[0]);
        p.WaitForExit();
        return p.ExitCode;
    }

    static int Main(string[] args)
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
            var worker = new PersistentWorker(interestingArgs);
            worker.Run();
            return 0;
        }
        else
        {
            return RunStandaloneCompile(interestingArgs);
        }
    }
}
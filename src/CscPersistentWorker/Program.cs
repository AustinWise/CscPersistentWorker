using PersistentWorker;

namespace CscPersistentWorker;

internal class Program
{
    static int Main(string[] args)
    {
        return PersistentWorker<CscCustomization>.RunMain(args);
    }

    static string CreatePathMapArg(string compilerArg)
    {
        return CreatePathMapArg(compilerArg, Environment.CurrentDirectory);
    }

    static string CreatePathMapArg(string compilerArg, string sandboxDir)
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

    private class CscCustomization : IPersistentWorkerCustomization
    {
        private CscCustomization()
        {
            // not intended to be instantiated
        }

        public static IEnumerable<string> GetExtraArguments(string dotnetProgramDllPath)
        {
            return new string[]
            {
                CreatePathMapArg(dotnetProgramDllPath),
            };
        }
    }
}
namespace PersistentWorker;

public interface IPersistentWorkerCustomization
{
    static abstract IEnumerable<string> GetExtraArguments(string dotnetProgramDllPath);
}

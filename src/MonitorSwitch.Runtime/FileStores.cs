using System.Text.Json;
using MonitorSwitch.Core;

namespace MonitorSwitch.Runtime;

/// <summary>state.json: the single authoritative recovery document.</summary>
public sealed class JsonStateStore(string path) : IOperationStore
{
    public OperationState? Read()
    {
        if (!File.Exists(path)) return null;
        OperationState? state;
        try { state = JsonSerializer.Deserialize<OperationState>(File.ReadAllBytes(path), StrictJson.Options); }
        catch (JsonException error) { throw new StateStoreException("state.json is corrupt.", error); }
        catch (IOException error) { throw new StateStoreException("state.json could not be read.", error); }
        if (state is null || state.SchemaVersion != OperationState.CurrentSchema)
            throw new StateStoreException("state.json has an unsupported schema.");
        return state;
    }

    public void ReplaceAtomically(OperationState state) =>
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(state, StrictJson.Options));
}

public sealed class FileConfigStore(string path, string backupPath) : IConfigStore
{
    public string? ReadText() => File.Exists(path) ? File.ReadAllText(path) : null;

    public void ReplaceAtomically(AppConfig config)
    {
        if (File.Exists(path)) File.Copy(path, backupPath, overwrite: true);
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(config, StrictJson.Options));
    }
}

/// <summary>
/// Exclusive, nonblocking lock on a file in the per-user state directory. Unlike a Local\ mutex it covers
/// every session that shares the directory, is released by the OS if the process dies, and has no thread affinity.
/// </summary>
public sealed class FileOperationLock(string path) : IOperationLock
{
    public IDisposable? TryAcquire()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
    }
}

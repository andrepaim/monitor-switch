namespace MonitorSwitch.Runtime;

public static class AtomicFile
{
    /// <summary>
    /// Writes a same-directory temporary file, flushes it to disk, then replaces the destination by rename
    /// (MoveFileEx with replace-existing on Windows). Readers see the old or the new document, never a mix.
    /// </summary>
    public static void Write(string path, byte[] content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

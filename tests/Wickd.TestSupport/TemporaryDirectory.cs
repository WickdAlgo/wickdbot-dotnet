namespace Wickd.Tests;

/// <summary>
/// Owns a temporary test directory and removes it when the test completes.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// Initializes a new temporary directory.
    /// </summary>
    internal TemporaryDirectory()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "Wickd.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>
    /// Gets the full path to the temporary directory.
    /// </summary>
    internal string DirectoryPath { get; }

    /// <summary>
    /// Writes a UTF-8 text file inside the temporary directory.
    /// </summary>
    /// <param name="fileName">File name relative to the temporary directory.</param>
    /// <param name="contents">File contents to write.</param>
    /// <returns>The full path to the written file.</returns>
    internal string WriteFile(string fileName, string contents)
    {
        var path = Path.Combine(DirectoryPath, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    /// <summary>
    /// Deletes the temporary directory and all files created inside it.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

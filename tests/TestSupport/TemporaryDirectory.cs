namespace MarstekOverlay.TestSupport;

public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MarstekOverlay.Tests", Guid.NewGuid().ToString("N"));

    public string GetFilePath(string fileName) => System.IO.Path.Combine(Path, fileName);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

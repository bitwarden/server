namespace Bit.Core.Test;

public class TempDirectory : IDisposable
{
    public string Directory { get; private set; }

    public TempDirectory()
    {
        Directory = Path.Combine(Path.GetTempPath(), $"bitwarden_{Guid.NewGuid().ToString().Replace("-", "")}");
    }

    public override string ToString() => Directory;

    #region IDisposable implementation
    ~TempDirectory()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                System.IO.Directory.Delete(Directory, true);
            }
            catch { }
        }
    }
    # endregion
}

using Jellyfin.Plugin.Audience;
using Xunit;

public sealed class LedgerStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Environment.CurrentDirectory, ".ledger-tests", Guid.NewGuid().ToString("N"));
    private string LedgerPath => Path.Combine(directory, "tagged-ids.txt");

    [Fact]
    public void MissingFileLoadsEmpty() => Assert.Empty(LedgerStore.Load(directory));

    [Fact]
    public void SaveAndLoadPreserveSetAndSupportAddingIds()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        LedgerStore.Save(directory, [first]);
        var loaded = LedgerStore.Load(directory);
        Assert.Contains(first, loaded);
        Assert.DoesNotContain(second, loaded);
        Assert.False(loaded.Add(first));
        Assert.True(loaded.Add(second));
        LedgerStore.Save(directory, loaded);
        Assert.True(LedgerStore.Load(directory).SetEquals([first, second]));
        Assert.False(File.Exists(LedgerPath + ".tmp"));
    }

    [Fact]
    public void EmptyFileLoadsEmpty()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(LedgerPath, "\n  \n");
        Assert.Empty(LedgerStore.Load(directory));
    }

    [Fact]
    public void CorruptLinesAreIgnoredWhileValidIdsAreRetained()
    {
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        File.WriteAllText(LedgerPath, $"not-a-guid\n  {id}  \n{id:N}\n");
        Assert.True(LedgerStore.Load(directory).SetEquals([id]));
    }

    [Fact]
    public void FailedTemporaryWriteLeavesExistingLedgerIntact()
    {
        var id = Guid.NewGuid();
        LedgerStore.Save(directory, [id]);
        Directory.CreateDirectory(LedgerPath + ".tmp");
        var error = Record.Exception(() => LedgerStore.Save(directory, [Guid.NewGuid()]));
        Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected exception: {error}");
        Assert.True(LedgerStore.Load(directory).SetEquals([id]));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

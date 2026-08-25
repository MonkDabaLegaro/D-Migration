using DMigration.Domain;
using DMigration.Infrastructure.Windows;
using Xunit;

namespace DMigration.Tests;

public sealed class KnownFolderMigrationProviderTests
{
    [Fact]
    public async Task Preflight_RejectsCloudManagedKnownFolder()
    {
        var rule = Rule("documents", KnownFolderIds.Documents);
        var host = new FakeKnownFolderHost();
        host.Paths[KnownFolderIds.Documents] = @"C:\Users\me\OneDrive\Documents";
        host.Directories[@"C:\Users\me\OneDrive\Documents"] = ["report.docx"];
        var provider = new KnownFolderMigrationProvider(host, [rule]);
        var step = Step("documents", @"C:\Users\me\OneDrive\Documents", @"D:\Documentos");

        var result = await provider.PreflightAsync(step);

        Assert.False(result.Success);
        Assert.Contains("OneDrive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StageSwitchValidateCommit_PreservesSourceUntilValidated()
    {
        var rule = Rule("documents", KnownFolderIds.Documents);
        var host = new FakeKnownFolderHost();
        host.Paths[KnownFolderIds.Documents] = @"C:\Users\me\Documents";
        host.Directories[@"C:\Users\me\Documents"] = ["report.docx", "notes.txt"];
        var provider = new KnownFolderMigrationProvider(host, [rule]);
        var step = Step("documents", @"C:\Users\me\Documents", @"D:\Documentos");

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.True(host.Directories.ContainsKey(step.Item.SourcePath));
        Assert.True(host.Directories.ContainsKey(step.Destination!));

        Assert.True((await provider.SwitchAsync(step)).Success);
        Assert.Equal(step.Destination, host.Paths[KnownFolderIds.Documents]);

        Assert.True((await provider.ValidateAsync(step)).Success);
        Assert.True(host.Directories.ContainsKey(step.Item.SourcePath));

        Assert.True((await provider.CommitAsync(step)).Success);
        Assert.False(host.Directories.ContainsKey(step.Item.SourcePath));
        Assert.True(host.Directories.ContainsKey(step.Destination!));
    }

    private static KnownFolderRule Rule(string id, Guid folderId) => new(id, folderId);

    private static MigrationStep Step(string id, string source, string destination)
    {
        var item = new InventoryItem(
            id,
            "windows-known-folders",
            id,
            source,
            1024,
            id,
            RiskLevel.Low,
            MigrationStrategy.KnownFolderRedirect,
            destination,
            true,
            "test");
        return new MigrationStep("step", item, destination);
    }

    private sealed class FakeKnownFolderHost : IKnownFolderMigrationHost
    {
        public Dictionary<Guid, string> Paths { get; } = [];
        public Dictionary<string, HashSet<string>> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string path) => Directories.ContainsKey(path);
        public long GetAvailableBytes(string path) => long.MaxValue;
        public string? GetKnownFolderPath(Guid folderId) => Paths.GetValueOrDefault(folderId);
        public bool IsCloudManaged(string path) => path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase);
        public void SetKnownFolderPath(Guid folderId, string path) => Paths[folderId] = path;

        public void CopyDirectory(string source, string destination) =>
            Directories[destination] = new HashSet<string>(Directories[source], StringComparer.OrdinalIgnoreCase);

        public bool DirectoryContentsMatch(string source, string destination) =>
            Directories.TryGetValue(source, out var left) &&
            Directories.TryGetValue(destination, out var right) &&
            left.SetEquals(right);

        public void DeleteDirectory(string path) => Directories.Remove(path);
    }
}

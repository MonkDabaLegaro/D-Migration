using DMigration.Domain;
using DMigration.Providers;
using Xunit;

namespace DMigration.Tests;

public sealed class ConfiguredDirectoryMigrationProviderTests
{
    [Fact]
    public async Task StageSwitchValidateCommit_PreservesSourceUntilValidationThenDeletesIt()
    {
        var host = new FakeHost();
        host.Directories[@"C:\cache"] = new Dictionary<string, long> { ["a.bin"] = 10, ["sub/b.bin"] = 20 };
        host.UserEnvironment["NPM_CONFIG_CACHE"] = @"C:\cache";
        var provider = new ConfiguredDirectoryMigrationProvider(
            host,
            [new ConfiguredDirectoryRule("npm-cache", "NPM_CONFIG_CACHE")]);
        var item = Item("npm-cache", @"C:\cache", @"D:\Librerias\Node\npm-cache");
        var step = new MigrationStep("step", item, item.RecommendedDestination);

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.True(host.Directories.ContainsKey(@"C:\cache"));
        Assert.True(host.Directories.ContainsKey(@"D:\Librerias\Node\npm-cache"));

        Assert.True((await provider.SwitchAsync(step)).Success);
        Assert.Equal(@"D:\Librerias\Node\npm-cache", host.UserEnvironment["NPM_CONFIG_CACHE"]);

        Assert.True((await provider.ValidateAsync(step)).Success);
        Assert.True(host.Directories.ContainsKey(@"C:\cache"));

        Assert.True((await provider.CommitAsync(step)).Success);
        Assert.False(host.Directories.ContainsKey(@"C:\cache"));
        Assert.True(host.Directories.ContainsKey(@"D:\Librerias\Node\npm-cache"));
    }

    [Fact]
    public async Task Rollback_RestoresOriginalConfigurationAndSource()
    {
        var host = new FakeHost();
        host.Directories[@"C:\Users\me\.ollama\models"] = new Dictionary<string, long> { ["model.bin"] = 100 };
        host.UserEnvironment["OLLAMA_MODELS"] = @"C:\Users\me\.ollama\models";
        var provider = new ConfiguredDirectoryMigrationProvider(
            host,
            [new ConfiguredDirectoryRule("ollama-models", "OLLAMA_MODELS")]);
        var item = Item("ollama-models", @"C:\Users\me\.ollama\models", @"D:\Datos\AI\Ollama");
        var step = new MigrationStep("step", item, item.RecommendedDestination);

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.True((await provider.SwitchAsync(step)).Success);
        host.DeleteDirectory(item.SourcePath);

        var result = await provider.RollbackAsync(step);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Users\me\.ollama\models", host.UserEnvironment["OLLAMA_MODELS"]);
        Assert.True(host.Directories.ContainsKey(@"C:\Users\me\.ollama\models"));
        Assert.True(host.Directories.ContainsKey(@"D:\Datos\AI\Ollama"));
    }

    [Fact]
    public void WindowsHost_DirectoryContentsMatch_RejectsSameLengthDifferentContent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dmigration-hash-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        try
        {
            File.WriteAllText(Path.Combine(source, "payload.bin"), "AAAA");
            File.WriteAllText(Path.Combine(destination, "payload.bin"), "BBBB");

            var host = new WindowsConfiguredDirectoryMigrationHost();

            Assert.False(host.DirectoryContentsMatch(source, destination));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static InventoryItem Item(string id, string source, string destination) => new(
        id,
        "developer-tools",
        id,
        source,
        30,
        "node",
        RiskLevel.Low,
        MigrationStrategy.ConfigurationChange,
        destination,
        true,
        "test");

    private sealed class FakeHost : IConfiguredDirectoryMigrationHost
    {
        public Dictionary<string, Dictionary<string, long>> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> UserEnvironment { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string path) => Directories.ContainsKey(path);
        public long GetAvailableBytes(string path) => long.MaxValue;
        public bool IsAnyProcessRunning(IReadOnlyCollection<string> processNames) => false;
        public string? GetUserEnvironmentVariable(string name) => UserEnvironment.GetValueOrDefault(name);
        public void SetUserEnvironmentVariable(string name, string? value) => UserEnvironment[name] = value;

        public void CopyDirectory(string source, string destination)
        {
            Directories[destination] = new Dictionary<string, long>(Directories[source], StringComparer.OrdinalIgnoreCase);
        }

        public bool DirectoryContentsMatch(string source, string destination) =>
            Directories.TryGetValue(source, out var left) &&
            Directories.TryGetValue(destination, out var right) &&
            left.Count == right.Count && left.All(x => right.TryGetValue(x.Key, out var size) && size == x.Value);

        public void DeleteDirectory(string path) => Directories.Remove(path);
    }
}

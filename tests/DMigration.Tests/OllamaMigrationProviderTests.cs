using DMigration.Domain;
using DMigration.Providers;
using Xunit;

namespace DMigration.Tests;

public sealed class OllamaMigrationProviderTests
{
    [Fact]
    public async Task ValidateAsync_RequiresDestinationRuntimeToExposeSameModelDigests()
    {
        var host = new FakeOllamaHost
        {
            RuntimeAvailable = true,
            AvailableBytes = long.MaxValue,
            SourceProbe = Probe(("qwen3:latest", "sha256:aaa")),
            DestinationProbe = Probe(("qwen3:latest", "sha256:bbb"))
        };
        host.Directories.Add(@"C:\Users\me\.ollama\models");

        var provider = new OllamaMigrationProvider(host);
        var step = Step();

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.True((await provider.SwitchAsync(step)).Success);

        var validation = await provider.ValidateAsync(step);

        Assert.False(validation.Success);
        Assert.Contains("digest", validation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(step.Item.SourcePath, host.Directories);
    }

    [Fact]
    public async Task FullLifecycle_DeletesSourceOnlyAfterRuntimeValidation()
    {
        var models = Probe(("qwen3:latest", "sha256:aaa"), ("gemma3:4b", "sha256:bbb"));
        var host = new FakeOllamaHost
        {
            RuntimeAvailable = true,
            AvailableBytes = long.MaxValue,
            SourceProbe = models,
            DestinationProbe = models
        };
        host.Directories.Add(@"C:\Users\me\.ollama\models");

        var provider = new OllamaMigrationProvider(host);
        var step = Step();

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.Contains(step.Item.SourcePath, host.Directories);

        Assert.True((await provider.SwitchAsync(step)).Success);
        Assert.Equal(step.Destination, host.UserEnvironment["OLLAMA_MODELS"]);
        Assert.Contains(step.Item.SourcePath, host.Directories);

        Assert.True((await provider.ValidateAsync(step)).Success);
        Assert.Contains(step.Item.SourcePath, host.Directories);

        Assert.True((await provider.CommitAsync(step)).Success);
        Assert.DoesNotContain(step.Item.SourcePath, host.Directories);
        Assert.Contains(step.Destination!, host.Directories);
    }

    [Fact]
    public async Task PreflightAsync_RejectsRunningOllama()
    {
        var host = new FakeOllamaHost
        {
            RuntimeAvailable = true,
            OllamaRunning = true,
            AvailableBytes = long.MaxValue,
            SourceProbe = Probe(("qwen3:latest", "sha256:aaa"))
        };
        host.Directories.Add(@"C:\Users\me\.ollama\models");

        var result = await new OllamaMigrationProvider(host).PreflightAsync(Step());

        Assert.False(result.Success);
        Assert.Contains("cerr", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(host.ProbedPaths);
    }

    private static MigrationStep Step()
    {
        var item = new InventoryItem(
            "ollama-models",
            "developer-tools",
            "Ollama models",
            @"C:\Users\me\.ollama\models",
            100,
            "ai",
            RiskLevel.Medium,
            MigrationStrategy.ConfigurationChange,
            @"D:\Datos\AI\Ollama",
            true,
            "test");
        return new MigrationStep("ollama-step", item, item.RecommendedDestination);
    }

    private static OllamaRuntimeProbe Probe(params (string Name, string Digest)[] models) =>
        new(true, models.Select(x => new OllamaModelIdentity(x.Name, x.Digest)).ToArray(), "ok");

    private sealed class FakeOllamaHost : IOllamaMigrationHost
    {
        public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> UserEnvironment { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ProbedPaths { get; } = [];
        public bool RuntimeAvailable { get; init; }
        public bool OllamaRunning { get; init; }
        public long AvailableBytes { get; init; }
        public OllamaRuntimeProbe SourceProbe { get; init; } = new(false, [], "not configured");
        public OllamaRuntimeProbe DestinationProbe { get; init; } = new(false, [], "not configured");

        public bool DirectoryExists(string path) => Directories.Contains(path);
        public long GetAvailableBytes(string path) => AvailableBytes;
        public bool IsRuntimeAvailable() => RuntimeAvailable;
        public bool IsOllamaRunning() => OllamaRunning;
        public string? GetUserEnvironmentVariable(string name) => UserEnvironment.GetValueOrDefault(name);
        public void SetUserEnvironmentVariable(string name, string? value) => UserEnvironment[name] = value;

        public void CopyDirectory(string source, string destination)
        {
            if (!Directories.Contains(source)) throw new DirectoryNotFoundException(source);
            Directories.Add(destination);
        }

        public bool DirectoryContentsMatch(string source, string destination) =>
            Directories.Contains(source) && Directories.Contains(destination);

        public void DeleteDirectory(string path) => Directories.Remove(path);

        public Task<OllamaRuntimeProbe> ProbeAsync(string modelsPath, CancellationToken cancellationToken = default)
        {
            ProbedPaths.Add(modelsPath);
            return Task.FromResult(modelsPath.StartsWith("D:", StringComparison.OrdinalIgnoreCase)
                ? DestinationProbe
                : SourceProbe);
        }
    }
}

using DMigration.Domain;
using DMigration.Providers;
using Xunit;

namespace DMigration.Tests;

public sealed class DockerDesktopMigrationProviderTests
{
    [Fact]
    public async Task Inventory_MarksDockerManual_WhenRelocationIsNotAutomatable()
    {
        var host = new FakeDockerHost
        {
            Installed = true,
            Backend = DockerDesktopBackend.Wsl2,
            DataRoot = @"C:\Users\me\AppData\Local\Docker\wsl",
            SupportsAutomaticRelocation = false
        };

        var items = await new DockerDesktopInventoryProvider(host, "D:").ScanAsync();
        var docker = Assert.Single(items);

        Assert.False(docker.CanExecute);
        Assert.Contains("asist", docker.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullLifecycle_PreservesSourceUntilRuntimeInventoryMatches()
    {
        var original = new DockerDesktopInventory(
            ["sha256:image-a", "sha256:image-b"],
            ["volume-a", "volume-b"]);
        var host = new FakeDockerHost
        {
            Installed = true,
            Backend = DockerDesktopBackend.Wsl2,
            DataRoot = @"C:\Users\me\AppData\Local\Docker\wsl",
            SupportsAutomaticRelocation = true,
            DesktopRunning = false,
            OriginalInventory = original,
            DestinationInventory = original
        };
        host.Paths.Add(host.DataRoot);

        var provider = new DockerDesktopMigrationProvider(host);
        var step = Step();

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.Contains(host.DataRoot, host.Paths);

        Assert.True((await provider.SwitchAsync(step)).Success);
        Assert.Contains(host.DataRoot, host.Paths);

        Assert.True((await provider.ValidateAsync(step)).Success);
        Assert.Contains(host.DataRoot, host.Paths);

        Assert.True((await provider.CommitAsync(step)).Success);
        Assert.DoesNotContain(host.DataRoot, host.Paths);
        Assert.Contains(step.Destination!, host.Paths);
    }

    [Fact]
    public async Task ValidateAsync_RejectsMissingVolumes_AndKeepsSource()
    {
        var host = new FakeDockerHost
        {
            Installed = true,
            Backend = DockerDesktopBackend.Wsl2,
            DataRoot = @"C:\Users\me\AppData\Local\Docker\wsl",
            SupportsAutomaticRelocation = true,
            DesktopRunning = false,
            OriginalInventory = new DockerDesktopInventory(["sha256:image-a"], ["volume-a", "volume-b"]),
            DestinationInventory = new DockerDesktopInventory(["sha256:image-a"], ["volume-a"])
        };
        host.Paths.Add(host.DataRoot);

        var provider = new DockerDesktopMigrationProvider(host);
        var step = Step();

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.True((await provider.SwitchAsync(step)).Success);

        var validation = await provider.ValidateAsync(step);

        Assert.False(validation.Success);
        Assert.Contains("inventario", validation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(host.DataRoot, host.Paths);
    }

    private static MigrationStep Step()
    {
        var item = new InventoryItem(
            "docker-desktop-wsl2",
            "docker-desktop",
            "Docker Desktop WSL2 data",
            @"C:\Users\me\AppData\Local\Docker\wsl",
            100,
            "docker",
            RiskLevel.High,
            MigrationStrategy.DataRootMigration,
            @"D:\Datos\Docker\wsl",
            true,
            "test");
        return new MigrationStep("docker-step", item, item.RecommendedDestination);
    }

    private sealed class FakeDockerHost : IDockerDesktopMigrationHost
    {
        public bool Installed { get; init; }
        public DockerDesktopBackend Backend { get; init; }
        public string DataRoot { get; init; } = string.Empty;
        public bool SupportsAutomaticRelocation { get; init; }
        public bool DesktopRunning { get; set; }
        public DockerDesktopInventory OriginalInventory { get; init; } = new([], []);
        public DockerDesktopInventory DestinationInventory { get; init; } = new([], []);
        public HashSet<string> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Calls { get; } = [];

        public bool IsInstalled() => Installed;
        public DockerDesktopBackend GetBackend() => Backend;
        public string? GetDataRoot() => DataRoot;
        public bool CanAutomateRelocation() => SupportsAutomaticRelocation;
        public bool IsDesktopRunning() => DesktopRunning;
        public Task StopDesktopAsync(CancellationToken cancellationToken = default) { DesktopRunning = false; Calls.Add("stop"); return Task.CompletedTask; }
        public Task StartDesktopAsync(CancellationToken cancellationToken = default) { DesktopRunning = true; Calls.Add("start"); return Task.CompletedTask; }
        public Task WaitForEngineAsync(CancellationToken cancellationToken = default) { Calls.Add("wait"); return Task.CompletedTask; }
        public DockerDesktopInventory CaptureInventory() => Calls.Contains("relocate") ? DestinationInventory : OriginalInventory;
        public void CopyDataRoot(string source, string destination) { Paths.Add(destination); Calls.Add("copy"); }
        public void DeleteDataRoot(string path) { Paths.Remove(path); Calls.Add("delete"); }
        public bool DataRootExists(string path) => Paths.Contains(path);
        public Task RelocateDataRootAsync(string destination, CancellationToken cancellationToken = default) { Calls.Add("relocate"); Paths.Add(destination); return Task.CompletedTask; }
        public Task RestoreDataRootAsync(string source, CancellationToken cancellationToken = default) { Calls.Add("restore"); return Task.CompletedTask; }
    }
}

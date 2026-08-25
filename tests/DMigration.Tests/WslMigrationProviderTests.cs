using DMigration.Domain;
using DMigration.Providers;
using Xunit;

namespace DMigration.Tests;

public sealed class WslMigrationProviderTests
{
    [Fact]
    public async Task PreflightAsync_RejectsWsl1()
    {
        var host = new FakeWslHost
        {
            Distributions = [new WslDistribution("Ubuntu", 1, false)]
        };
        var provider = new WslMigrationProvider(host);

        var result = await provider.PreflightAsync(Step("Ubuntu"));

        Assert.False(result.Success);
        Assert.Contains("WSL 2", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(host.Calls);
    }

    [Fact]
    public async Task StageAsync_ValidatesTemporaryCopyBeforeOriginalCanBeUnregistered()
    {
        var host = new FakeWslHost
        {
            Distributions = [new WslDistribution("Ubuntu", 2, true)],
            OriginalIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a"),
            TemporaryIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a")
        };
        var provider = new WslMigrationProvider(host);
        var step = Step("Ubuntu");

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);

        Assert.Contains("export:Ubuntu", host.Calls);
        Assert.Contains(host.Calls, x => x.StartsWith("import-temp:", StringComparison.Ordinal));
        Assert.Contains(host.Calls, x => x.StartsWith("probe-temp:", StringComparison.Ordinal));
        Assert.DoesNotContain("unregister:Ubuntu", host.Calls);
    }

    [Fact]
    public async Task SwitchAndValidate_PreserveOriginalNameAndDefaultDistribution()
    {
        var host = new FakeWslHost
        {
            Distributions = [new WslDistribution("Ubuntu", 2, true)],
            OriginalIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a"),
            TemporaryIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a"),
            FinalIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a")
        };
        var provider = new WslMigrationProvider(host);
        var step = Step("Ubuntu");

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.True((await provider.SwitchAsync(step)).Success);
        Assert.True((await provider.ValidateAsync(step)).Success);

        var unregisterIndex = host.Calls.IndexOf("unregister:Ubuntu");
        var finalImportIndex = host.Calls.IndexOf("import-final:Ubuntu");
        Assert.True(unregisterIndex >= 0 && finalImportIndex > unregisterIndex);
        Assert.Contains("set-default:Ubuntu", host.Calls);
        Assert.Contains("probe-final:Ubuntu", host.Calls);
    }

    [Fact]
    public async Task RollbackAsync_RestoresOriginalNameFromBackupWhenFinalImportFails()
    {
        var host = new FakeWslHost
        {
            Distributions = [new WslDistribution("Ubuntu", 2, true)],
            OriginalIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a"),
            TemporaryIdentity = new WslDistributionIdentity("jaime", "ubuntu", "machine-a"),
            FailFinalImport = true
        };
        var provider = new WslMigrationProvider(host);
        var step = Step("Ubuntu");

        Assert.True((await provider.PreflightAsync(step)).Success);
        Assert.True((await provider.StageAsync(step)).Success);
        Assert.False((await provider.SwitchAsync(step)).Success);

        var rollback = await provider.RollbackAsync(step);

        Assert.True(rollback.Success);
        Assert.Contains("rollback-import:Ubuntu", host.Calls);
        Assert.Contains("set-default:Ubuntu", host.Calls);
    }

    private static MigrationStep Step(string name)
    {
        var item = new InventoryItem(
            $"wsl:{name}",
            "wsl",
            $"WSL {name}",
            $"wsl://{name}",
            0,
            "wsl",
            RiskLevel.High,
            MigrationStrategy.ExportImport,
            $@"D:\Datos\WSL\{name}\ext4.vhdx",
            true,
            "test");
        return new MigrationStep($"wsl-step-{name}", item, item.RecommendedDestination);
    }

    private sealed class FakeWslHost : IWslMigrationHost
    {
        public IReadOnlyList<WslDistribution> Distributions { get; init; } = [];
        public WslDistributionIdentity OriginalIdentity { get; init; } = new("user", "linux", "machine");
        public WslDistributionIdentity TemporaryIdentity { get; init; } = new("user", "linux", "machine");
        public WslDistributionIdentity FinalIdentity { get; init; } = new("user", "linux", "machine");
        public bool FailFinalImport { get; init; }
        public List<string> Calls { get; } = [];

        public bool IsWslAvailable() => true;
        public IReadOnlyList<WslDistribution> ListDistributions() => Distributions;
        public string? GetDefaultDistribution() => Distributions.FirstOrDefault(x => x.IsDefault)?.Name;
        public Task TerminateAsync(string name, CancellationToken cancellationToken = default)
        {
            Calls.Add($"terminate:{name}");
            return Task.CompletedTask;
        }

        public Task ExportVhdAsync(string name, string vhdPath, CancellationToken cancellationToken = default)
        {
            Calls.Add($"export:{name}");
            return Task.CompletedTask;
        }

        public void CopyFile(string source, string destination) => Calls.Add("copy-vhd");
        public bool FileExists(string path) => true;
        public void DeleteFile(string path) => Calls.Add("delete-vhd");

        public Task ImportInPlaceAsync(string name, string vhdPath, WslImportRole role, CancellationToken cancellationToken = default)
        {
            var label = role switch
            {
                WslImportRole.TemporaryValidation => $"import-temp:{name}",
                WslImportRole.Final => $"import-final:{name}",
                WslImportRole.Rollback => $"rollback-import:{name}",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
            Calls.Add(label);
            if (role == WslImportRole.Final && FailFinalImport)
                throw new InvalidOperationException("final import failed");
            return Task.CompletedTask;
        }

        public Task<WslDistributionIdentity> ProbeAsync(string name, WslProbeRole role, CancellationToken cancellationToken = default)
        {
            Calls.Add(role switch
            {
                WslProbeRole.Original => $"probe-original:{name}",
                WslProbeRole.TemporaryValidation => $"probe-temp:{name}",
                WslProbeRole.Final => $"probe-final:{name}",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            });
            return Task.FromResult(role switch
            {
                WslProbeRole.Original => OriginalIdentity,
                WslProbeRole.TemporaryValidation => TemporaryIdentity,
                WslProbeRole.Final => FinalIdentity,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            });
        }

        public Task UnregisterAsync(string name, CancellationToken cancellationToken = default)
        {
            Calls.Add($"unregister:{name}");
            return Task.CompletedTask;
        }

        public Task SetDefaultAsync(string name, CancellationToken cancellationToken = default)
        {
            Calls.Add($"set-default:{name}");
            return Task.CompletedTask;
        }
    }
}

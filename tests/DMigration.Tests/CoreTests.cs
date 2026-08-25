using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Tests;

public sealed class CoreTests
{
    [Fact]
    public void DestinationLayout_MapsPythonIntoLibraries()
    {
        var path = DestinationLayout.For("D:", "python", "pip");
        Assert.Equal(Path.Combine("D:\", "Librerias", "Python", "pip"), path);
    }

    [Fact]
    public void PlanningService_ExcludesProtectedItems()
    {
        var item = new InventoryItem(
            "windows",
            "test",
            "Windows",
            "C:\\Windows",
            100,
            "system",
            RiskLevel.Protected,
            MigrationStrategy.Protected,
            null,
            false,
            "System files");

        var plan = new PlanningService().Create([item]);

        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void ReclaimableBytes_CountsOnlyExecutableSteps()
    {
        var executable = new InventoryItem("a", "p", "A", "C:\\A", 100, "python", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "D:\\A", true, "");
        var manual = new InventoryItem("b", "p", "B", "C:\\B", 200, "wsl", RiskLevel.High, MigrationStrategy.ExportImport, "D:\\B", false, "");
        var plan = new MigrationPlan("id", DateTimeOffset.UtcNow,
        [
            new MigrationStep("1", executable, executable.RecommendedDestination),
            new MigrationStep("2", manual, manual.RecommendedDestination)
        ]);

        Assert.Equal(100, plan.ReclaimableBytes);
    }

    [Fact]
    public async Task InventoryService_DeduplicatesByProviderAndId()
    {
        var provider = new FakeProvider([
            new InventoryItem("same", "fake", "A", "C:\\A", 10, "node", RiskLevel.Low, MigrationStrategy.Manual, null, false, ""),
            new InventoryItem("same", "fake", "A", "C:\\A", 20, "node", RiskLevel.Low, MigrationStrategy.Manual, null, false, "")
        ]);

        var items = await new InventoryService([provider]).ScanAsync();

        var item = Assert.Single(items);
        Assert.Equal(20, item.SizeBytes);
    }

    private sealed class FakeProvider(IReadOnlyList<InventoryItem> items) : IInventoryProvider
    {
        public string Name => "fake";
        public Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default) => Task.FromResult(items);
    }
}

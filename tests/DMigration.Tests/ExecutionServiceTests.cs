using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Tests;

public sealed class ExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_StagesSwitchesValidatesAndCommitsInOrder()
    {
        var item = new InventoryItem(
            "npm-cache",
            "developer-tools",
            "npm cache",
            @"C:\cache",
            1024,
            "node",
            RiskLevel.Low,
            MigrationStrategy.ConfigurationChange,
            @"D:\Librerias\Node\npm-cache",
            true,
            "test");
        var plan = new MigrationPlan("plan", DateTimeOffset.UtcNow,
            [new MigrationStep("step", item, item.RecommendedDestination)]);
        var provider = new RecordingMigrationProvider();
        var journal = new RecordingJournalStore();
        var service = new ExecutionService(journal, [provider]);

        var result = await service.ExecuteAsync(plan);

        Assert.Equal(
            ["preflight", "stage", "switch", "validate", "commit"],
            provider.Calls);
        Assert.Equal(PlanStepState.Committed, Assert.Single(result.Steps).State);
        Assert.Equal(PlanStepState.Committed, Assert.Single(journal.LastSaved!.Steps).State);
    }

    private sealed class RecordingMigrationProvider : IMigrationProvider
    {
        public List<string> Calls { get; } = [];
        public string Name => "recording";
        public bool CanHandle(InventoryItem item) => true;

        public Task<MigrationOperationResult> PreflightAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("preflight");
            return Ok();
        }

        // Kept only so this test compiles against the old contract. The new lifecycle must not call it.
        public Task<MigrationOperationResult> ExecuteAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("legacy-execute");
            return Ok();
        }

        public Task<MigrationOperationResult> StageAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("stage");
            return Ok();
        }

        public Task<MigrationOperationResult> SwitchAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("switch");
            return Ok();
        }

        public Task<MigrationOperationResult> ValidateAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("validate");
            return Ok();
        }

        public Task<MigrationOperationResult> CommitAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("commit");
            return Ok();
        }

        public Task<MigrationOperationResult> RollbackAsync(MigrationStep step, CancellationToken cancellationToken = default)
        {
            Calls.Add("rollback");
            return Ok();
        }

        private static Task<MigrationOperationResult> Ok() =>
            Task.FromResult(new MigrationOperationResult(true, "ok"));
    }

    private sealed class RecordingJournalStore : IJournalStore
    {
        public MigrationPlan? LastSaved { get; private set; }

        public Task SaveAsync(MigrationPlan plan, CancellationToken cancellationToken = default)
        {
            LastSaved = plan;
            return Task.CompletedTask;
        }

        public Task<MigrationPlan?> LoadAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(LastSaved);
    }
}

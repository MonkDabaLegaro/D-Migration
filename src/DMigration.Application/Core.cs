using DMigration.Domain;

namespace DMigration.Application;

public interface IInventoryProvider
{
    string Name { get; }
    Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default);
}

public sealed record MigrationOperationResult(bool Success, string Message);

public interface IMigrationProvider
{
    string Name { get; }
    bool CanHandle(InventoryItem item);
    Task<MigrationOperationResult> PreflightAsync(MigrationStep step, CancellationToken cancellationToken = default);
    Task<MigrationOperationResult> StageAsync(MigrationStep step, CancellationToken cancellationToken = default);
    Task<MigrationOperationResult> SwitchAsync(MigrationStep step, CancellationToken cancellationToken = default);
    Task<MigrationOperationResult> ValidateAsync(MigrationStep step, CancellationToken cancellationToken = default);
    Task<MigrationOperationResult> CommitAsync(MigrationStep step, CancellationToken cancellationToken = default);
    Task<MigrationOperationResult> RollbackAsync(MigrationStep step, CancellationToken cancellationToken = default);
}

public interface IJournalStore
{
    Task SaveAsync(MigrationPlan plan, CancellationToken cancellationToken = default);
    Task<MigrationPlan?> LoadAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class InventoryService(IEnumerable<IInventoryProvider> providers)
{
    public async Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<InventoryItem>();
        foreach (var provider in providers)
            results.AddRange(await provider.ScanAsync(cancellationToken));

        return results
            .GroupBy(x => $"{x.Provider}:{x.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.SizeBytes).First())
            .OrderByDescending(x => x.SizeBytes)
            .ToArray();
    }
}

public sealed class PlanningService
{
    public MigrationPlan Create(IEnumerable<InventoryItem> items)
    {
        var steps = items
            .Where(x => x.Strategy is not MigrationStrategy.Protected)
            .Select(x => new MigrationStep(Guid.NewGuid().ToString("N"), x, x.RecommendedDestination))
            .ToArray();

        return new MigrationPlan(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, steps);
    }
}

public sealed record DoctorFinding(string Component, bool Healthy, string Message);

public sealed class DoctorService
{
    public IReadOnlyList<DoctorFinding> Inspect(IReadOnlyList<InventoryItem> items)
    {
        var findings = new List<DoctorFinding>();
        foreach (var item in items.Where(x => x.Category is "python" or "node" or "docker" or "wsl" or "ide" or "ai"))
        {
            findings.Add(new DoctorFinding(
                item.Name,
                item.Risk is not RiskLevel.High,
                $"{item.Strategy}: {item.Notes}"));
        }
        return findings;
    }
}

public sealed class ExecutionService(IJournalStore journal, IEnumerable<IMigrationProvider>? migrationProviders = null)
{
    private readonly IReadOnlyList<IMigrationProvider> _migrationProviders = migrationProviders?.ToArray() ?? [];

    public async Task<MigrationPlan> DryRunAsync(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        await journal.SaveAsync(plan, cancellationToken);
        return plan;
    }

    public async Task<MigrationPlan> ExecuteAsync(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.Steps.Any(x => !x.Item.CanExecute))
            throw new InvalidOperationException("El plan contiene operaciones que requieren intervención manual o un provider específico todavía no ejecutable.");

        var current = plan;
        for (var index = 0; index < current.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = current.Steps[index];
            var provider = _migrationProviders.FirstOrDefault(x => x.CanHandle(step.Item))
                ?? throw new InvalidOperationException($"No existe migration provider ejecutable para {step.Item.Name}.");

            await RunRequiredAsync("Preflight", provider.PreflightAsync(step, cancellationToken), step);
            current = ReplaceStep(current, index, step with { State = PlanStepState.PreflightPassed });
            await journal.SaveAsync(current, cancellationToken);

            await RunRequiredAsync("Staging", provider.StageAsync(current.Steps[index], cancellationToken), step);
            current = ReplaceStep(current, index, current.Steps[index] with { State = PlanStepState.Staged });
            await journal.SaveAsync(current, cancellationToken);

            try
            {
                await RunRequiredAsync("Switch", provider.SwitchAsync(current.Steps[index], cancellationToken), step);
                current = ReplaceStep(current, index, current.Steps[index] with { State = PlanStepState.Switched });
                await journal.SaveAsync(current, cancellationToken);

                var validation = await provider.ValidateAsync(current.Steps[index], cancellationToken);
                if (!validation.Success)
                    throw new InvalidOperationException($"Validación falló para {step.Item.Name}: {validation.Message}");

                current = ReplaceStep(current, index, current.Steps[index] with { State = PlanStepState.Validated });
                await journal.SaveAsync(current, cancellationToken);

                await RunRequiredAsync("Commit", provider.CommitAsync(current.Steps[index], cancellationToken), step);
                current = ReplaceStep(current, index, current.Steps[index] with { State = PlanStepState.Committed });
                await journal.SaveAsync(current, cancellationToken);
            }
            catch
            {
                var rollback = await provider.RollbackAsync(current.Steps[index], cancellationToken);
                current = ReplaceStep(
                    current,
                    index,
                    current.Steps[index] with { State = rollback.Success ? PlanStepState.RolledBack : PlanStepState.Failed });
                await journal.SaveAsync(current, cancellationToken);
                throw;
            }
        }

        return current;
    }

    private static async Task RunRequiredAsync(string phase, Task<MigrationOperationResult> operation, MigrationStep step)
    {
        var result = await operation;
        if (!result.Success)
            throw new InvalidOperationException($"{phase} falló para {step.Item.Name}: {result.Message}");
    }

    private static MigrationPlan ReplaceStep(MigrationPlan plan, int index, MigrationStep replacement)
    {
        var steps = plan.Steps.ToArray();
        steps[index] = replacement;
        return plan with { Steps = steps };
    }
}

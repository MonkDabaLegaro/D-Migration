using DMigration.Domain;

namespace DMigration.Application;

public interface IInventoryProvider
{
    string Name { get; }
    Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default);
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

public sealed class ExecutionService(IJournalStore journal)
{
    public async Task<MigrationPlan> DryRunAsync(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        await journal.SaveAsync(plan, cancellationToken);
        return plan;
    }

    public Task<MigrationPlan> ExecuteAsync(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.Steps.Any(x => !x.Item.CanExecute))
            throw new InvalidOperationException("El plan contiene operaciones que requieren intervención manual o un provider específico todavía no ejecutable.");

        throw new NotSupportedException("La ejecución destructiva permanece deshabilitada hasta que cada provider implemente preflight, validación y rollback.");
    }
}

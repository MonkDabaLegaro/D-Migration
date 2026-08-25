namespace DMigration.Domain;

public enum RiskLevel { Low, Medium, High, Protected }
public enum MigrationStrategy { KnownFolderRedirect, ConfigurationChange, ApplicationReinstall, DataRootMigration, ExportImport, Junction, MoveOnly, Manual, Protected }
public enum PlanStepState { Planned, PreflightPassed, Staged, Verified, Switched, Validated, Committed, RolledBack, Failed }

public sealed record InventoryItem(
    string Id,
    string Provider,
    string Name,
    string SourcePath,
    long SizeBytes,
    string Category,
    RiskLevel Risk,
    MigrationStrategy Strategy,
    string? RecommendedDestination,
    bool CanExecute,
    string Notes);

public sealed record MigrationStep(
    string Id,
    InventoryItem Item,
    string? Destination,
    PlanStepState State = PlanStepState.Planned);

public sealed record MigrationPlan(
    string Id,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MigrationStep> Steps)
{
    public long ReclaimableBytes => Steps.Where(x => x.Item.CanExecute).Sum(x => x.Item.SizeBytes);
}

public static class DestinationLayout
{
    public static string Root(string drive) => $"{drive.TrimEnd('\\')}\\";

    public static string For(string drive, string category, string name) => category.ToLowerInvariant() switch
    {
        "downloads" => Path.Combine(Root(drive), "Descargas"),
        "documents" => Path.Combine(Root(drive), "Documentos"),
        "pictures" => Path.Combine(Root(drive), "Fotos y videos", "Fotos"),
        "videos" => Path.Combine(Root(drive), "Fotos y videos", "Videos"),
        "music" => Path.Combine(Root(drive), "Musica"),
        "games" => Path.Combine(Root(drive), "Games", name),
        "python" => Path.Combine(Root(drive), "Librerias", "Python", name),
        "node" => Path.Combine(Root(drive), "Librerias", "Node", name),
        "ai" => Path.Combine(Root(drive), "Datos", "AI", name),
        "wsl" => Path.Combine(Root(drive), "Datos", "WSL", name),
        "docker" => Path.Combine(Root(drive), "Datos", "Docker", name),
        "ide" => Path.Combine(Root(drive), "Componentes", "Desarrollo", "IDEs", name),
        _ => Path.Combine(Root(drive), "Componentes", name)
    };
}

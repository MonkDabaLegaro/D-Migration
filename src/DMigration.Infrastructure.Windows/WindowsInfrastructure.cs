using System.Text.Json;
using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Infrastructure.Windows;

public sealed class WindowsKnownFolderProvider(string destinationDrive = "D:") : IInventoryProvider
{
    public string Name => "windows-known-folders";

    public Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            ("downloads", Path.Combine(profile, "Downloads"), "Downloads", "Descargas"),
            ("documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents", "Documentos"),
            ("pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pictures", "Fotos y videos"),
            ("videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videos", "Fotos y videos"),
            ("music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Music", "Musica")
        };

        var items = new List<InventoryItem>();
        foreach (var (id, path, category, name) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;

            var cloudManaged = KnownFolderPathSafety.IsCloudManaged(path);
            var canExecute = OperatingSystem.IsWindows() && !cloudManaged;
            var notes = cloudManaged
                ? "Carpeta administrada por OneDrive u otro proveedor cloud; requiere revisión manual."
                : "Carpeta conocida de Windows apta para redirección transaccional mediante Shell Known Folder API.";

            items.Add(new InventoryItem(
                id,
                Name,
                name,
                path,
                DirectorySizer.TryGetSize(path),
                category.ToLowerInvariant(),
                cloudManaged ? RiskLevel.Medium : RiskLevel.Low,
                MigrationStrategy.KnownFolderRedirect,
                DestinationLayout.For(destinationDrive, category, name),
                canExecute,
                notes));
        }

        return Task.FromResult<IReadOnlyList<InventoryItem>>(items);
    }
}

public static class DirectorySizer
{
    public static long TryGetSize(string path)
    {
        try
        {
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return total;
        }
        catch (UnauthorizedAccessException) { return 0; }
        catch (IOException) { return 0; }
    }
}

public sealed class JsonJournalStore : IJournalStore
{
    private readonly string _root;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonJournalStore(string preferredDrive = "D:")
    {
        var preferred = Path.Combine(preferredDrive + Path.DirectorySeparatorChar, ".d-migration", "state");
        _root = Directory.Exists(Path.GetPathRoot(preferred) ?? string.Empty)
            ? preferred
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D-Migration", "state");
    }

    public async Task SaveAsync(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, $"{plan.Id}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, plan, Options, cancellationToken);
    }

    public async Task<MigrationPlan?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_root, $"{id}.json");
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MigrationPlan>(stream, Options, cancellationToken);
    }
}

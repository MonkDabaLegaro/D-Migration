using DMigration.Application;
using DMigration.Domain;
using DMigration.Infrastructure.Windows;

namespace DMigration.Providers;

public sealed class DeveloperToolProvider(string destinationDrive = "D:") : IInventoryProvider
{
    public string Name => "developer-tools";

    public Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var items = new List<InventoryItem>();

        AddPath(items, "pip-cache", "pip cache", Environment.GetEnvironmentVariable("PIP_CACHE_DIR") ?? Path.Combine(local, "pip", "Cache"), "python", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "pip", "Preferir configuración oficial de caché de pip.");
        AddPath(items, "npm-cache", "npm cache", Environment.GetEnvironmentVariable("npm_config_cache") ?? Path.Combine(local, "npm-cache"), "node", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "npm-cache", "Preferir npm config sobre junctions.");
        AddPath(items, "pnpm-store", "pnpm store", Environment.GetEnvironmentVariable("PNPM_HOME") ?? Path.Combine(local, "pnpm"), "node", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "pnpm", "Reconfigurar store/home con el gestor antes de mover datos.");
        AddPath(items, "ollama-models", "Ollama models", Environment.GetEnvironmentVariable("OLLAMA_MODELS") ?? Path.Combine(home, ".ollama", "models"), "ai", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "Ollama", "Usar OLLAMA_MODELS y validar el runtime antes de retirar el origen.");
        AddPath(items, "huggingface-cache", "Hugging Face cache", Environment.GetEnvironmentVariable("HF_HOME") ?? Path.Combine(home, ".cache", "huggingface"), "ai", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "HuggingFace", "Usar HF_HOME/HF_HUB_CACHE en vez de enlaces genéricos.");

        if (OperatingSystem.IsWindows())
        {
            AddPath(items, "docker-desktop", "Docker Desktop data", Path.Combine(local, "Docker"), "docker", RiskLevel.Medium, MigrationStrategy.DataRootMigration, "Docker", "La imagen de disco debe reubicarse mediante mecanismos soportados por Docker Desktop.");
            AddPath(items, "wsl-data", "WSL distributions", Path.Combine(local, "Packages"), "wsl", RiskLevel.High, MigrationStrategy.ExportImport, "WSL", "Nunca mover VHDX directamente; usar export/import o import-in-place según corresponda.", canExecute: false, includeOnlyWhen: ContainsWslData);

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            AddPath(items, "visual-studio", "Visual Studio", Path.Combine(programFiles, "Microsoft Visual Studio"), "ide", RiskLevel.Medium, MigrationStrategy.ApplicationReinstall, "VisualStudio", "Instalación administrada por Visual Studio Installer; conservar workloads/configuración y reinstalar con rutas soportadas.");
            AddPath(items, "vscode-extensions", "VS Code extensions", Path.Combine(home, ".vscode", "extensions"), "ide", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "VSCode", "Configurable mediante --extensions-dir o variables/launcher; evitar mover la instalación completa.");
        }

        return Task.FromResult<IReadOnlyList<InventoryItem>>(items.OrderByDescending(x => x.SizeBytes).ToArray());

        void AddPath(
            ICollection<InventoryItem> target,
            string id,
            string display,
            string path,
            string category,
            RiskLevel risk,
            MigrationStrategy strategy,
            string destinationName,
            string notes,
            bool canExecute = false,
            Func<string, bool>? includeOnlyWhen = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            if (includeOnlyWhen is not null && !includeOnlyWhen(path)) return;

            target.Add(new InventoryItem(
                id,
                Name,
                display,
                path,
                DirectorySizer.TryGetSize(path),
                category,
                risk,
                strategy,
                DestinationLayout.For(destinationDrive, category, destinationName),
                canExecute,
                notes));
        }
    }

    private static bool ContainsWslData(string packagesRoot)
    {
        try
        {
            return Directory.EnumerateFiles(packagesRoot, "ext4.vhdx", SearchOption.AllDirectories).Any();
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }
}

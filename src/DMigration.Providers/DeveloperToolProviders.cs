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
        var items = new List<InventoryItem>();

        AddPath(items, "pip-cache", "pip cache", Environment.GetEnvironmentVariable("PIP_CACHE_DIR") ?? Path.Combine(local, "pip", "Cache"), "python", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "pip", "Configurar PIP_CACHE_DIR y validar la copia antes de retirar el origen.", canExecute: true);
        AddPath(items, "npm-cache", "npm cache", Environment.GetEnvironmentVariable("NPM_CONFIG_CACHE") ?? Environment.GetEnvironmentVariable("npm_config_cache") ?? Path.Combine(local, "npm-cache"), "node", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "npm-cache", "Configurar NPM_CONFIG_CACHE y validar la copia antes de retirar el origen.", canExecute: true);
        AddPath(items, "pnpm-store", "pnpm store", Environment.GetEnvironmentVariable("PNPM_HOME") ?? Path.Combine(local, "pnpm"), "node", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "pnpm", "PNPM_HOME no identifica necesariamente el store; requiere provider dedicado antes de automatizar.");
        AddPath(items, "ollama-models", "Ollama models", Environment.GetEnvironmentVariable("OLLAMA_MODELS") ?? Path.Combine(home, ".ollama", "models"), "ai", RiskLevel.Medium, MigrationStrategy.ConfigurationChange, "Ollama", "OLLAMA_MODELS es soportado, pero la automatización requiere validar el runtime con los modelos en el destino antes de retirar el origen.");
        AddPath(items, "huggingface-cache", "Hugging Face cache", Environment.GetEnvironmentVariable("HF_HOME") ?? Path.Combine(home, ".cache", "huggingface"), "ai", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "HuggingFace", "Configurar HF_HOME y validar la copia antes de retirar el origen.", canExecute: true);

        if (OperatingSystem.IsWindows())
        {
            AddPath(items, "docker-desktop", "Docker Desktop data", Path.Combine(local, "Docker"), "docker", RiskLevel.Medium, MigrationStrategy.DataRootMigration, "Docker", "La imagen de disco debe reubicarse mediante mecanismos soportados por Docker Desktop.");
            AddPath(items, "wsl-data", "WSL distributions", Path.Combine(local, "Packages"), "wsl", RiskLevel.High, MigrationStrategy.ExportImport, "WSL", "Nunca mover VHDX directamente; usar export/import o import-in-place según corresponda.", includeOnlyWhen: ContainsWslData);

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            AddPath(items, "visual-studio", "Visual Studio", Path.Combine(programFiles, "Microsoft Visual Studio"), "ide", RiskLevel.Medium, MigrationStrategy.ApplicationReinstall, "VisualStudio", "Instalación administrada por Visual Studio Installer; conservar workloads/configuración y reinstalar con rutas soportadas.");
            AddPath(items, "vscode-extensions", "VS Code extensions", Path.Combine(home, ".vscode", "extensions"), "ide", RiskLevel.Low, MigrationStrategy.ConfigurationChange, "VSCode", "Requiere un provider dedicado a --extensions-dir/configuración antes de automatizar.");
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

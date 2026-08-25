using System.Diagnostics;
using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Providers;

public enum DockerDesktopBackend { Unknown, Wsl2, HyperV }
public sealed record DockerDesktopInventory(IReadOnlyList<string> Images, IReadOnlyList<string> Volumes);

public interface IDockerDesktopMigrationHost
{
    bool IsInstalled();
    DockerDesktopBackend GetBackend();
    string? GetDataRoot();
    bool CanAutomateRelocation();
    bool IsDesktopRunning();
    Task StopDesktopAsync(CancellationToken cancellationToken = default);
    Task StartDesktopAsync(CancellationToken cancellationToken = default);
    Task WaitForEngineAsync(CancellationToken cancellationToken = default);
    DockerDesktopInventory CaptureInventory();
    void CopyDataRoot(string source, string destination);
    void DeleteDataRoot(string path);
    bool DataRootExists(string path);
    Task RelocateDataRootAsync(string destination, CancellationToken cancellationToken = default);
    Task RestoreDataRootAsync(string source, CancellationToken cancellationToken = default);
}

public sealed class DockerDesktopInventoryProvider(IDockerDesktopMigrationHost host, string destinationDrive) : IInventoryProvider
{
    public string Name => "docker-desktop";
    public Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!host.IsInstalled()) return Task.FromResult<IReadOnlyList<InventoryItem>>([]);
        var root = host.GetDataRoot();
        if (string.IsNullOrWhiteSpace(root)) return Task.FromResult<IReadOnlyList<InventoryItem>>([]);
        var automatic = host.GetBackend() == DockerDesktopBackend.Wsl2 && host.CanAutomateRelocation();
        var notes = automatic ? "Docker Desktop WSL2 con canal de reubicación verificable." : "Reubicación asistida: Docker Desktop no expone un canal automatizable soportado en esta instalación.";
        InventoryItem item = new("docker-desktop-wsl2", "docker-desktop", "Docker Desktop data", root, 0, "docker", RiskLevel.High,
            MigrationStrategy.DataRootMigration, Path.Combine(destinationDrive, "Datos", "Docker", "wsl"), automatic, notes);
        return Task.FromResult<IReadOnlyList<InventoryItem>>([item]);
    }
}

public sealed class DockerDesktopMigrationProvider(IDockerDesktopMigrationHost host) : IMigrationProvider
{
    private readonly Dictionary<string, DockerDesktopInventory> _inventories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
    public string Name => "docker-desktop-wsl2";
    public bool CanHandle(InventoryItem item) => item.Id == "docker-desktop-wsl2" && item.CanExecute;
    public Task<MigrationOperationResult> PreflightAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!host.IsInstalled()) return Fail("Docker Desktop no está instalado.");
        if (host.GetBackend() != DockerDesktopBackend.Wsl2) return Fail("Sólo Docker Desktop con backend WSL2 está automatizado.");
        if (!host.CanAutomateRelocation()) return Fail("Esta instalación requiere reubicación asistida desde Docker Desktop.");
        if (host.IsDesktopRunning()) return Fail("Cierra Docker Desktop antes de ejecutar la migración.");
        var source = host.GetDataRoot();
        if (string.IsNullOrWhiteSpace(source) || !host.DataRootExists(source)) return Fail("No se encontró el data root de Docker Desktop.");
        _sources[step.Id] = source;
        _inventories[step.Id] = host.CaptureInventory();
        return Ok("Preflight Docker completado.");
    }
    public Task<MigrationOperationResult> StageAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_sources.TryGetValue(step.Id, out var source) || string.IsNullOrWhiteSpace(step.Destination)) return Fail("Preflight requerido.");
        host.CopyDataRoot(source, step.Destination);
        return Ok("Copia de staging creada; el origen permanece intacto.");
    }
    public async Task<MigrationOperationResult> SwitchAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(step.Destination)) return await Fail("Destino inválido.");
        await host.RelocateDataRootAsync(step.Destination, cancellationToken);
        await host.StartDesktopAsync(cancellationToken);
        await host.WaitForEngineAsync(cancellationToken);
        return await Ok("Docker Desktop arrancó usando el destino.");
    }
    public Task<MigrationOperationResult> ValidateAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_inventories.TryGetValue(step.Id, out var before)) return Fail("Inventario original no disponible.");
        var after = host.CaptureInventory();
        var imagesMatch = before.Images.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(after.Images.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var volumesMatch = before.Volumes.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(after.Volumes.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        return imagesMatch && volumesMatch ? Ok("Inventario Docker validado: imágenes y volúmenes coinciden.") : Fail("El inventario Docker del destino no coincide con el origen.");
    }
    public async Task<MigrationOperationResult> CommitAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_sources.TryGetValue(step.Id, out var source)) return await Fail("Origen no registrado.");
        if (host.IsDesktopRunning()) await host.StopDesktopAsync(cancellationToken);
        host.DeleteDataRoot(source);
        await host.StartDesktopAsync(cancellationToken);
        await host.WaitForEngineAsync(cancellationToken);
        return await Ok("Origen Docker eliminado después de validación.");
    }
    public async Task<MigrationOperationResult> RollbackAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_sources.TryGetValue(step.Id, out var source)) return await Fail("No existe estado para rollback.");
        if (host.IsDesktopRunning()) await host.StopDesktopAsync(cancellationToken);
        await host.RestoreDataRootAsync(source, cancellationToken);
        await host.StartDesktopAsync(cancellationToken);
        await host.WaitForEngineAsync(cancellationToken);
        return await Ok("Docker Desktop restaurado al data root original.");
    }
    private static Task<MigrationOperationResult> Ok(string message) => Task.FromResult(new MigrationOperationResult(true, message));
    private static Task<MigrationOperationResult> Fail(string message) => Task.FromResult(new MigrationOperationResult(false, message));
}

public sealed class WindowsDockerDesktopMigrationHost : IDockerDesktopMigrationHost
{
    private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private string DefaultWslRoot => Path.Combine(_localAppData, "Docker", "wsl");
    public bool IsInstalled() => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"));
    public DockerDesktopBackend GetBackend() => Directory.Exists(DefaultWslRoot) ? DockerDesktopBackend.Wsl2 : DockerDesktopBackend.Unknown;
    public string? GetDataRoot() => GetBackend() == DockerDesktopBackend.Wsl2 ? DefaultWslRoot : null;
    public bool CanAutomateRelocation() => false;
    public bool IsDesktopRunning() => Process.GetProcessesByName("Docker Desktop").Length > 0 || Process.GetProcessesByName("com.docker.backend").Length > 0;
    public bool DataRootExists(string path) => Directory.Exists(path);
    public Task StopDesktopAsync(CancellationToken cancellationToken = default) => Unsupported();
    public Task StartDesktopAsync(CancellationToken cancellationToken = default) => Unsupported();
    public Task WaitForEngineAsync(CancellationToken cancellationToken = default) => Unsupported();
    public DockerDesktopInventory CaptureInventory() => throw new NotSupportedException("Inventario runtime reservado para un canal soportado de reubicación.");
    public void CopyDataRoot(string source, string destination) => throw new NotSupportedException("Reubicación automática no habilitada.");
    public void DeleteDataRoot(string path) => throw new NotSupportedException("Reubicación automática no habilitada.");
    public Task RelocateDataRootAsync(string destination, CancellationToken cancellationToken = default) => Unsupported();
    public Task RestoreDataRootAsync(string source, CancellationToken cancellationToken = default) => Unsupported();
    private static Task Unsupported() => Task.FromException(new NotSupportedException("Usa Docker Desktop > Settings > Resources > Advanced > Disk image location."));
}

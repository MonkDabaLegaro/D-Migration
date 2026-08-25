using System.Diagnostics;
using DMigration.Providers;

namespace DMigration.Infrastructure.Windows;

public sealed class WindowsDockerDesktopMigrationHost : IDockerDesktopMigrationHost
{
    private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private string DefaultWslRoot => Path.Combine(_localAppData, "Docker", "wsl");

    public bool IsInstalled() =>
        Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker")) ||
        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"));

    public DockerDesktopBackend GetBackend() => Directory.Exists(DefaultWslRoot)
        ? DockerDesktopBackend.Wsl2
        : DockerDesktopBackend.Unknown;

    public string? GetDataRoot() => GetBackend() == DockerDesktopBackend.Wsl2 ? DefaultWslRoot : null;

    // Docker documents changing Disk image location through Desktop Settings, but does not publish
    // a stable programmatic contract for mutating that setting on an existing installation.
    public bool CanAutomateRelocation() => false;

    public bool IsDesktopRunning() => Process.GetProcessesByName("Docker Desktop").Length > 0 ||
                                      Process.GetProcessesByName("com.docker.backend").Length > 0;

    public Task StopDesktopAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("La reubicación automática no está habilitada para este host.");
    public Task StartDesktopAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("La reubicación automática no está habilitada para este host.");
    public Task WaitForEngineAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("La reubicación automática no está habilitada para este host.");
    public DockerDesktopInventory CaptureInventory() =>
        throw new NotSupportedException("El inventario runtime se habilitará junto con un canal soportado de reubicación.");
    public void CopyDataRoot(string source, string destination) =>
        throw new NotSupportedException("La reubicación automática no está habilitada para este host.");
    public void DeleteDataRoot(string path) =>
        throw new NotSupportedException("La reubicación automática no está habilitada para este host.");
    public bool DataRootExists(string path) => Directory.Exists(path);
    public Task RelocateDataRootAsync(string destination, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Usa Docker Desktop > Settings > Resources > Advanced > Disk image location.");
    public Task RestoreDataRootAsync(string source, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Usa Docker Desktop para restaurar Disk image location.");
}

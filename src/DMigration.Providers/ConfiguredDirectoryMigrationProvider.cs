using System.Diagnostics;
using System.Security.Cryptography;
using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Providers;

public sealed record ConfiguredDirectoryRule(
    string ItemId,
    string EnvironmentVariable,
    IReadOnlyCollection<string>? BlockingProcesses = null);

public interface IConfiguredDirectoryMigrationHost
{
    bool DirectoryExists(string path);
    long GetAvailableBytes(string path);
    bool IsAnyProcessRunning(IReadOnlyCollection<string> processNames);
    string? GetUserEnvironmentVariable(string name);
    void SetUserEnvironmentVariable(string name, string? value);
    void CopyDirectory(string source, string destination);
    bool DirectoryContentsMatch(string source, string destination);
    void DeleteDirectory(string path);
}

public sealed class ConfiguredDirectoryMigrationProvider(
    IConfiguredDirectoryMigrationHost host,
    IEnumerable<ConfiguredDirectoryRule> rules) : IMigrationProvider
{
    private readonly IReadOnlyDictionary<string, ConfiguredDirectoryRule> _rules =
        rules.ToDictionary(x => x.ItemId, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _originalConfiguration = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "configured-directory";

    public bool CanHandle(InventoryItem item) =>
        item.Provider.Equals("developer-tools", StringComparison.OrdinalIgnoreCase) &&
        item.CanExecute &&
        _rules.ContainsKey(item.Id);

    public Task<MigrationOperationResult> PreflightAsync(
        MigrationStep step,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error))
            return Result(false, error);

        if (!host.DirectoryExists(step.Item.SourcePath))
            return Result(false, "El directorio de origen ya no existe.");

        if (PathsEqual(step.Item.SourcePath, destination))
            return Result(false, "Origen y destino son la misma ubicación.");

        if (host.DirectoryExists(destination))
            return Result(false, "El destino ya existe; se rechaza para evitar mezclar o sobrescribir datos.");

        var blockers = rule.BlockingProcesses ?? [];
        if (blockers.Count > 0 && host.IsAnyProcessRunning(blockers))
            return Result(false, $"Cierra primero: {string.Join(", ", blockers)}.");

        if (host.GetAvailableBytes(destination) < step.Item.SizeBytes)
            return Result(false, "No hay espacio libre suficiente en el volumen de destino.");

        _originalConfiguration[step.Id] = host.GetUserEnvironmentVariable(rule.EnvironmentVariable);
        return Result(true, "Preflight completado.");
    }

    public Task<MigrationOperationResult> StageAsync(
        MigrationStep step,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out _, out var destination, out var error))
            return Result(false, error);

        try
        {
            host.CopyDirectory(step.Item.SourcePath, destination);
            return Result(true, "Datos copiados al destino; el origen permanece intacto.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, ex.Message);
        }
    }

    public Task<MigrationOperationResult> SwitchAsync(
        MigrationStep step,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error))
            return Result(false, error);

        try
        {
            host.SetUserEnvironmentVariable(rule.EnvironmentVariable, destination);
            return Result(true, $"{rule.EnvironmentVariable} ahora apunta al destino.");
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            return Result(false, ex.Message);
        }
    }

    public Task<MigrationOperationResult> ValidateAsync(
        MigrationStep step,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error))
            return Result(false, error);

        if (!string.Equals(
                Normalize(host.GetUserEnvironmentVariable(rule.EnvironmentVariable)),
                Normalize(destination),
                StringComparison.OrdinalIgnoreCase))
            return Result(false, $"{rule.EnvironmentVariable} no quedó configurada con el destino esperado.");

        try
        {
            if (!host.DirectoryContentsMatch(step.Item.SourcePath, destination))
                return Result(false, "Origen y destino no coinciden por ruta, tamaño y SHA-256.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, $"No se pudo verificar integridad: {ex.Message}");
        }

        return Result(true, "Configuración e integridad SHA-256 verificadas.");
    }

    public Task<MigrationOperationResult> CommitAsync(
        MigrationStep step,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out _, out var destination, out var error))
            return Result(false, error);

        if (!host.DirectoryExists(destination))
            return Result(false, "El destino desapareció antes del commit.");

        try
        {
            host.DeleteDirectory(step.Item.SourcePath);
            return Result(true, "Origen eliminado después de la validación.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, ex.Message);
        }
    }

    public Task<MigrationOperationResult> RollbackAsync(
        MigrationStep step,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error))
            return Result(false, error);

        try
        {
            if (!host.DirectoryExists(step.Item.SourcePath) && host.DirectoryExists(destination))
                host.CopyDirectory(destination, step.Item.SourcePath);

            if (_originalConfiguration.TryGetValue(step.Id, out var originalValue))
                host.SetUserEnvironmentVariable(rule.EnvironmentVariable, originalValue);

            return Result(true, "Configuración y origen restaurados; la copia de destino se conserva para evitar pérdida de datos.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Result(false, ex.Message);
        }
    }

    private bool TryResolve(
        MigrationStep step,
        out ConfiguredDirectoryRule rule,
        out string destination,
        out string error)
    {
        if (!_rules.TryGetValue(step.Item.Id, out rule!))
        {
            destination = string.Empty;
            error = $"No existe una regla ejecutable para {step.Item.Id}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(step.Destination))
        {
            destination = string.Empty;
            error = "El plan no contiene destino.";
            return false;
        }

        destination = step.Destination;
        error = string.Empty;
        return true;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? path) => path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static Task<MigrationOperationResult> Result(bool success, string message) =>
        Task.FromResult(new MigrationOperationResult(success, message));
}

public sealed class WindowsConfiguredDirectoryMigrationHost : IConfiguredDirectoryMigrationHost
{
    private sealed record FileFingerprint(long Length, string Sha256);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public long GetAvailableBytes(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root)) return 0;
        return new DriveInfo(root).AvailableFreeSpace;
    }

    public bool IsAnyProcessRunning(IReadOnlyCollection<string> processNames)
    {
        foreach (var processName in processNames)
        {
            var normalized = Path.GetFileNameWithoutExtension(processName);
            if (Process.GetProcessesByName(normalized).Length > 0) return true;
        }
        return false;
    }

    public string? GetUserEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);

    public void SetUserEnvironmentVariable(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);

    public void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    public bool DirectoryContentsMatch(string source, string destination)
    {
        if (!Directory.Exists(source) || !Directory.Exists(destination)) return false;

        var sourceFiles = Snapshot(source);
        var destinationFiles = Snapshot(destination);
        return sourceFiles.Count == destinationFiles.Count &&
               sourceFiles.All(x => destinationFiles.TryGetValue(x.Key, out var fingerprint) && fingerprint == x.Value);
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static Dictionary<string, FileFingerprint> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(root, file),
                Fingerprint,
                StringComparer.OrdinalIgnoreCase);

    private static FileFingerprint Fingerprint(string file)
    {
        using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = SHA256.HashData(stream);
        return new FileFingerprint(stream.Length, Convert.ToHexString(hash));
    }
}

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Infrastructure.Windows;

public sealed record KnownFolderRule(string ItemId, Guid FolderId);

public static class KnownFolderIds
{
    public static readonly Guid Documents = new("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    public static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");
    public static readonly Guid Pictures = new("33E28130-4E1E-4676-835A-98395C3BC3BB");
    public static readonly Guid Videos = new("18989B1D-99B5-455B-841C-AB7C74E4DDFC");
    public static readonly Guid Music = new("4BD8D571-6D19-48D3-BE97-422220080E43");
}

public interface IKnownFolderMigrationHost
{
    bool DirectoryExists(string path);
    long GetAvailableBytes(string path);
    string? GetKnownFolderPath(Guid folderId);
    bool IsCloudManaged(string path);
    void SetKnownFolderPath(Guid folderId, string path);
    void CopyDirectory(string source, string destination);
    bool DirectoryContentsMatch(string source, string destination);
    void DeleteDirectory(string path);
}

public sealed class KnownFolderMigrationProvider(
    IKnownFolderMigrationHost host,
    IEnumerable<KnownFolderRule> rules) : IMigrationProvider
{
    private readonly IReadOnlyDictionary<string, KnownFolderRule> _rules =
        rules.ToDictionary(x => x.ItemId, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _originalPaths = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "windows-known-folder";

    public bool CanHandle(InventoryItem item) =>
        item.Provider.Equals("windows-known-folders", StringComparison.OrdinalIgnoreCase) &&
        item.CanExecute &&
        _rules.ContainsKey(item.Id);

    public Task<MigrationOperationResult> PreflightAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error)) return Result(false, error);

        var currentPath = host.GetKnownFolderPath(rule.FolderId);
        if (string.IsNullOrWhiteSpace(currentPath))
            return Result(false, "Windows no devolvió una ruta efectiva para la carpeta conocida.");

        if (!PathsEqual(currentPath, step.Item.SourcePath))
            return Result(false, "El plan está desactualizado: Windows cambió la ubicación actual de la carpeta conocida.");

        if (host.IsCloudManaged(currentPath))
            return Result(false, "La carpeta está administrada por OneDrive u otro proveedor cloud y requiere revisión manual.");

        if (!host.DirectoryExists(currentPath))
            return Result(false, "El directorio de origen ya no existe.");

        if (PathsEqual(currentPath, destination))
            return Result(false, "Origen y destino son la misma ubicación.");

        if (host.DirectoryExists(destination))
            return Result(false, "El destino ya existe; se rechaza para evitar mezclar dos carpetas conocidas.");

        if (host.GetAvailableBytes(destination) < step.Item.SizeBytes)
            return Result(false, "No hay espacio libre suficiente en el volumen de destino.");

        _originalPaths[step.Id] = currentPath;
        return Result(true, "Preflight de carpeta conocida completado.");
    }

    public Task<MigrationOperationResult> StageAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out _, out var destination, out var error)) return Result(false, error);

        try
        {
            host.CopyDirectory(step.Item.SourcePath, destination);
            return Result(true, "Datos copiados; el origen permanece intacto.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, ex.Message);
        }
    }

    public Task<MigrationOperationResult> SwitchAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error)) return Result(false, error);

        try
        {
            host.SetKnownFolderPath(rule.FolderId, destination);
            return Result(true, "Windows redirigió la carpeta conocida al destino.");
        }
        catch (Exception ex) when (ex is ExternalException or UnauthorizedAccessException or IOException)
        {
            return Result(false, ex.Message);
        }
    }

    public Task<MigrationOperationResult> ValidateAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error)) return Result(false, error);

        if (!PathsEqual(host.GetKnownFolderPath(rule.FolderId), destination))
            return Result(false, "Windows no informa el destino esperado como ruta efectiva.");

        if (!host.DirectoryContentsMatch(step.Item.SourcePath, destination))
            return Result(false, "La copia no coincide byte a byte con el origen.");

        return Result(true, "Ruta efectiva y contenido validados.");
    }

    public Task<MigrationOperationResult> CommitAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error)) return Result(false, error);

        if (!PathsEqual(host.GetKnownFolderPath(rule.FolderId), destination))
            return Result(false, "La carpeta dejó de apuntar al destino antes del commit.");

        if (!host.DirectoryContentsMatch(step.Item.SourcePath, destination))
            return Result(false, "La integridad cambió antes del commit; el origen se conserva.");

        try
        {
            host.DeleteDirectory(step.Item.SourcePath);
            return Result(true, "Origen eliminado después de validar la redirección y la integridad.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, ex.Message);
        }
    }

    public Task<MigrationOperationResult> RollbackAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolve(step, out var rule, out var destination, out var error)) return Result(false, error);

        try
        {
            if (_originalPaths.TryGetValue(step.Id, out var originalPath) && !string.IsNullOrWhiteSpace(originalPath))
            {
                if (!host.DirectoryExists(originalPath) && host.DirectoryExists(destination))
                    host.CopyDirectory(destination, originalPath);

                host.SetKnownFolderPath(rule.FolderId, originalPath);
            }

            return Result(true, "Ruta original restaurada; la copia de destino se conserva como salvaguarda.");
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            return Result(false, ex.Message);
        }
    }

    private bool TryResolve(MigrationStep step, out KnownFolderRule rule, out string destination, out string error)
    {
        if (!_rules.TryGetValue(step.Item.Id, out rule!))
        {
            destination = string.Empty;
            error = $"No existe una regla de carpeta conocida para {step.Item.Id}.";
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

    private static bool PathsEqual(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? path) =>
        path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static Task<MigrationOperationResult> Result(bool success, string message) =>
        Task.FromResult(new MigrationOperationResult(success, message));
}

public sealed class WindowsKnownFolderMigrationHost : IKnownFolderMigrationHost
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public long GetAvailableBytes(string path)
    {
        var root = Path.GetPathRoot(path);
        return string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
    }

    public string? GetKnownFolderPath(Guid folderId)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var id = folderId;
        var hr = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var rawPath);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        try { return Marshal.PtrToStringUni(rawPath); }
        finally { Marshal.FreeCoTaskMem(rawPath); }
    }

    public bool IsCloudManaged(string path) => KnownFolderPathSafety.IsCloudManaged(path);

    public void SetKnownFolderPath(Guid folderId, string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Known Folder redirection requires Windows.");
        Directory.CreateDirectory(path);
        var id = folderId;
        var hr = SHSetKnownFolderPath(ref id, 0, IntPtr.Zero, path);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    public void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    public bool DirectoryContentsMatch(string source, string destination)
    {
        if (!Directory.Exists(source) || !Directory.Exists(destination)) return false;
        var sourceFiles = Snapshot(source);
        var destinationFiles = Snapshot(destination);
        return sourceFiles.Count == destinationFiles.Count && sourceFiles.All(x =>
            destinationFiles.TryGetValue(x.Key, out var other) &&
            x.Value.Length == other.Length &&
            CryptographicOperations.FixedTimeEquals(x.Value.Hash, other.Hash));
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static Dictionary<string, FileFingerprint> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(root, file),
                file => Fingerprint(file),
                StringComparer.OrdinalIgnoreCase);

    private static FileFingerprint Fingerprint(string file)
    {
        using var stream = File.OpenRead(file);
        return new FileFingerprint(stream.Length, SHA256.HashData(stream));
    }

    private sealed record FileFingerprint(long Length, byte[] Hash);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHSetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, string pszPath);
}

public static class KnownFolderPathSafety
{
    public static bool IsCloudManaged(string path)
    {
        var normalized = Normalize(path);
        if (normalized.Contains($"{Path.DirectorySeparatorChar}OneDrive", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains($"{Path.AltDirectorySeparatorChar}OneDrive", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            var root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root) && IsUnder(normalized, Normalize(root))) return true;
        }

        return false;
    }

    private static bool IsUnder(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

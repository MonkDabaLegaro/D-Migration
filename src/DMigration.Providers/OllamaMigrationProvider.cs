using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Providers;

public sealed record OllamaModelIdentity(string Name, string Digest);
public sealed record OllamaRuntimeProbe(bool Success, IReadOnlyList<OllamaModelIdentity> Models, string Message);

public interface IOllamaMigrationHost
{
    bool DirectoryExists(string path);
    long GetAvailableBytes(string path);
    bool IsRuntimeAvailable();
    bool IsOllamaRunning();
    string? GetUserEnvironmentVariable(string name);
    void SetUserEnvironmentVariable(string name, string? value);
    void CopyDirectory(string source, string destination);
    bool DirectoryContentsMatch(string source, string destination);
    void DeleteDirectory(string path);
    Task<OllamaRuntimeProbe> ProbeAsync(string modelsPath, CancellationToken cancellationToken = default);
}

public sealed class OllamaMigrationProvider(IOllamaMigrationHost host) : IMigrationProvider
{
    private const string ItemId = "ollama-models";
    private const string EnvironmentVariable = "OLLAMA_MODELS";
    private readonly Dictionary<string, string?> _originalConfiguration = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<OllamaModelIdentity>> _sourceModels = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "ollama-models";

    public bool CanHandle(InventoryItem item) =>
        item.Provider.Equals("developer-tools", StringComparison.OrdinalIgnoreCase) &&
        item.Id.Equals(ItemId, StringComparison.OrdinalIgnoreCase) &&
        item.CanExecute;

    public async Task<MigrationOperationResult> PreflightAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!TryDestination(step, out var destination, out var error)) return Result(false, error);
        if (!host.DirectoryExists(step.Item.SourcePath)) return Result(false, "El directorio de modelos de Ollama ya no existe.");
        if (PathsEqual(step.Item.SourcePath, destination)) return Result(false, "Origen y destino son la misma ubicación.");
        if (host.DirectoryExists(destination)) return Result(false, "El destino ya existe; se rechaza para evitar mezclar modelos.");
        if (!host.IsRuntimeAvailable()) return Result(false, "No se encontró el runtime de Ollama.");
        if (host.IsOllamaRunning()) return Result(false, "Cierra Ollama antes de migrar para poder validar ambos almacenes de forma aislada.");
        if (host.GetAvailableBytes(destination) < step.Item.SizeBytes) return Result(false, "No hay espacio libre suficiente en el destino.");

        _originalConfiguration[step.Id] = host.GetUserEnvironmentVariable(EnvironmentVariable);
        var probe = await host.ProbeAsync(step.Item.SourcePath, cancellationToken);
        if (!probe.Success) return Result(false, $"Ollama no pudo validar el almacén original: {probe.Message}");
        _sourceModels[step.Id] = NormalizeModels(probe.Models);
        return Result(true, $"Runtime de Ollama validó {probe.Models.Count} modelos en el origen.");
    }

    public Task<MigrationOperationResult> StageAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryDestination(step, out var destination, out var error)) return Task.FromResult(Result(false, error));
        try
        {
            host.CopyDirectory(step.Item.SourcePath, destination);
            return Task.FromResult(Result(true, "Modelos copiados; el origen permanece intacto."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result(false, ex.Message));
        }
    }

    public Task<MigrationOperationResult> SwitchAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryDestination(step, out var destination, out var error)) return Task.FromResult(Result(false, error));
        try
        {
            host.SetUserEnvironmentVariable(EnvironmentVariable, destination);
            return Task.FromResult(Result(true, "OLLAMA_MODELS ahora apunta al destino."));
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result(false, ex.Message));
        }
    }

    public async Task<MigrationOperationResult> ValidateAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!TryDestination(step, out var destination, out var error)) return Result(false, error);
        if (!string.Equals(Normalize(host.GetUserEnvironmentVariable(EnvironmentVariable)), Normalize(destination), StringComparison.OrdinalIgnoreCase))
            return Result(false, "OLLAMA_MODELS no quedó configurada con el destino esperado.");

        try
        {
            if (!host.DirectoryContentsMatch(step.Item.SourcePath, destination))
                return Result(false, "Origen y destino no coinciden por ruta, tamaño y SHA-256.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, $"No se pudo verificar la copia: {ex.Message}");
        }

        if (!_sourceModels.TryGetValue(step.Id, out var expected))
            return Result(false, "No existe inventario de runtime del origen para validar la migración.");

        var probe = await host.ProbeAsync(destination, cancellationToken);
        if (!probe.Success) return Result(false, $"Ollama no pudo abrir los modelos desde el destino: {probe.Message}");
        var actual = NormalizeModels(probe.Models);
        if (!expected.SequenceEqual(actual))
            return Result(false, "El runtime de Ollama no expuso los mismos nombres y digest de modelos desde el destino.");

        return Result(true, $"Copia SHA-256 y runtime de Ollama validados para {actual.Count} modelos.");
    }

    public Task<MigrationOperationResult> CommitAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryDestination(step, out var destination, out var error)) return Task.FromResult(Result(false, error));
        if (!host.DirectoryExists(destination)) return Task.FromResult(Result(false, "El destino desapareció antes del commit."));
        try
        {
            host.DeleteDirectory(step.Item.SourcePath);
            return Task.FromResult(Result(true, "Origen eliminado después de validar Ollama contra el destino."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result(false, ex.Message));
        }
    }

    public Task<MigrationOperationResult> RollbackAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryDestination(step, out var destination, out var error)) return Task.FromResult(Result(false, error));
        try
        {
            if (!host.DirectoryExists(step.Item.SourcePath) && host.DirectoryExists(destination))
                host.CopyDirectory(destination, step.Item.SourcePath);
            if (_originalConfiguration.TryGetValue(step.Id, out var original))
                host.SetUserEnvironmentVariable(EnvironmentVariable, original);
            return Task.FromResult(Result(true, "OLLAMA_MODELS y el origen fueron restaurados; la copia de destino se conserva."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult(Result(false, ex.Message));
        }
    }

    private static IReadOnlyList<OllamaModelIdentity> NormalizeModels(IEnumerable<OllamaModelIdentity> models) =>
        models.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Digest, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool TryDestination(MigrationStep step, out string destination, out string error)
    {
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
    private static MigrationOperationResult Result(bool success, string message) => new(success, message);
}

public sealed class WindowsOllamaMigrationHost : IOllamaMigrationHost
{
    private sealed record FileFingerprint(long Length, string Sha256);
    private sealed record TagsResponse([property: JsonPropertyName("models")] OllamaTag[]? Models);
    private sealed record OllamaTag([property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("digest")] string? Digest);

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public long GetAvailableBytes(string path)
    {
        var root = Path.GetPathRoot(path);
        return string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
    }
    public bool IsRuntimeAvailable() => ResolveOllamaExecutable() is not null;
    public bool IsOllamaRunning() => Process.GetProcessesByName("ollama").Length > 0 || Process.GetProcessesByName("ollama app").Length > 0;
    public string? GetUserEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    public void SetUserEnvironmentVariable(string name, string? value) => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);

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
        var left = Snapshot(source);
        var right = Snapshot(destination);
        return left.Count == right.Count && left.All(x => right.TryGetValue(x.Key, out var value) && value == x.Value);
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    public async Task<OllamaRuntimeProbe> ProbeAsync(string modelsPath, CancellationToken cancellationToken = default)
    {
        var executable = ResolveOllamaExecutable();
        if (executable is null) return new(false, [], "ollama.exe no está disponible.");
        var port = ReserveLoopbackPort();
        var host = $"127.0.0.1:{port}";
        var start = new ProcessStartInfo(executable, "serve")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["OLLAMA_MODELS"] = modelsPath;
        start.Environment["OLLAMA_HOST"] = host;

        using var process = Process.Start(start);
        if (process is null) return new(false, [], "No se pudo iniciar el runtime temporal de Ollama.");
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://{host}"), Timeout = TimeSpan.FromSeconds(2) };
            for (var attempt = 0; attempt < 30; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited) return new(false, [], $"Ollama terminó antes del probe (exit {process.ExitCode}).");
                try
                {
                    var response = await client.GetFromJsonAsync<TagsResponse>("/api/tags", cancellationToken);
                    var models = (response?.Models ?? [])
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Digest))
                        .Select(x => new OllamaModelIdentity(x.Name!, x.Digest!))
                        .ToArray();
                    return new(true, models, "Runtime temporal respondió /api/tags.");
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                await Task.Delay(200, cancellationToken);
            }
            return new(false, [], "El runtime temporal no respondió /api/tags a tiempo.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static string? ResolveOllamaExecutable()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, "ollama.exe");
            if (File.Exists(candidate)) return candidate;
        }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultCandidate = Path.Combine(local, "Programs", "Ollama", "ollama.exe");
        return File.Exists(defaultCandidate) ? defaultCandidate : null;
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Dictionary<string, FileFingerprint> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file), Fingerprint, StringComparer.OrdinalIgnoreCase);

    private static FileFingerprint Fingerprint(string file)
    {
        using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new(stream.Length, Convert.ToHexString(SHA256.HashData(stream)));
    }
}

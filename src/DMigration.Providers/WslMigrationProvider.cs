using System.Diagnostics;
using System.Text;
using DMigration.Application;
using DMigration.Domain;

namespace DMigration.Providers;

public sealed record WslDistribution(string Name, int Version, bool IsDefault);
public sealed record WslDistributionIdentity(string User, string OsId, string Hostname);
public enum WslImportRole { TemporaryValidation, Final, Rollback }
public enum WslProbeRole { Original, TemporaryValidation, Final }

public interface IWslMigrationHost
{
    bool IsWslAvailable();
    IReadOnlyList<WslDistribution> ListDistributions();
    string? GetDefaultDistribution();
    Task TerminateAsync(string name, CancellationToken cancellationToken = default);
    Task ExportVhdAsync(string name, string vhdPath, CancellationToken cancellationToken = default);
    void CopyFile(string source, string destination);
    bool FileExists(string path);
    void DeleteFile(string path);
    Task ImportInPlaceAsync(string name, string vhdPath, WslImportRole role, CancellationToken cancellationToken = default);
    Task<WslDistributionIdentity> ProbeAsync(string name, WslProbeRole role, CancellationToken cancellationToken = default);
    Task UnregisterAsync(string name, CancellationToken cancellationToken = default);
    Task SetDefaultAsync(string name, CancellationToken cancellationToken = default);
}

public sealed class WslInventoryProvider(IWslMigrationHost host, string destinationDrive = "D:") : IInventoryProvider
{
    public string Name => "wsl";

    public Task<IReadOnlyList<InventoryItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || !host.IsWslAvailable())
            return Task.FromResult<IReadOnlyList<InventoryItem>>([]);

        var items = host.ListDistributions()
            .Select(d => new InventoryItem(
                $"wsl:{d.Name}",
                Name,
                $"WSL {d.Name}",
                $"wsl://{d.Name}",
                0,
                "wsl",
                RiskLevel.High,
                MigrationStrategy.ExportImport,
                Path.Combine(destinationDrive + Path.DirectorySeparatorChar, "Datos", "WSL", d.Name, "ext4.vhdx"),
                d.Version == 2,
                d.Version == 2
                    ? "WSL 2: exportar VHD, validar una importación temporal y conservar backup hasta commit."
                    : "WSL 1 permanece manual; --vhd sólo aplica a WSL 2."))
            .ToArray();
        return Task.FromResult<IReadOnlyList<InventoryItem>>(items);
    }
}

public sealed class WslMigrationProvider(IWslMigrationHost host) : IMigrationProvider
{
    private sealed record State(
        string Name,
        string BackupPath,
        string TemporaryPath,
        string TemporaryName,
        string FinalPath,
        bool WasDefault,
        WslDistributionIdentity Identity,
        bool OriginalUnregistered);

    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);
    public string Name => "wsl-export-import";

    public bool CanHandle(InventoryItem item) =>
        item.Id.StartsWith("wsl:", StringComparison.OrdinalIgnoreCase) &&
        item.Strategy == MigrationStrategy.ExportImport;

    public async Task<MigrationOperationResult> PreflightAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(step.Item) || string.IsNullOrWhiteSpace(step.Destination))
            return Fail("Paso WSL inválido.");
        if (!host.IsWslAvailable())
            return Fail("WSL no está disponible.");

        var name = DistributionName(step.Item);
        var distro = host.ListDistributions().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (distro is null) return Fail($"La distribución {name} ya no existe.");
        if (distro.Version != 2) return Fail("Sólo WSL 2 puede migrarse automáticamente mediante exportación VHD.");

        try
        {
            await host.TerminateAsync(name, cancellationToken);
            var identity = await host.ProbeAsync(name, WslProbeRole.Original, cancellationToken);
            await host.TerminateAsync(name, cancellationToken);
            var finalPath = step.Destination!;
            var root = Path.GetDirectoryName(finalPath)!;
            var backup = Path.Combine(root, ".staging", $"{Safe(name)}-{step.Id}-backup.vhdx");
            var temp = Path.Combine(root, ".staging", $"{Safe(name)}-{step.Id}-validate.vhdx");
            var tempName = $"DMigration-Validate-{Safe(name)}-{step.Id[..Math.Min(8, step.Id.Length)]}";
            _states[step.Id] = new State(name, backup, temp, tempName, finalPath, distro.IsDefault, identity, false);
            return Ok("WSL 2 listo para exportación segura.");
        }
        catch (Exception ex) { return Fail($"Preflight WSL falló: {ex.Message}"); }
    }

    public async Task<MigrationOperationResult> StageAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(step.Id, out var state)) return Fail("Falta preflight WSL.");
        try
        {
            await host.ExportVhdAsync(state.Name, state.BackupPath, cancellationToken);
            host.CopyFile(state.BackupPath, state.TemporaryPath);
            await host.ImportInPlaceAsync(state.TemporaryName, state.TemporaryPath, WslImportRole.TemporaryValidation, cancellationToken);
            var tempIdentity = await host.ProbeAsync(state.TemporaryName, WslProbeRole.TemporaryValidation, cancellationToken);
            await host.TerminateAsync(state.TemporaryName, cancellationToken);
            await host.UnregisterAsync(state.TemporaryName, cancellationToken);
            if (!SameIdentity(state.Identity, tempIdentity))
                return Fail("La copia WSL temporal no conserva la identidad básica de la distribución.");
            host.DeleteFile(state.TemporaryPath);
            return Ok("Backup exportado y copia temporal validada antes de unregister.");
        }
        catch (Exception ex)
        {
            try { await host.UnregisterAsync(state.TemporaryName, cancellationToken); } catch { }
            return Fail($"Validación temporal WSL falló: {ex.Message}");
        }
    }

    public async Task<MigrationOperationResult> SwitchAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(step.Id, out var state)) return Fail("Falta estado WSL.");
        try
        {
            host.CopyFile(state.BackupPath, state.FinalPath);
            await host.UnregisterAsync(state.Name, cancellationToken);
            _states[step.Id] = state = state with { OriginalUnregistered = true };
            await host.ImportInPlaceAsync(state.Name, state.FinalPath, WslImportRole.Final, cancellationToken);
            if (state.WasDefault) await host.SetDefaultAsync(state.Name, cancellationToken);
            return Ok("Distribución registrada bajo su nombre original desde D:.");
        }
        catch (Exception ex) { return Fail($"Switch WSL falló: {ex.Message}"); }
    }

    public async Task<MigrationOperationResult> ValidateAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(step.Id, out var state)) return Fail("Falta estado WSL.");
        try
        {
            var finalIdentity = await host.ProbeAsync(state.Name, WslProbeRole.Final, cancellationToken);
            await host.TerminateAsync(state.Name, cancellationToken);
            if (!SameIdentity(state.Identity, finalIdentity))
                return Fail("La distribución final no conserva usuario, OS ID y hostname esperados.");
            if (state.WasDefault && !string.Equals(host.GetDefaultDistribution(), state.Name, StringComparison.OrdinalIgnoreCase))
                return Fail("No se preservó la distribución WSL predeterminada.");
            return Ok("Distribución final arrancó y conservó identidad básica.");
        }
        catch (Exception ex) { return Fail($"Validación WSL final falló: {ex.Message}"); }
    }

    public Task<MigrationOperationResult> CommitAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(step.Id, out var state)) return Task.FromResult(Fail("Falta estado WSL."));
        try
        {
            if (host.FileExists(state.BackupPath)) host.DeleteFile(state.BackupPath);
            if (host.FileExists(state.TemporaryPath)) host.DeleteFile(state.TemporaryPath);
            _states.Remove(step.Id);
            return Task.FromResult(Ok("Backup WSL retirado después de validación final."));
        }
        catch (Exception ex) { return Task.FromResult(Fail($"Commit WSL falló: {ex.Message}")); }
    }

    public async Task<MigrationOperationResult> RollbackAsync(MigrationStep step, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(step.Id, out var state)) return Fail("No existe estado WSL para rollback.");
        try
        {
            try { await host.UnregisterAsync(state.TemporaryName, cancellationToken); } catch { }
            if (state.OriginalUnregistered)
            {
                try { await host.UnregisterAsync(state.Name, cancellationToken); } catch { }
                if (!host.FileExists(state.BackupPath)) return Fail("El VHDX de backup WSL no está disponible para rollback.");
                await host.ImportInPlaceAsync(state.Name, state.BackupPath, WslImportRole.Rollback, cancellationToken);
                if (state.WasDefault) await host.SetDefaultAsync(state.Name, cancellationToken);
            }
            return Ok("Rollback WSL completado; el backup se conserva.");
        }
        catch (Exception ex) { return Fail($"Rollback WSL falló: {ex.Message}"); }
    }

    private static string DistributionName(InventoryItem item) => item.Id[4..];
    private static string Safe(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    private static bool SameIdentity(WslDistributionIdentity left, WslDistributionIdentity right) =>
        string.Equals(left.User, right.User, StringComparison.Ordinal) &&
        string.Equals(left.OsId, right.OsId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Hostname, right.Hostname, StringComparison.OrdinalIgnoreCase);
    private static MigrationOperationResult Ok(string message) => new(true, message);
    private static MigrationOperationResult Fail(string message) => new(false, message);
}

public sealed class WindowsWslMigrationHost : IWslMigrationHost
{
    public bool IsWslAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { Run(["--status"], throwOnError: false); return true; }
        catch { return false; }
    }

    public IReadOnlyList<WslDistribution> ListDistributions()
    {
        var output = Clean(Run(["--list", "--verbose"]));
        var result = new List<WslDistribution>();
        foreach (var raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("NAME", StringComparison.OrdinalIgnoreCase)) continue;
            var isDefault = line.StartsWith('*');
            if (isDefault) line = line[1..].TrimStart();
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[^1], out var version)) continue;
            var name = string.Join(' ', parts[..^2]);
            result.Add(new WslDistribution(name, version, isDefault));
        }
        return result;
    }

    public string? GetDefaultDistribution() => ListDistributions().FirstOrDefault(x => x.IsDefault)?.Name;

    public Task TerminateAsync(string name, CancellationToken cancellationToken = default) =>
        RunAsync(["--terminate", name], cancellationToken);

    public Task ExportVhdAsync(string name, string vhdPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(vhdPath)!);
        return RunAsync(["--export", name, vhdPath, "--vhd"], cancellationToken);
    }

    public void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
    }

    public bool FileExists(string path) => File.Exists(path);
    public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }

    public Task ImportInPlaceAsync(string name, string vhdPath, WslImportRole role, CancellationToken cancellationToken = default) =>
        RunAsync(["--import-in-place", name, vhdPath], cancellationToken);

    public async Task<WslDistributionIdentity> ProbeAsync(string name, WslProbeRole role, CancellationToken cancellationToken = default)
    {
        const string script = "printf '%s\\n' \"$(id -un)\" \"$(. /etc/os-release 2>/dev/null; printf %s \\\"${ID:-unknown}\\\")\" \"$(hostname)\"";
        var output = Clean(await RunCaptureAsync(["-d", name, "--", "sh", "-lc", script], cancellationToken));
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 3) throw new InvalidOperationException("WSL probe no devolvió identidad completa.");
        return new WslDistributionIdentity(lines[0], lines[1], lines[2]);
    }

    public Task UnregisterAsync(string name, CancellationToken cancellationToken = default) =>
        RunAsync(["--unregister", name], cancellationToken);

    public Task SetDefaultAsync(string name, CancellationToken cancellationToken = default) =>
        RunAsync(["--set-default", name], cancellationToken);

    private static string Run(IReadOnlyList<string> args, bool throwOnError = true)
    {
        using var process = Start(args);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (throwOnError && process.ExitCode != 0) throw new InvalidOperationException(Clean(stderr));
        return stdout;
    }

    private static async Task RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        _ = await RunCaptureAsync(args, cancellationToken);
    }

    private static async Task<string> RunCaptureAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        using var process = Start(args);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(Clean(stderr));
        return stdout;
    }

    private static Process Start(IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo("wsl.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.Unicode,
            StandardErrorEncoding = Encoding.Unicode
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar wsl.exe.");
    }

    private static string Clean(string value) => value.Replace("\0", string.Empty).Trim();
}

# D-Migration

D-Migration is a Windows-first storage migration manager focused on keeping the system drive lean without blindly moving application folders.

The project is being rebuilt as a modular .NET 10 CLI. It inventories user data and developer tooling, assigns a migration strategy and risk level, creates an explicit plan, journals that plan, and only allows execution when a provider can guarantee a supported and verifiable migration path.

## Current architecture

```text
src/
  DMigration.Domain/                  Core records, policies and destination layout
  DMigration.Application/             Inventory, planning, doctor and execution use-cases
  DMigration.Infrastructure.Windows/  Windows known folders, filesystem and journal adapters
  DMigration.Providers/               Developer-tool detection and technology policies
  DMigration.Cli/                     Composition root and terminal UI

tests/
  DMigration.Tests/
```

The legacy PowerShell, BAT and Python scripts remain in the repository temporarily as reference material. The new CLI does **not** invoke the destructive legacy migration flows.

## Safety model

D-Migration is intentionally conservative:

- scanning is read-only;
- unknown items remain on `C:`;
- `scan` and `plan` never move or delete files;
- dry-run is the default behavior;
- unsupported migrations are marked manual/non-executable;
- source deletion is not allowed before provider-specific validation;
- WSL is classified for export/import rather than direct VHDX movement;
- Visual Studio is treated as installer-managed software;
- package-manager caches prefer official configuration over junctions;
- administrator elevation is not requested globally.

The current increment keeps destructive execution disabled until providers implement their own preflight, validation and rollback contracts.

## Quick start

Requirements: Windows and the .NET 10 SDK.

```powershell
git clone https://github.com/MonkDabaLegaro/D-Migration.git
cd D-Migration
.\run.ps1
```

You can also invoke commands directly:

```powershell
.\run.ps1 scan
.\run.ps1 plan
.\run.ps1 doctor
.\run.ps1 apply <plan-id>
.\run.ps1 rollback <plan-id>
```

`apply <plan-id>` is a dry-run unless `--execute` is explicitly provided. Even with `--execute`, the application rejects plans containing providers that are not yet safely executable.

## Default D: layout

```text
D:\
  Componentes\
    Aplicaciones\
    Desarrollo\
      IDEs\
      SDKs\
      Toolchains\
      Containers\
  Librerias\
    Python\
    Node\
    NuGet\
    Cargo\
    Gradle\
    Maven\
  Datos\
    Databases\
    Docker\
    WSL\
    AI\
      Ollama\
      HuggingFace\
  Descargas\
  Documentos\
  Fotos y videos\
  Musica\
  Games\
  .d-migration\
    backups\
    logs\
    staging\
    state\
```

Set `DMIGRATION_DESTINATION_DRIVE` to use another destination drive.

## Initial detection coverage

| Area | Detection | Strategy |
|---|---|---|
| Downloads/Documents/Pictures/Videos/Music | Yes | Windows known-folder redirect |
| pip cache | Yes | Configuration change |
| npm cache | Yes | Configuration change |
| pnpm data | Yes | Configuration change |
| Ollama models | Yes | Configuration change |
| Hugging Face cache | Yes | Configuration change |
| Docker Desktop data | Yes | Docker-managed data-root migration |
| WSL distributions | Yes | Export/import |
| Visual Studio | Yes | Installer-managed reinstall/configuration |
| VS Code extensions | Yes | Configuration change |

## Commands

- `scan`: inventories supported locations and reports size, risk and strategy.
- `plan`: creates and journals a migration plan without changing the machine.
- `doctor`: reports detected development tooling and the strategy required to cleanly relocate it.
- `apply`: validates a stored plan; actual mutation remains provider-gated.
- `rollback`: reserved for journaled provider rollbacks once mutation providers are enabled.

## Design documentation

- `docs/superpowers/specs/2026-08-25-modular-storage-manager-design.md`
- `docs/superpowers/plans/2026-08-25-modular-storage-manager.md`

## Development

```powershell
dotnet build src/DMigration.Cli/DMigration.Cli.csproj
dotnet test tests/DMigration.Tests/DMigration.Tests.csproj
```

GitHub Actions runs the same build and test flow on Windows with .NET 10.

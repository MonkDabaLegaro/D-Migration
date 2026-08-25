# D-Migration

D-Migration is a Windows-first storage migration manager focused on keeping the system drive lean without blindly moving application folders.

The project is being rebuilt as a modular .NET 10 CLI. It inventories user data and developer tooling, assigns a migration strategy and risk level, creates an explicit plan, journals that plan, and only allows execution when a provider can guarantee a supported and verifiable migration path.

## Current architecture

```text
src/
  DMigration.Domain/                  Core records, policies and destination layout
  DMigration.Application/             Inventory, planning, doctor and execution use-cases
  DMigration.Infrastructure.Windows/  Windows known folders, filesystem and journal adapters
  DMigration.Providers/               Developer-tool discovery and migration providers
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
- executable migration follows `preflight -> stage -> switch -> validate -> commit`;
- staging copies data while preserving the source;
- source deletion happens only in commit, after configuration and copied contents have been validated;
- failures after switch trigger automatic rollback during the same `apply` execution;
- rollback restores the original user configuration and recreates the source if needed while preserving the destination copy;
- WSL remains export/import-only and is not automatically moved yet;
- Visual Studio remains installer-managed and is not automatically moved yet;
- Docker Desktop remains provider-specific and is not automatically moved yet;
- Ollama remains manual until its provider validates the runtime against the relocated model store;
- administrator elevation is not requested globally.

## Quick start

Requirements: Windows and the .NET 10 SDK.

```powershell
git clone https://github.com/MonkDabaLegaro/D-Migration.git
cd D-Migration
.\run.ps1
```

Useful commands:

```powershell
.\run.ps1 scan
.\run.ps1 plan
.\run.ps1 plan --safe
.\run.ps1 doctor
.\run.ps1 apply <plan-id>
.\run.ps1 apply <safe-plan-id> --execute
.\run.ps1 rollback <plan-id>
```

`plan` creates a complete review plan including manual items. `plan --safe` creates a separate plan containing only items that currently have an executable migration provider. `apply` is still a dry-run unless `--execute` is explicitly supplied.

Persistent/manual rollback across separate program executions is not enabled yet. Automatic rollback during the active `apply` transaction is enabled.

## First executable providers

The first real migration provider handles regenerable caches whose owning tool supports a user-level environment variable for relocating its data:

| Item | Configuration switched by D-Migration | Automatic |
|---|---|---:|
| pip cache | `PIP_CACHE_DIR` | Yes |
| npm cache | `NPM_CONFIG_CACHE` | Yes |
| Hugging Face cache | `HF_HOME` | Yes |
| Ollama models | `OLLAMA_MODELS`; runtime validation still required | No |
| pnpm | Dedicated store provider still required | No |
| Docker Desktop | Dedicated Docker provider required | No |
| WSL distributions | Export/import provider required | No |
| Visual Studio | Visual Studio Installer provider required | No |
| VS Code extensions | Dedicated configuration provider required | No |

For an automatic directory migration D-Migration verifies available destination space, refuses a pre-existing destination, copies without deleting the source, changes the owning configuration, compares relative file names and sizes, and only then removes the source.

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

## Detection coverage

| Area | Detection | Strategy |
|---|---|---|
| Downloads/Documents/Pictures/Videos/Music | Yes | Windows known-folder redirect, manual for now |
| pip cache | Yes | Transactional configuration change |
| npm cache | Yes | Transactional configuration change |
| pnpm data | Yes | Manual until store semantics are handled separately |
| Ollama models | Yes | Configuration change plus runtime probe, manual for now |
| Hugging Face cache | Yes | Transactional configuration change |
| Docker Desktop data | Yes | Docker-managed data-root migration, manual for now |
| WSL distributions | Yes | Export/import, manual for now |
| Visual Studio | Yes | Installer-managed reinstall/configuration, manual for now |
| VS Code extensions | Yes | Dedicated configuration provider pending |

## Commands

- `scan`: inventories supported locations and reports size, risk and strategy.
- `plan`: creates and journals a complete migration review plan without changing the machine.
- `plan --safe`: creates a journal containing only currently executable provider-backed operations.
- `doctor`: reports detected development tooling and the strategy required to cleanly relocate it.
- `apply <id>`: dry-runs a stored plan.
- `apply <id> --execute`: executes a safe plan through the staged provider lifecycle.
- `rollback <id>`: currently reports status only; persistent cross-process rollback is the next journal increment.

## Design documentation

- `docs/superpowers/specs/2026-08-25-modular-storage-manager-design.md`
- `docs/superpowers/plans/2026-08-25-modular-storage-manager.md`

## Development

```powershell
dotnet build src/DMigration.Cli/DMigration.Cli.csproj
dotnet test tests/DMigration.Tests/DMigration.Tests.csproj
```

GitHub Actions runs the same build and test flow on Windows with .NET 10.

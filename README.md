# D-Migration

D-Migration is a Windows-first storage migration manager focused on keeping the system drive lean without blindly moving application folders.

The project is being rebuilt as a modular .NET 10 CLI. It inventories user data and developer tooling, assigns a migration strategy and risk level, creates an explicit plan, journals that plan, and only allows execution when a provider can guarantee a supported and verifiable migration path.

## Current architecture

```text
src/
  DMigration.Domain/                  Core records, policies and destination layout
  DMigration.Application/             Inventory, planning, doctor and execution use-cases
  DMigration.Infrastructure.Windows/  Windows Known Folder APIs, filesystem and journal adapters
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
- validation compares relative paths, file sizes and SHA-256 content hashes where directory providers are used;
- source deletion happens only in commit, after configuration and copied contents have been validated;
- failures after switch trigger automatic rollback during the same `apply` execution;
- rollback restores the original user configuration or Known Folder path and recreates the source if needed while preserving the destination copy;
- Known Folders are redirected through the Windows Shell Known Folder API instead of direct registry edits;
- Known Folders already managed by OneDrive/cloud remain manual;
- Ollama requires the desktop/runtime process to be closed, validates both stores with isolated temporary runtimes, and compares model names plus digests before commit;
- WSL 2 uses documented `--export --vhd` / `--import-in-place` flows, validates a temporary imported VHD before unregistering the original, and keeps an independent backup until final validation;
- WSL 1 remains manual;
- Visual Studio remains installer-managed and is not automatically moved yet;
- Docker Desktop remains provider-specific and is not automatically moved yet;
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

## Executable providers

| Item | Configuration / Windows mechanism | Automatic |
|---|---|---:|
| Downloads | Windows Known Folder API | Yes, unless cloud-managed |
| Documents | Windows Known Folder API | Yes, unless cloud-managed |
| Pictures | Windows Known Folder API | Yes, unless cloud-managed |
| Videos | Windows Known Folder API | Yes, unless cloud-managed |
| Music | Windows Known Folder API | Yes, unless cloud-managed |
| pip cache | `PIP_CACHE_DIR` | Yes |
| npm cache | `NPM_CONFIG_CACHE` | Yes |
| Hugging Face cache | `HF_HOME` | Yes |
| Ollama models | `OLLAMA_MODELS` + isolated `/api/tags` runtime probe | Yes, when Ollama is closed |
| WSL 2 distributions | `wsl --export --vhd` + temporary/final `--import-in-place` validation | Yes |
| WSL 1 distributions | Export/import VHD is not supported | No |
| pnpm | Dedicated store provider still required | No |
| Docker Desktop | Dedicated Docker provider required | No |
| Visual Studio | Visual Studio Installer provider required | No |
| VS Code extensions | Dedicated configuration provider required | No |

For automatic directory migrations D-Migration verifies available destination space, refuses a pre-existing destination, copies without deleting the source, switches the owning configuration/path, verifies each copied file by relative path, length and SHA-256 hash, and only then removes the source.

For Windows Known Folders, preflight also checks that the plan still matches the path Windows currently reports and rejects OneDrive/cloud-managed locations instead of fighting a sync provider.

For Ollama, preflight refuses to run while Ollama is active. It launches an isolated temporary `ollama serve` against the original store and records the model names/digests returned by `/api/tags`. After staging and setting the user-level `OLLAMA_MODELS`, validation verifies the byte-for-byte copy and launches a second isolated runtime against the destination. The source is deleted only when the destination runtime exposes the same model identities and digests.

For WSL 2, D-Migration treats each distribution as a logical inventory item rather than guessing its internal package/VHD path from undocumented registry state. Staging exports an independent backup VHDX, copies it to a validation VHDX, registers that copy under a temporary distribution name, boots it, and compares user, OS identity and hostname. Only after that succeeds can the original distribution be unregistered. The final VHDX is registered under the original distribution name and the default-distribution selection is restored when applicable. If final import or validation fails, rollback re-registers the retained backup. The backup is removed only at commit. Because backup and validation/final VHDX coexist during the transaction, sufficient temporary free space on the destination is required. WSL 1 remains manual.

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
      <Distribution>\
        ext4.vhdx
    AI\
      Ollama\
      HuggingFace\
  Descargas\
  Documentos\
  Fotos y videos\
    Fotos\
    Videos\
  Musica\
  Games\
  .d-migration\
    backups\
    logs\
    staging\
    state\
```

Pictures and Videos intentionally use separate subdirectories so the two Windows Known Folders never share a destination or rollback boundary.

Set `DMIGRATION_DESTINATION_DRIVE` to use another destination drive.

## Detection coverage

| Area | Detection | Strategy |
|---|---|---|
| Downloads/Documents/Pictures/Videos/Music | Yes | Transactional Windows Known Folder redirect; cloud-managed paths remain manual |
| pip cache | Yes | Transactional configuration change |
| npm cache | Yes | Transactional configuration change |
| pnpm data | Yes | Manual until store semantics are handled separately |
| Ollama models | Yes | Transactional configuration change + isolated runtime/digest validation |
| Hugging Face cache | Yes | Transactional configuration change |
| WSL distributions | Yes, per distribution | WSL 2 transactional VHD export/import; WSL 1 manual |
| Docker Desktop data | Yes | Docker-managed data-root migration, manual for now |
| Visual Studio | Yes | Installer-managed reinstall/configuration, manual for now |
| VS Code extensions | Yes | Dedicated configuration provider pending |

## Commands

- `scan`: inventories supported locations and reports size, risk and strategy. WSL distributions report unknown size rather than relying on undocumented VHD paths.
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

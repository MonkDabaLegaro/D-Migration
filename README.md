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
- staging preserves the source;
- source deletion happens only in commit after validation;
- failures after switch trigger automatic rollback during the same `apply` execution;
- Known Folders use the Windows Shell API and cloud-managed folders remain manual;
- Ollama validates model names/digests with isolated runtimes before commit;
- WSL 2 validates an imported backup before unregistering the original and keeps a backup through final validation;
- WSL 1 remains manual;
- Docker Desktop WSL2 is detected by a dedicated provider, but existing installations remain assisted/manual unless Docker exposes a supported programmatic relocation channel;
- Docker configuration internals such as undocumented `settings-store.json` keys are never treated as a public API;
- Visual Studio remains installer-managed;
- administrator elevation is not requested globally.

## Quick start

Requirements: Windows and the .NET 10 SDK.

```powershell
git clone https://github.com/MonkDabaLegaro/D-Migration.git
cd D-Migration
.\run.ps1
```

```powershell
.\run.ps1 scan
.\run.ps1 plan
.\run.ps1 plan --safe
.\run.ps1 doctor
.\run.ps1 apply <plan-id>
.\run.ps1 apply <safe-plan-id> --execute
.\run.ps1 rollback <plan-id>
```

`plan` includes manual review items. `plan --safe` contains only provider-backed operations that the current host declares executable. `apply` remains dry-run unless `--execute` is explicitly supplied.

## Provider status

| Item | Mechanism | Automatic |
|---|---|---:|
| Downloads/Documents/Pictures/Videos/Music | Windows Known Folder API | Yes, unless cloud-managed |
| pip cache | `PIP_CACHE_DIR` | Yes |
| npm cache | `NPM_CONFIG_CACHE` | Yes |
| Hugging Face cache | `HF_HOME` | Yes |
| Ollama models | `OLLAMA_MODELS` + isolated `/api/tags` probe | Yes, when Ollama is closed |
| WSL 2 distributions | `wsl --export --vhd` + temporary/final `--import-in-place` validation | Yes |
| WSL 1 distributions | VHD workflow unsupported | No |
| pnpm | Dedicated store semantics pending | No |
| Docker Desktop WSL2 | Dedicated backend/data-root provider + runtime inventory contract | Assisted on current Windows host |
| Docker Desktop Hyper-V | Backend-specific provider pending | No |
| Visual Studio | Visual Studio Installer provider pending | No |
| VS Code extensions | Dedicated provider pending | No |

### Docker Desktop safety boundary

Docker Desktop documents changing **Disk image location** from `Settings > Resources > Advanced`, and documents `--wsl-default-data-root` for installer-time configuration. It does not document a stable contract for mutating the corresponding setting of an existing installation by editing `settings-store.json` directly.

D-Migration therefore detects Docker Desktop WSL2 and its default `%LOCALAPPDATA%\Docker\wsl` data root, assigns the target `D:\Datos\Docker\wsl`, and reports the migration as assisted/manual on the production Windows host. It is deliberately excluded from `plan --safe` while no supported relocation API is available.

The Docker provider contract and tests already define the future executable lifecycle: capture image/volume inventory while stopped, stage without deleting the source, relocate through a supported host operation, start Docker and wait for the engine, compare image and volume identities, and only then remove the old data root. A missing image or volume fails validation and preserves the source. This contract is not enabled by faking an undocumented settings mutation.

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
      wsl\
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

Set `DMIGRATION_DESTINATION_DRIVE` to use another destination drive.

## Commands

- `scan`: inventories supported locations and reports risk/strategy.
- `plan`: creates and journals a complete review plan.
- `plan --safe`: journals only currently executable provider-backed operations.
- `doctor`: reports detected tooling and required relocation strategy.
- `apply <id>`: dry-runs a stored plan.
- `apply <id> --execute`: executes a safe plan through the staged lifecycle.
- `rollback <id>`: persistent cross-process rollback remains pending; active `apply` rollback is automatic.

## Design documentation

- `docs/superpowers/specs/2026-08-25-modular-storage-manager-design.md`
- `docs/superpowers/plans/2026-08-25-modular-storage-manager.md`

## Development

```powershell
dotnet build src/DMigration.Cli/DMigration.Cli.csproj
dotnet test tests/DMigration.Tests/DMigration.Tests.csproj
```

GitHub Actions runs the same build and test flow on Windows with .NET 10.

# D-Migration Modular Storage Manager Design

## Goal

Turn D-Migration from machine-specific scripts into a Windows-first modular monolith that inventories storage, classifies movable data, builds explicit migration plans, applies only provider-supported operations, validates them, journals state, and supports rollback where a provider can guarantee it.

## Product model

D-Migration is not a generic folder mover. A discovered item is represented as inventory with provenance, size, category, risk, migration strategy, destination recommendation, and provider ownership. Migration follows `Detected -> Analyzed -> Planned -> PreflightPassed -> Staged -> Verified -> Switched -> Validated -> Committed`; source deletion is only allowed after validation.

## Runtime and UX

- Windows-first CLI.
- .NET 10 LTS.
- Spectre.Console for interactive terminal UX.
- One distributable application, internally split into focused projects/modules.
- `run.ps1` bootstraps local execution after clone.
- Commands: `scan`, `plan`, `doctor`, `apply`, `rollback`.
- No global administrator elevation. Providers request elevation only when the operation actually needs it.

## Modules

- `DMigration.Domain`: immutable domain types and policy enums.
- `DMigration.Application`: scan, plan, doctor, execution and rollback use-cases plus provider contracts.
- `DMigration.Infrastructure.Windows`: Windows filesystem, known-folder, process, environment and journal adapters.
- `DMigration.Providers`: technology-specific providers.
- `DMigration.Cli`: composition root and terminal presentation.

## Provider contract

Inventory providers discover items. Migration providers own compatibility decisions for a technology. Every migration-capable provider exposes detection, plan construction, preflight, execution, validation and rollback. Generic file discovery never implies generic relocation.

Initial provider set:

1. Windows known folders.
2. Large user directories.
3. Python/pip cache.
4. Node/npm cache.
5. Ollama models.
6. WSL discovery.
7. Docker discovery.
8. Visual Studio discovery.

The initial increment may mark complex technologies as `Manual` or `Protected` while still detecting them and explaining the supported strategy. This is preferable to unsafe automatic movement.

## Destination layout

The default preset is configurable and uses:

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

## Safety rules

- Unknown items remain on C by default.
- No recursive delete before post-migration validation.
- No swallowed errors in application orchestration.
- Every executable migration produces a journal record.
- `apply` is explicit and never implied by `scan` or `plan`.
- Dry-run is the default execution mode.
- Junctions are a provider strategy of last resort, never the generic strategy.
- WSL uses export/import semantics rather than moving VHDX files directly.
- Visual Studio is treated as installer-managed software rather than a movable folder.
- Package-manager caches prefer official configuration over links.

## Testing

Domain policies and plan construction are unit tested. Providers expose deterministic metadata that can be tested without mutating the host. Host-mutating integration tests are separated and are not run implicitly on developer machines.

## Legacy transition

Existing PowerShell, BAT and Python scripts remain temporarily as reference material. New CLI flows do not invoke destructive legacy scripts. They can be removed after equivalent provider behavior is implemented and covered by tests.

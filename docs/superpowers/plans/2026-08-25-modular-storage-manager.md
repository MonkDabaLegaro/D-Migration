# D-Migration Modular Storage Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the first safe, testable .NET 10 modular-monolith version of D-Migration with inventory, planning, diagnostics, journaling and provider-based migration boundaries.

**Architecture:** Keep a single CLI product with separate Domain, Application, Windows Infrastructure and Providers projects. Discovery is read-only; planning is explicit; execution is opt-in and restricted to providers that support validated operations. Legacy destructive scripts remain disconnected from the new runtime.

**Tech Stack:** .NET 10, C# 13, Spectre.Console, System.Text.Json, xUnit, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-25-modular-storage-manager-design.md`

## Global Constraints

- Windows-first CLI.
- .NET 10 LTS.
- No global administrator elevation.
- Unknown items remain on C by default.
- No recursive delete before post-migration validation.
- Every executable migration produces a journal record.
- Dry-run is the default execution mode.
- Junctions are never the generic migration strategy.
- Legacy destructive scripts are not invoked by the new CLI.

---

### Task 1: Solution foundation and domain model

**Files:** create project files under `src/` and `tests/`; create `Directory.Build.props`; create core domain records and enums.

- [ ] Add failing domain tests for destination recommendation and plan state.
- [ ] Add minimal domain implementation.
- [ ] Add project references and build configuration.
- [ ] Commit as an independently buildable foundation.

### Task 2: Inventory and planning application services

**Files:** `src/DMigration.Application/*`, application tests.

- [ ] Define `IInventoryProvider`, `IMigrationProvider`, `IJournalStore`.
- [ ] Implement `InventoryService`, `PlanningService`, `DoctorService`.
- [ ] Verify duplicate discoveries are normalized by provider/id and planning never marks unsupported items executable.
- [ ] Commit application layer.

### Task 3: Windows and developer-tool providers

**Files:** `src/DMigration.Infrastructure.Windows/*`, `src/DMigration.Providers/*`.

- [ ] Discover Windows known folders and large user folders read-only.
- [ ] Detect pip/npm caches and Ollama models from environment/default locations.
- [ ] Detect WSL, Docker and Visual Studio and classify them with explicit safe/manual strategies.
- [ ] Add JSON journal store under `D:\.d-migration\state` with local-app-data fallback.
- [ ] Commit providers and infrastructure.

### Task 4: CLI and bootstrap

**Files:** `src/DMigration.Cli/*`, `run.ps1`.

- [ ] Implement `scan`, `plan`, `doctor`, `apply`, `rollback` command routing.
- [ ] Render inventory and plans with Spectre.Console.
- [ ] Keep `apply` dry-run unless `--execute` is explicitly supplied; reject unsupported plan steps.
- [ ] Add clone-and-run PowerShell bootstrap.
- [ ] Commit CLI.

### Task 5: Documentation and CI

**Files:** `README.md`, `.github/workflows/ci.yml`, `.gitignore`.

- [ ] Document product scope, safety model, architecture, commands, destination layout and provider support matrix.
- [ ] Build and test on Windows in GitHub Actions with .NET 10.
- [ ] Ensure legacy scripts are clearly marked as legacy/reference and are not called by the new bootstrap.
- [ ] Commit docs and CI.

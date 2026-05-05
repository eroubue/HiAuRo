# HiAuRo Agent Guide

## What This Is

HiAuRo is a FFXIV Dalamud **combat assist framework** (.NET 10, Dalamud.NET.Sdk 15.0.0). It is NOT a job rotation — it provides the runtime, data layer, and ACR interfaces for ACR authors to build job rotations on top.

## Build & Verify

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

No `.sln` — single project. CEF renderer (`HiAuRo.Renderer`) is a separate `.csproj` added in Phase 5.3.

## Architecture Rules

**These are non-negotiable across all phases:**

1. Keep code flat and direct. Prefer existing APIs over wrapper layers.
2. No premature abstraction — don't build for hypothetical future needs.
3. Use Chinese comments for maintenance and collaboration.
4. New capabilities are **additive** — never rewrite familiar workflows.
5. Before Phase 7 (Fact Axis), keep ACR interfaces and author experience close to AEAssist.

## Technology Choices

- **OmenTools** (NOT ECommons) — `DService.Init(pluginInterface)` / `DService.Uninit()` for lifecycle. All Dalamud services accessed via `DService.*`. ImGuiOm for ImGui wrappers.
- **CEF + Web UI** (NOT ImGui for panels) — Kestrel HTTP + WebSocket (`localhost:5678`), HTML/CSS/JS frontend. CEF renders in-game via D3D11 shared textures (adapted from Browsingway). Browser dev at `localhost:5678`, no game needed for UI work.
- **No AEAssist runtime dependency** — HiAuRo is a standalone Dalamud plugin.

## Project Layout

```
/mnt/d/HiAuRo/          ← git repo root
├── doc/                ← all planning docs (READ BEFORE CODING)
│   ├── PROJECT.md      ← charter, constraints, key decisions
│   ├── REQUIREMENTS.md ← 46 requirement IDs with traceability
│   ├── ROADMAP.md      ← 9-phase roadmap with plan details
│   ├── ARCHITECTURE.md ← layered design, data flow, interfaces
│   ├── STACK.md        ← tech stack & dependency versions
│   ├── dev-tasks/      ← per-phase task breakdowns (PHASE1~PHASE5.4)
│   ├── OMEN_TOOLS_USAGE.md  ← what OmenTools provides vs what we build
│   └── AEASSIST_STUDY.md    ← AEAssist architecture reference
└── HiAuRo/             ← plugin source (TO BE CREATED per dev-tasks)
```

## Reference Code (read-only, do not modify)

| Path | What |
|------|------|
| `/mnt/d/ACR/HiAuRo/HiAuRo/资料/AEAssist/` | AEAssist decompiled source (host framework reference) |
| `/mnt/d/ACR/Oblivion/` | BLM ACR example (AE-style IRotationEntry usage) |
| `/home/ooooozzooo/OmenTools/` | OmenTools source (DService, OmenService managers) |
| `/home/ooooozzooo/Browsingway/` | CEF rendering reference (D3D11 texture sharing) |

## Phase Development Order

Development follows strict bottom-up order. Start at Phase 1, complete all tasks, then move to the next.

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5.1 → (5.2 ∥ 5.3) → Phase 5.4  ← MVP
                                                                              │
                                                                        Phase 6 → 7 → 8 → 9
```

**Before starting any phase**, read the corresponding `doc/dev-tasks/PHASE*_*.md` which contains the exact file manifest, tasks, and verification steps.

## Key Conventions

- **File naming**: Chinese-friendly, direct names. `BRD_GCD_强力射击.cs` not `BRDGCD.cs`. `BRDHelp.cs` not `BardDataAccessor.cs`.
- **ACR author interfaces**: `IRotationEntry` is the single entry point. `ISlotResolver` has `int Check()` + `void Build(Slot slot)`. `Slot` is an execution unit containing `List<SlotAction>`.
- **UI**: ACR authors use `IRotationUI.RegisterControls(IUiBuilder)` with declarative C# methods (AddCheckbox, AddDropdown, AddHotkey, AddTab, AddGroup, etc.). HiAuRo translates to JSON → web frontend renders HTML. Or authors set `IRotationEntry.UseCustomUi = true` and provide their own HTML files.
- **EventHandler**: 10 callback methods (OnPreCombat, OnResetBattle, OnNoTarget, OnBattleUpdate, OnSpellCastSuccess, BeforeSpell, AfterSpell, OnEnterRotation, OnExitRotation, OnTerritoryChanged). Fired by AIRunner synchronously (not via EventSystem hooks).

## Common Pitfalls

- **Don't use `Svc.ClientState.LocalPlayer`** — Dalamud API marks it obsolete. Use `IObjectTable.LocalPlayer` (live object) + `IPlayerState` (profile).
- **Don't iterate `IPartyMember.GameObject` multiple times** — expensive. Resolve once per scan.
- **Don't classify enemies by `BattleNpcSubKind.Enemy` alone** — some solo-duty allies also carry this flag. Must also check `ObjectKind`, `OwnerId`, `BuddyList`, `IsTargetable`.
- **Don't add wrapper layers around OmenTools** — DService is already the service locator. `HiAuRo.Data` is a thin forwarding facade, not a repository.

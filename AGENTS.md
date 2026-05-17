# HiAuRo Agent Guide

## What This Is

HiAuRo is a FFXIV Dalamud **combat assist framework** (.NET 10, Dalamud.NET.Sdk 15.0.0). It is NOT a job rotation — it provides the runtime, data layer, and ACR interfaces for ACR authors to build job rotations on top.

## Build & Verify

```bash
dotnet build HiAuRo.slnx -c Release -nologo
```


## Architecture Rules

**These are non-negotiable across all phases:**

1. Keep code flat and direct. Prefer existing APIs over wrapper layers.
2. No premature abstraction — don't build for hypothetical future needs.
3. Use Chinese comments for maintenance and collaboration.
4. New capabilities are **additive** — never rewrite familiar workflows.
5. ACR interfaces stay close to AEAssist conventions for ACR author familiarity.


## Project Layout

```
.                       ← git repo root
├── doc/                ← all planning docs (READ BEFORE CODING)
│   ├── PROJECT.md      ← charter, constraints, key decisions
│   ├── REQUIREMENTS.md ← 46 requirement IDs with traceability
│   ├── ROADMAP.md      ← 9-phase roadmap with plan details
│   ├── ARCHITECTURE.md ← layered design, data flow, interfaces
│   ├── STACK.md        ← tech stack & dependency versions
│   ├── dev-tasks/      ← per-phase task breakdowns (PHASE1~PHASE9)
│   ├── OMEN_TOOLS_USAGE.md  ← what OmenTools provides vs what we build
│   └── AEASSIST_STUDY.md    ← AEAssist architecture reference
├── HiAuRo/             ← plugin source
│   ├── ACR/            ← interfaces, helpers, slot system, target resolvers
│   ├── Command/        ← /hi command handler
│   ├── Data/           ← game data layer (battle, combat, objects, party, target)
│   ├── Execution/      ← execution axis + trigger metadata + script compiler
│   ├── Runtime/        ← runtime core, AIRunner, ACR lifecycle, spell queue
│   ├── UI/             ← Web UI (Kestrel + CEF) + ImGui overlays
│   ├── FactAxis/       ← fact axis (spell table, timeline, fact nodes)
│   ├── Decision/       ← decision engine + decision types
│   ├── Authoring/      ← authoring server
│   ├── Infrastructure/ ← logging, config, Browsingway IPC
│   ├── Recording/      ← encounter recording
│   └── Setting/        ← settings manager
├── OmenTools/          ← Dalamud service encapsulation (submodule)
└── Browsingway/        ← CEF rendering reference (submodule)
```

## Reference Code (read-only, do not modify)

| Path | What |
|------|------|
| `OmenTools/` | OmenTools source (DService, OmenService managers) |
| `Browsingway/` | CEF rendering reference (D3D11 texture sharing) |

## Phase Development Order

Development follows strict bottom-up order. Start at Phase 1, complete all tasks, then move to the next.

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5.1 → (5.2 ∥ 5.3) → Phase 5.4  ← MVP
                                                                              │
                                                                        Phase 6 → 7 → 8 → 9
```

All 9 phases are **completed** (46/46 v1 requirements).

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

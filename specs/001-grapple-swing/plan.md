# Implementation Plan: Pendulum Grapple Swing & Variable Tether Control

**Branch**: `001-grapple-swing` | **Date**: 2026-11-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-grapple-swing/spec.md`

## Summary

Refactor the existing `GrapplingHook` into a physics-correct pendulum swing system with variable tether-length control, zero-allocation anchor targeting with camera view-cone bias, anti-tunnelling collision safety, and decoupled team integration contracts (`IPlayerTelemetry`, `AnimatorParameterNames`, `SwingConfig` ScriptableObject). All swing physics run in FixedUpdate via Rigidbody forces with ContinuousDynamic collision detection, using a CharacterController disable/re-enable strategy to coexist with existing ground locomotion.

## Technical Context

**Language/Version**: C# / Unity .NET Standard 2.1 (Mono runtime)

**Primary Dependencies**: Unity 2022+ (URP), Unity Input System 1.14.2, Cinemachine, Unity Physics (built-in 3D)

**Storage**: N/A (runtime state only; ScriptableObject assets for config)

**Testing**: In-editor play-mode validation, Unity Profiler for GC allocation checks, manual acceptance scenarios per [quickstart.md](quickstart.md)

**Target Platform**: Windows PC (minimum: 8 GB RAM, Intel UHD 620)

**Project Type**: Unity 3D game (single-player, third-person traversal)

**Performance Goals**: Stable frame rate on Intel UHD 620; zero per-frame GC allocations in all gameplay-critical scripts

**Constraints**: Zero-allocation hot paths (Principle IV); all physics in FixedUpdate with ContinuousDynamic (Principle III); all tunables in ScriptableObjects (Principle II); all cross-team state via interfaces (Principle I)

**Scale/Scope**: 4 C# scripts modified/created, 1 ScriptableObject definition, 1 Input Action Asset update, 3 interface files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Strict Module Boundaries | PASS | New `IPlayerTelemetry` interface for cross-team state. `AnimatorParameterNames` static constants. No direct MonoBehaviour references across ownership boundaries. See [contracts/IPlayerTelemetry.md](contracts/IPlayerTelemetry.md). |
| II. Data-Driven Tuning | PASS | All tunable values in `SwingConfig` ScriptableObject. No hardcoded constants. See [contracts/SwingConfig.md](contracts/SwingConfig.md) and [data-model.md](data-model.md). |
| III. High-Speed Physics & Anti-Tunnelling | PASS | Swing physics in FixedUpdate via Rigidbody. ContinuousDynamic collision detection during swing. Tether obstruction detection + stuck-state recovery. See [research.md](research.md) R1, R5. |
| IV. Target Hardware Budget | PASS | `OverlapSphereNonAlloc` with pre-allocated buffer. No LINQ, closures, or per-frame allocations. Animator parameter hashes cached in Awake. See [research.md](research.md) R4. |
| V. Incremental Testable Deliverables | PASS | 12 validation scenarios with specific pass/fail criteria and numeric thresholds. See [quickstart.md](quickstart.md). |

**Post-Phase-1 Re-check**: All five principles satisfied. No violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-grapple-swing/
├── spec.md
├── plan.md                          # This file
├── research.md                      # Phase 0: technical decisions
├── data-model.md                    # Phase 1: entities and relationships
├── quickstart.md                    # Phase 1: validation guide
├── contracts/
│   ├── IPlayerTelemetry.md          # Interface contract
│   ├── AnimatorParameterNames.md    # Shared constants contract
│   └── SwingConfig.md              # ScriptableObject contract
├── checklists/
│   └── requirements.md              # Spec quality checklist
└── tasks.md                         # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
Assets/
├── PlayerControls.inputactions          # UPDATE: add Jump, Grapple, ReelIn, ReelOut actions
├── Scripts/
│   ├── Interfaces/
│   │   ├── IPlayerState.cs              # EXISTING (unchanged)
│   │   ├── IPlayerHUDData.cs            # EXISTING (unchanged)
│   │   └── IPlayerTelemetry.cs          # NEW: extends IPlayerState with grapple telemetry
│   ├── Player/
│   │   ├── PlayerController.cs          # MODIFY: implement IPlayerTelemetry, add Animator driving
│   │   ├── GrapplingHook.cs             # REWRITE: pendulum physics, tether control, targeting
│   │   └── SwingConfig.cs               # NEW: ScriptableObject data asset
│   └── AnimatorParameterNames.cs        # NEW: shared Animator constant strings
└── Data/
    └── DefaultSwingConfig.asset         # NEW: default ScriptableObject instance (created in Editor)
```

**Structure Decision**: Follows existing `Assets/Scripts/` hierarchy with `Interfaces/` and `Player/` subdirectories already established. New files added to existing directories by namespace. `AnimatorParameterNames` goes at the root `TheLastDelivery` namespace level since it's shared across all team subsystems.

## Complexity Tracking

No constitution violations to justify. All design decisions align with the five principles.

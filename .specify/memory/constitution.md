<!--
## Sync Impact Report
- Version change: 0.0.0 → 1.0.0
- Modified principles: N/A (initial ratification)
- Added sections:
  - Principle I: Strict Module Boundaries & Team Handoff Contracts
  - Principle II: Data-Driven Tuning
  - Principle III: High-Speed Physics & Anti-Tunnelling Safety
  - Principle IV: Target Hardware Budget
  - Principle V: Incremental Testable Deliverables
  - Section: Baseline & Technology Stack
  - Section: Development Workflow
  - Section: Governance
- Removed sections: N/A
- Follow-up TODOs: None
-->

# The Last Delivery Constitution

## Core Principles

### I. Strict Module Boundaries & Team Handoff Contracts

All shared runtime state MUST be exposed through decoupled C# interfaces and events. Direct
field access or tight coupling between team members' subsystems is prohibited.

- Huzaifa owns: advanced grapple physics, velocity-aware camera triggers, music-box
  integrity/damage, collision safety, and Input System mapping.
- Hamza owns: AI/Drones.
- Asfa owns: HUD/Anim/VFX.
- Anusha owns: Level/GameLoop.
- Integration contracts: `IPlayerTelemetry` (speed, position, swing state),
  `IDamageableCargo` (integrity, damage events), `ICameraFeedbackEvents` (FOV/shake
  triggers), `IPlayerState` (position, velocity, grounded, swinging),
  `IPlayerHUDData` (speed, integrity, time), and agreed `Animator` parameter strings.
- No subsystem may reference another owner's MonoBehaviour directly; communicate
  exclusively through the declared interfaces and C# events/`UnityEvent`s.

**Rationale:** Four team members working in parallel on a single Unity project. Without
strict interface boundaries, merge conflicts and runtime coupling will block integration.

### II. Data-Driven Tuning

Every tunable gameplay value MUST live in a `ScriptableObject` asset, never in hardcoded
C# constants or serialized MonoBehaviour defaults that require opening a scene.

- Physics constants (gravity multiplier, drag, mass overrides).
- Tether spring rate, retract speed, rope length limits.
- FOV curves, camera shake intensity profiles.
- Impact damage thresholds, integrity decay rates.
- Drone behavior parameters (patrol speed, detection radius, attack cooldown).

Level designers and team members MUST be able to adjust any parameter by editing a
ScriptableObject in the Inspector without modifying or recompiling C# source files.

**Rationale:** Balancing a traversal game requires rapid iteration. ScriptableObjects
provide hot-reloadable, version-controllable, scene-independent tuning that keeps code
diffs clean and design iteration fast.

### III. High-Speed Physics & Anti-Tunnelling Safety

All swing, grapple, and high-velocity traversal MUST use Rigidbody forces applied in
`FixedUpdate` with `ContinuousDynamic` collision detection mode.

- `Update` MUST NOT apply physics forces or modify Rigidbody velocity directly.
- Every physics-driven movement path MUST include boundary checks for stuck-state
  recovery (e.g., player clipped inside geometry or fell below the kill plane).
- Rope/tether constraint enforcement MUST prevent the player from tunnelling through
  thin walls or rooftop edges during high-speed swings.
- All grapple anchor searches, line-of-sight checks, and rope constraint calculations
  MUST use layer-masked raycasts to avoid false collisions with triggers or UI elements.

**Rationale:** Grappling-hook traversal generates velocities that exceed Unity's default
discrete collision tolerance. Without continuous detection and explicit boundary recovery,
players will clip through geometry and experience unfair physics glitches.

### IV. Target Hardware Budget

All `Update` and `FixedUpdate` code paths MUST be zero-allocation (no `new` heap objects,
no LINQ, no closures, no string concatenation per frame) and lightweight enough to maintain
stable frame rates on the minimum target hardware.

- Minimum target: 8 GB RAM, Intel UHD 620 integrated graphics.
- Per-frame GC allocations MUST be zero in player-critical scripts (PlayerController,
  GrapplingHook, camera controllers, damage systems).
- `Physics.OverlapSphere` and similar query APIs MUST use pre-allocated `Collider[]`
  buffers via `NonAlloc` variants.
- Object pooling MUST be used for any runtime-spawned objects (VFX, projectiles, UI
  popups) instead of `Instantiate`/`Destroy` per instance.

**Rationale:** The game targets student/low-spec hardware. A single per-frame allocation
in a hot path can cause visible GC stutter on integrated GPUs with limited thermal headroom.

### V. Incremental Testable Deliverables

Every feature specification MUST include concrete, repeatable acceptance checks that can be
verified in-editor or in a test scene without requiring the full game loop.

- Each spec MUST list at minimum: one pass condition and one fail condition.
- Physics features MUST include calibration checks with specific numeric thresholds
  (e.g., "impulse > 15 m/s MUST trigger damage event," "tether retract MUST stabilize
  rope length within 0.3 s").
- Interface contracts MUST include event-firing verification steps (e.g., "subscribe to
  `IDamageableCargo.OnDamaged`; drop music box from 5 m; verify event fires with
  damage > 0").
- Features that cannot be acceptance-tested in isolation MUST document the required
  integration dependencies and provide a mock/stub path for independent verification.

**Rationale:** With four team members integrating asynchronously, untestable features
become integration surprises. Concrete acceptance criteria allow each owner to verify
their work before handoff.

## Baseline & Technology Stack

The following are already implemented and MUST NOT be broken by new feature work:

- **Engine:** Unity (URP rendering pipeline).
- **Language:** C#, targeting .NET Standard 2.1 / Unity's Mono runtime.
- **Input:** Unity Input System package (v1.14.2). Migration from legacy `Input.GetKey`
  to the new Input System actions is planned but the existing legacy calls in
  `PlayerController` and `GrapplingHook` are the current baseline.
- **Camera:** Cinemachine (basic follow camera operational).
- **Locomotion:** Third-person run, jump, ground detection via SphereCast, air steering,
  and external velocity inheritance (`PlayerController`).
- **Grapple:** Anchor detection (tag + line-of-sight), attach/release, basic pendulum
  swing, release momentum boost (`GrapplingHook`).
- **Interfaces:** `IPlayerState` (Position, Velocity, IsGrounded, IsSwinging) and
  `IPlayerHUDData` (CurrentSpeed, IntegrityPercentage, RemainingTime) are defined in
  `Assets/Scripts/Interfaces/`.

All new specifications MUST build on top of these scripts and interfaces. Breaking changes
to `PlayerController` or `GrapplingHook` public API require explicit team agreement.

## Development Workflow

- **Branching:** Each team member works on their own feature branch. Integration happens
  via pull request into the shared development branch.
- **Scene discipline:** Avoid concurrent edits to the same Unity scene file. Use prefabs
  and ScriptableObjects to minimize scene-level merge conflicts.
- **Namespace convention:** All project code MUST live under the `TheLastDelivery`
  namespace hierarchy (e.g., `TheLastDelivery.Player`, `TheLastDelivery.Interfaces`,
  `TheLastDelivery.AI`, `TheLastDelivery.UI`, `TheLastDelivery.GameLoop`).
- **Animator parameters:** Shared Animator parameter names MUST be documented as string
  constants in a shared static class to prevent typo-driven bugs across team boundaries.
- **Specification compliance:** Every PR description MUST reference which constitution
  principles it satisfies and confirm that acceptance checks from the feature spec pass.

## Governance

This constitution supersedes conflicting patterns found elsewhere in the codebase. When a
conflict is discovered, the code MUST be updated to comply with the constitution, not the
other way around.

- **Amendments** require: (1) written proposal describing the change and rationale,
  (2) agreement from all four team members, (3) version bump to this document with an
  updated Sync Impact Report.
- **Version policy:** MAJOR for principle removals or incompatible redefinitions, MINOR
  for new principles or material expansions, PATCH for wording clarifications.
- **Compliance review:** All code reviews MUST verify adherence to the five core principles.
  Reviewers MUST flag violations using the principle number (e.g., "violates Principle IV:
  LINQ in Update loop").
- **Guidance file:** Runtime development guidance and per-feature specifications live in
  `.specify/` alongside this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-11-13 | **Last Amended**: 2026-11-13

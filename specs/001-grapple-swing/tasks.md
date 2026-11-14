# Tasks: Pendulum Grapple Swing & Variable Tether Control

**Input**: Design documents from `/specs/001-grapple-swing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Not explicitly requested in the feature specification. Test tasks are omitted.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Unity project**: `Assets/Scripts/` for C# source, `Assets/Data/` for ScriptableObject assets
- Interfaces in `Assets/Scripts/Interfaces/`
- Player scripts in `Assets/Scripts/Player/`
- Shared constants at `Assets/Scripts/` root namespace level

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization — input actions, shared constants, ScriptableObject definition, and interface contracts that all user stories depend on.

- [X] T001 [P] Update `Assets/PlayerControls.inputactions` to add four new actions in the `Player` action map: `Jump` (Button: Keyboard Space, Gamepad South), `Grapple` (Button: Mouse Left Button / Keyboard E, Gamepad Left Bumper), `ReelIn` (Button: Keyboard Left Shift, Gamepad Left Trigger), `ReelOut` (Button: Keyboard Left Ctrl, Gamepad Right Trigger). Rebind existing `Fire` action from Gamepad Right Trigger to Gamepad Right Bumper to resolve the conflict (per research.md R3).
- [X] T002 [P] Create `Assets/Scripts/AnimatorParameterNames.cs` — static class in `TheLastDelivery` namespace with `const string` fields: `Speed = "Speed"`, `VerticalVelocity = "VerticalVelocity"`, `IsGrounded = "IsGrounded"`, `IsSwinging = "IsSwinging"`, `IsLanding = "IsLanding"` (per contracts/AnimatorParameterNames.md).
- [X] T003 [P] Create `Assets/Scripts/Player/SwingConfig.cs` — ScriptableObject in `TheLastDelivery.Player` namespace with `[CreateAssetMenu(fileName = "SwingConfig", menuName = "TheLastDelivery/Swing Config")]`. Fields: `swingGravityMultiplier` (float, default 1.0f), `swingDamping` (float, default 0.995f), `swingSteerForce` (float, default 12.0f), `reelInSpeed` (float, default 8.0f), `reelOutSpeed` (float, default 6.0f), `minTetherLength` (float, default 3.0f), `maxTetherLength` (float, default 40.0f), `releaseBoostMultiplier` (float, default 1.15f), `maxAnchorRange` (float, default 35.0f), `viewConeWeight` (float, default 0.6f, Range 0-1), `distanceWeight` (float, default 0.4f, Range 0-1), `anchorLayerMask` (LayerMask, default ~0), `obstacleLayerMask` (LayerMask, default ~0), `obstructionGracePeriod` (float, default 0.3f), `stuckVelocityThreshold` (float, default 0.5f), `stuckRecoveryImpulse` (float, default 5.0f). Verbatim data constraints: "minTetherLength > 0 and < maxTetherLength", "viewConeWeight + distanceWeight should equal 1.0 (enforced via normalization at runtime, not hard error)", "All speed/force values > 0", "obstructionGracePeriod >= 0 (0 = instant release)". Pure data container with no methods (per contracts/SwingConfig.md, data-model.md).
- [X] T004 [P] Create `Assets/Scripts/Interfaces/IPlayerTelemetry.cs` — interface in `TheLastDelivery.Interfaces` namespace extending `IPlayerState`. Add properties: `float CurrentSpeed { get; }`, `Vector3? CurrentGrapplePoint { get; }`, `bool HasGrappleTarget { get; }`. Enforce verbatim constraints: read-only, no setters, and null-safe Vector3? for targeting (per contracts/IPlayerTelemetry.md).
- [X] T005 Create `Assets/Data/DefaultSwingConfig.asset` — instantiate the SwingConfig ScriptableObject via the Unity Editor Create Asset menu (`Assets > Create > TheLastDelivery > Swing Config`) with all default values.

**Checkpoint**: All shared contracts, data definitions, and input actions are in place. No runtime behavior yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure changes to PlayerController that MUST be complete before ANY user story can be implemented — Rigidbody coexistence strategy, IPlayerTelemetry implementation, and Animator parameter driving.

**⚠ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Modify `Assets/Scripts/Player/PlayerController.cs` to implement `IPlayerTelemetry` interface. Add properties: `CurrentSpeed` (returns horizontal velocity magnitude), `CurrentGrapplePoint` (backed by a nullable Vector3 field settable by GrapplingHook), `HasGrappleTarget` (returns CurrentGrapplePoint.HasValue). Ensure existing `IPlayerState` and `IPlayerHUDData` implementations remain intact (FR-012, FR-016).
- [X] T007 Modify `Assets/Scripts/Player/PlayerController.cs` to cache Animator parameter hashes in `Awake()` using `Animator.StringToHash()` with constants from `AnimatorParameterNames`. In `Update()`, drive Animator parameters: `Speed` (CurrentSpeed), `VerticalVelocity` (velocity.y), `IsGrounded` (isGrounded), `IsSwinging` (isSwinging), `IsLanding` (detect landing frame). Zero GC allocations — use cached hashes only (FR-013, Principle IV).
- [X] T008 Modify `Assets/Scripts/Player/PlayerController.cs` to add a `LaunchWithVelocity(Vector3 velocity)` public method that sets an external velocity vector to be consumed by the next Update's movement calculation, enabling seamless momentum transfer from grapple release to ground/air locomotion (FR-017).
- [X] T009 Modify `Assets/Scripts/Player/PlayerController.cs` to support CharacterController disable/enable strategy for swing mode. Add: `EnterSwingMode()` — disables CharacterController, sets Rigidbody to non-kinematic with ContinuousDynamic collision detection, transfers current position. `ExitSwingMode()` — reads Rigidbody velocity, sets Rigidbody to kinematic, re-enables CharacterController, calls `LaunchWithVelocity()` with captured velocity. Ensure Rigidbody component exists (add via RequireComponent or ensure in Awake) (per research.md R1, FR-009).

**Checkpoint**: PlayerController now supports swing mode transitions, exposes telemetry, and drives Animator parameters. GrapplingHook rewrite can begin.

---

## Phase 3: User Story 1 — Pendulum Swing from Rooftop Anchor (Priority: P1) 🎯 MVP

**Goal**: Player attaches grapple to a valid anchor and swings in a smooth, physics-correct pendulum arc. Release at any point transfers swing velocity into ground/air locomotion.

**Independent Test**: Attach grapple to a single anchor in a test scene. Player swings in a clean pendulum arc. Release at arc peak and observe momentum-preserving launch. Test passes when arc is visually smooth and release velocity matches swing direction and speed (quickstart.md V1, V2, V3).

### Implementation for User Story 1

- [X] T010 [US1] Rewrite `Assets/Scripts/Player/GrapplingHook.cs` — foundational structure. Add `[SerializeField] private SwingConfig config` field. Add `[SerializeField] private PlayerInput playerInput` field (or get via GetComponent). In `Awake()`: pre-allocate `Collider[32]` buffer for `OverlapSphereNonAlloc`, cache references to Rigidbody, CharacterController, PlayerController, main Camera transform. Subscribe to Input System actions: `Grapple` (performed/canceled callbacks). Set up internal tether state fields: `isAttached` (bool), `anchorPoint` (Vector3), `anchorTransform` (Transform), `currentLength` (float). Zero allocations in Awake (FR-015, Principle IV).
- [X] T011 [US1] Implement anchor attach logic in `Assets/Scripts/Player/GrapplingHook.cs`. On Grapple input performed: if not attached AND a valid target exists (use simple nearest-anchor fallback for US1 before smart targeting), compute initial tether length as distance to anchor, clamp to `config.minTetherLength`..`config.maxTetherLength`, store `anchorPoint` and `anchorTransform`, call `PlayerController.EnterSwingMode()`, set `isAttached = true`. Set `IPlayerTelemetry.CurrentGrapplePoint` on PlayerController.
- [X] T012 [US1] Implement pendulum physics in `Assets/Scripts/Player/GrapplingHook.cs` `FixedUpdate()`. When `isAttached`: (1) Apply gravity: `rb.AddForce(Physics.gravity * config.swingGravityMultiplier, ForceMode.Acceleration)`. (2) Enforce rigid-rod constraint: compute vector from player to anchor, if distance > currentLength, project player position back onto sphere of radius currentLength centered on anchor, cancel radial velocity component (dot product with rope direction), keep tangential component only. (3) Apply velocity damping: `rb.velocity *= config.swingDamping`. All in FixedUpdate, zero GC allocations (FR-001, FR-002, research.md R2).
- [X] T013 [US1] Implement directional swing steering in `Assets/Scripts/Player/GrapplingHook.cs` `FixedUpdate()`. Read movement input (WASD/left stick) from Input System Move action. Compute steering force perpendicular to the tether direction: cross the rope direction with the world up vector to get the lateral axis, apply `config.swingSteerForce` scaled by input magnitude. Apply via `rb.AddForce()`. Do not modify tether length (FR-003).
- [X] T014 [US1] Implement grapple release in `Assets/Scripts/Player/GrapplingHook.cs`. On Grapple input canceled (or Jump input): capture `rb.velocity`, multiply by `config.releaseBoostMultiplier`, call `PlayerController.ExitSwingMode()` which internally calls `LaunchWithVelocity()` with the boosted velocity. Set `isAttached = false`, clear `anchorPoint`, set `CurrentGrapplePoint = null` on PlayerController. Handle anchor destroyed/disabled mid-swing: check `anchorTransform == null` or `!anchorTransform.gameObject.activeInHierarchy` each FixedUpdate, trigger auto-release if true (FR-017, edge case).

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Validate with quickstart.md V1 (Pendulum Swing Arc), V2 (Release Momentum Transfer), V3 (Directional Swing Steering).

---

## Phase 4: User Story 2 — Reel In / Extend Tether Mid-Swing (Priority: P1)

**Goal**: Player controls tether length mid-swing — reeling in to gain height/speed, extending to widen the arc — with smooth clamping at configurable min/max bounds.

**Independent Test**: While swinging, hold reel-in to shorten tether (player rises toward anchor). Hold reel-out to lengthen (arc widens). Verify tether stops at min/max bounds with no jitter. Verify reel speed matches SwingConfig values (quickstart.md V4).

### Implementation for User Story 2

- [ ] T015 [US2] Implement tether reel-in/reel-out in `Assets/Scripts/Player/GrapplingHook.cs`. In `Awake()`, subscribe to Input System `ReelIn` and `ReelOut` actions (track pressed state via performed/canceled callbacks). In `FixedUpdate()`, when `isAttached`: if ReelIn held, decrease `currentLength` by `config.reelInSpeed * Time.fixedDeltaTime`; if ReelOut held, increase `currentLength` by `config.reelOutSpeed * Time.fixedDeltaTime`. Clamp `currentLength` to `config.minTetherLength`..`config.maxTetherLength` after each adjustment. No jitter or oscillation at bounds — clamping happens before the rigid-rod constraint enforcement so the constraint applies the new length cleanly (FR-004, FR-005).

**Checkpoint**: User Story 2 complete. Validate with quickstart.md V4 (Reel In / Reel Out). Verify SC-002: tether reel-in from max to min completes at consistent duration matching config speed.

---

## Phase 5: User Story 3 — Smart Anchor Targeting with HUD Feedback (Priority: P2)

**Goal**: System selects the best grapple anchor using proximity + camera view-cone bias, verifies line-of-sight, and exposes the target through IPlayerTelemetry for HUD rendering.

**Independent Test**: Place 3-4 anchors at varying distances/angles. Walk and look around. Observe correct anchor selected (closest to camera center within range and LOS). Verify `CurrentGrapplePoint` updates each frame via telemetry. Verify zero GC allocations via Profiler (quickstart.md V5).

### Implementation for User Story 3

- [ ] T016 [US3] Implement anchor targeting system in `Assets/Scripts/Player/GrapplingHook.cs` `Update()`. Each frame: (1) Call `Physics.OverlapSphereNonAlloc(playerPos, config.maxAnchorRange, _anchorBuffer, config.anchorLayerMask)`. (2) Iterate results, filter by `GrapplePoint` tag. (3) For each candidate: compute `distance` from player, compute `viewConeScore` as dot product of camera forward and direction to anchor (clamped 0-1), verify line-of-sight via `Physics.Raycast` with `config.obstacleLayerMask`, skip if occluded. (4) Compute `combinedScore = config.viewConeWeight * viewConeScore + config.distanceWeight * (1 - distance/config.maxAnchorRange)`, normalize weights at runtime. (5) Select highest-scoring candidate. Deterministic tiebreaker: lower `collider.GetInstanceID()` wins. (6) Update `PlayerController.CurrentGrapplePoint` with selected anchor position (or null if none). Zero allocations — no LINQ, no temporary lists, scoring in-place over the pre-allocated buffer (FR-006, FR-007, FR-008, FR-015, research.md R4).
- [ ] T017 [US3] Replace the simple nearest-anchor fallback in T011's attach logic with the smart targeting result in `Assets/Scripts/Player/GrapplingHook.cs`. On Grapple input, attach to `CurrentGrapplePoint` if `HasGrappleTarget` is true.

**Checkpoint**: User Story 3 complete. Validate with quickstart.md V5 (Anchor Targeting & View-Cone Bias). Verify SC-003: correct anchor selected within 1 frame, no flickering.

---

## Phase 6: User Story 4 — Collision Safety During High-Speed Swings (Priority: P2)

**Goal**: Player never clips through geometry during swings. Tether obstruction triggers auto-release after a configurable grace period. Stuck states are detected and recovered.

**Independent Test**: Swing at max speed toward thin geometry — verify zero penetration. Swing around a corner that occludes the anchor — verify auto-release triggers within grace period and momentum is preserved (quickstart.md V6, V7, V8).

### Implementation for User Story 4

- [ ] T018 [US4] Implement tether obstruction detection in `Assets/Scripts/Player/GrapplingHook.cs` `FixedUpdate()`. When `isAttached`: raycast from player position to `anchorPoint` using `config.obstacleLayerMask`. If hit (LOS blocked), increment `obstructionTimer` by `Time.fixedDeltaTime`. If `obstructionTimer >= config.obstructionGracePeriod`, trigger auto-release (same as manual release — capture velocity, ExitSwingMode, clear state). If LOS clear, reset `obstructionTimer` to 0. Grace period prevents jittery releases when swinging past thin poles (FR-010, research.md R5).
- [ ] T019 [US4] Implement stuck-state detection and recovery in `Assets/Scripts/Player/GrapplingHook.cs` `FixedUpdate()`. When `isAttached`: record `lastValidPosition` each FixedUpdate when `!Physics.CheckSphere(playerPos, 0.3f, config.obstacleLayerMask)` (player not overlapping geometry). If `rb.velocity.magnitude < config.stuckVelocityThreshold` AND `Physics.CheckSphere(playerPos, 0.3f, config.obstacleLayerMask)` returns true (player overlapping geometry), trigger stuck recovery: set `rb.position = lastValidPosition`, apply upward impulse of `config.stuckRecoveryImpulse`, then auto-release grapple (FR-011).
- [ ] T020 [US4] Verify ContinuousDynamic collision detection is set on the Rigidbody during swing mode in `Assets/Scripts/Player/GrapplingHook.cs`. Add an explicit safety check in `GrapplingHook.cs` to ensure collisionDetectionMode is `CollisionDetectionMode.ContinuousDynamic` when `isAttached` — if not, set it (FR-009, SC-004).

**Checkpoint**: User Story 4 complete. Validate with quickstart.md V6 (Wall Penetration), V7 (Tether Obstruction Auto-Release), V8 (Stuck-State Recovery). Verify SC-004: zero penetration across 100 consecutive swings. Verify SC-005: auto-release within grace period.

---

## Phase 7: User Story 5 — Team Integration Contracts (Priority: P2)

**Goal**: External team members can read live grapple state via `IPlayerTelemetry` and `AnimatorParameterNames` without referencing concrete grapple/player classes.

**Independent Test**: Create a debug MonoBehaviour referencing only `IPlayerTelemetry`. Verify all properties update correctly during idle, running, swinging, and mid-air states. Verify Animator parameters are set each frame. Verify no compilation errors when referencing only the interface (quickstart.md V9, V10).

### Implementation for User Story 5

- [ ] T021 [US5] Verify and finalize `IPlayerTelemetry` integration in `Assets/Scripts/Player/PlayerController.cs`. Ensure `CurrentSpeed` returns accurate horizontal speed (`new Vector3(velocity.x, 0, velocity.z).magnitude`). Ensure `CurrentGrapplePoint` is updated by GrapplingHook each frame (set during targeting in Update, cleared on release). Ensure `HasGrappleTarget` returns correct state. Verify a test script using only `TheLastDelivery.Interfaces` namespace compiles (FR-012, SC-007).
- [ ] T022 [US5] Verify Animator parameter driving in `Assets/Scripts/Player/PlayerController.cs`. Ensure all five parameters are driven every Update frame: `Speed` (horizontal speed), `VerticalVelocity` (velocity.y), `IsGrounded` (CharacterController or ground-check state), `IsSwinging` (isSwinging flag), `IsLanding` (transition from !isGrounded to isGrounded). All calls use cached hashes from T007. Verify no inline string literals for Animator parameters anywhere in the codebase (FR-013).
- [ ] T023 [US5] Verify that `PlayerController` exposes a way for `GrapplingHook` to set `CurrentGrapplePoint` without exposing a public setter on the `IPlayerTelemetry` interface in `Assets/Scripts/Player/PlayerController.cs`. Use an internal method or explicit implementation pattern to ensure the interface remains read-only for external consumers (contracts/IPlayerTelemetry.md rule 1).

**Checkpoint**: User Story 5 complete. Validate with quickstart.md V9 (IPlayerTelemetry Contract), V10 (Animator Parameters). Verify SC-007: external scripts compile with interface-only reference.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Zero-allocation verification, regression testing, edge case handling, and final validation across all stories.

- [ ] T024 [P] Verify zero GC allocations in `Assets/Scripts/Player/GrapplingHook.cs` and `Assets/Scripts/Player/PlayerController.cs`. Review all hot paths (Update, FixedUpdate) for: `new` heap objects, LINQ queries, closures/lambdas, string concatenation, `Physics.OverlapSphere` (must be NonAlloc variant), boxing of value types. Fix any violations found (FR-015, SC-006, Principle IV).
- [ ] T025 [P] Handle edge case in `Assets/Scripts/Player/GrapplingHook.cs`: player attaches to anchor directly above (zero-length pendulum arm). Ensure tether clamps to `config.minTetherLength` and player hangs vertically; reel-out begins the swing (spec.md edge case).
- [ ] T026 [P] Handle edge case in `Assets/Scripts/Player/GrapplingHook.cs`: player is grounded and attaches to anchor at same height. Tether engages but player must jump or provide input to initiate swing motion; gravity alone won't start a pendulum at equilibrium (spec.md edge case).
- [ ] T027 [P] Handle edge case in `Assets/Scripts/Player/GrapplingHook.cs`: reel-in would pull player into anchor collider. Tether clamps at `config.minTetherLength` before contact; player does not collide with anchor point geometry (spec.md edge case).
- [ ] T028 [P] Handle edge case in `Assets/Scripts/Player/GrapplingHook.cs`: player swings into kill plane / falls below level. Ensure grapple auto-releases before respawn triggers so existing boundary/kill-plane system handles respawn cleanly (spec.md edge case).
- [ ] T029 Verify ground locomotion regression in `Assets/Scripts/Player/PlayerController.cs`. Run, jump, air-steer must function identically to pre-feature baseline. Ensure CharacterController re-enable after swing mode does not alter movement speed, jump height, or air-steer responsiveness (FR-016, SC-008, quickstart.md V12).
- [ ] T030 Run full validation pass per `specs/001-grapple-swing/quickstart.md` (V1-V12) and confirm all scenarios pass.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational phase completion
- **User Story 2 (Phase 4)**: Depends on User Story 1 (requires swing system to exist for reel to modify)
- **User Story 3 (Phase 5)**: Depends on Foundational phase; can proceed in parallel with US1/US2 but T017 integrates with US1's attach logic
- **User Story 4 (Phase 6)**: Depends on User Story 1 (requires swing physics to be in place for collision safety)
- **User Story 5 (Phase 7)**: Depends on Foundational phase; can proceed in parallel with other stories but validates final integration
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **User Story 2 (P1)**: Depends on US1's tether state and FixedUpdate loop existing
- **User Story 3 (P2)**: Can start after Foundational; T017 integrates into US1's attach path
- **User Story 4 (P2)**: Depends on US1's swing physics in FixedUpdate
- **User Story 5 (P2)**: Can start after Foundational; validates integration of all prior work

### Within Each User Story

- Models/data before services
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks (T001-T004) marked [P] can run in parallel
- Within Foundational, T006/T007/T008 modify the same file (`Assets/Scripts/Player/PlayerController.cs`) — execute sequentially; T009 also modifies PlayerController.cs — execute after T008
- User Story 3 targeting (T016) can be developed in parallel with US1 if the attach path uses a temporary fallback
- All Polish tasks marked [P] can run in parallel
- Edge case tasks (T025-T028) are independent and can run in parallel

---

## Parallel Example: User Story 1

```text
# Setup phase — launch all in parallel:
Task T001: "Update Assets/PlayerControls.inputactions with Jump, Grapple, ReelIn, ReelOut actions"
Task T002: "Create Assets/Scripts/AnimatorParameterNames.cs"
Task T003: "Create Assets/Scripts/Player/SwingConfig.cs ScriptableObject"
Task T004: "Create Assets/Scripts/Interfaces/IPlayerTelemetry.cs interface"

# After Setup — Foundational (sequential, same file):
Task T006: "Implement IPlayerTelemetry on Assets/Scripts/Player/PlayerController.cs"
Task T007: "Add Animator parameter driving to Assets/Scripts/Player/PlayerController.cs"
Task T008: "Add LaunchWithVelocity method to Assets/Scripts/Player/PlayerController.cs"
Task T009: "Add EnterSwingMode/ExitSwingMode to Assets/Scripts/Player/PlayerController.cs"

# After Foundational — US1 implementation:
Task T010: "GrapplingHook rewrite — foundational structure in Assets/Scripts/Player/GrapplingHook.cs"
Task T011: "Anchor attach logic in Assets/Scripts/Player/GrapplingHook.cs"
Task T012: "Pendulum physics in FixedUpdate in Assets/Scripts/Player/GrapplingHook.cs"
Task T013: "Directional swing steering in Assets/Scripts/Player/GrapplingHook.cs"
Task T014: "Grapple release with momentum transfer in Assets/Scripts/Player/GrapplingHook.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T005)
2. Complete Phase 2: Foundational (T006-T009)
3. Complete Phase 3: User Story 1 (T010-T014)
4. **STOP and VALIDATE**: Run `specs/001-grapple-swing/quickstart.md` V1, V2, V3
5. Deploy/demo if ready — player can swing and release with momentum

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Validate V1-V3 → MVP! (basic pendulum swing)
3. Add User Story 2 → Validate V4 → Tether control added
4. Add User Story 3 → Validate V5 → Smart targeting added
5. Add User Story 4 → Validate V6-V8 → Collision safety added
6. Add User Story 5 → Validate V9-V10 → Team integration verified
7. Polish → Validate V11-V12 → Feature complete

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Huzaifa: User Stories 1 & 2 (core swing physics, tightly coupled)
   - Another developer: User Story 3 (targeting — independent logic, integrates at T017)
3. User Stories 4 & 5 after US1 is stable

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All physics code in FixedUpdate, all targeting in Update (per Principle III)
- All tunable values read from SwingConfig ScriptableObject (per Principle II)
- All Animator parameter names via AnimatorParameterNames constants (per Development Workflow)
- Zero per-frame GC allocations in all hot paths (per Principle IV)

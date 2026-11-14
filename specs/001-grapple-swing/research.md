# Research: Pendulum Grapple Swing & Variable Tether Control

**Date**: 2026-11-13
**Feature**: `001-grapple-swing`

## R1: CharacterController / Rigidbody Coexistence During Swing

**Decision**: Disable CharacterController during swing, enable a Rigidbody for physics-driven pendulum movement, then re-enable CharacterController on release.

**Rationale**: Unity's CharacterController and Rigidbody both control the Transform directly and conflict when both are active. CharacterController ignores physics forces — it only responds to `Move()` calls. The constitution (Principle III) mandates Rigidbody forces in FixedUpdate for high-speed swing physics. The cleanest separation:

1. On grapple attach: disable `CharacterController` component, enable `Rigidbody` (set to non-kinematic), set collision detection to `ContinuousDynamic`.
2. During swing: all movement via `Rigidbody.AddForce()` / `Rigidbody.velocity` manipulation in `FixedUpdate`.
3. On grapple release: read `Rigidbody.velocity`, set Rigidbody back to kinematic, re-enable `CharacterController`, call `PlayerController.LaunchWithVelocity()` with the captured velocity.

This avoids the need for two separate GameObjects or a custom physics solver. The Rigidbody can be present at all times (kinematic when grounded), and the CharacterController enabled/disabled as needed.

**Alternatives considered**:
- **Keep CharacterController, simulate pendulum manually**: Would require reimplementing collision response, defeating the purpose of using Unity's physics engine. Rejected because it violates Principle III's mandate for Rigidbody forces.
- **Two GameObjects (physics proxy)**: A child Rigidbody that drives the parent transform during swing. Overly complex, introduces parent-child synchronization issues, and creates two sources of truth for position.
- **Replace CharacterController entirely with Rigidbody**: Would break existing ground locomotion (run, jump, ground detection) which is built entirely on `CharacterController.Move()` and `controller.isGrounded`. Too risky and violates the "build on top without breaking" requirement.

## R2: Pendulum Constraint Model — Rigid Rod vs. Spring

**Decision**: Use a rigid-rod constraint (project out radial velocity component) rather than a spring model.

**Rationale**: A spring model (current codebase approach — `pullSpeed * excess`) creates elastic, bouncy behavior that feels like a bungee cord rather than a rigid rope. A rigid-rod pendulum:

1. Each FixedUpdate, compute the vector from player to anchor and the current distance.
2. If distance exceeds `ropeLength`, project the player back onto the sphere of radius `ropeLength` centered on the anchor.
3. Cancel the radial component of velocity (dot product with rope direction), keeping only the tangential component.
4. Apply gravity normally — the tangential component of gravity creates the pendulum's restoring force.

This produces the clean, physically correct pendulum arc the spec requires (FR-001, FR-002). The current spring-pull approach in `GrapplingHook.ApplySwingPhysics()` will be replaced entirely.

**Alternatives considered**:
- **Spring-damper (current approach)**: `swingVelocity += ropeDir * (excess * pullSpeed * dt)`. This creates oscillation at the rope boundary and doesn't produce clean arcs. Rejected.
- **Configurable Joint**: Unity's `ConfigurableJoint` between player Rigidbody and a kinematic anchor Rigidbody. Would handle the constraint automatically but provides less control over the exact rope feel, and adds complexity with joint configuration. Rejected for lack of fine-grained control.

## R3: Input System Migration Strategy

**Decision**: Add new actions (Jump, Grapple, ReelIn, ReelOut) to the existing `PlayerControls.inputactions` asset in the `Player` action map. Migrate GrapplingHook to use PlayerInput component. PlayerController legacy input (`Input.GetAxisRaw`, `Input.GetKeyDown`) remains unchanged for now — migration is a separate concern.

**Rationale**: The Input Action Asset already exists with Move, Look, and Fire actions. The spec requires Input System bindings for reel-in/out (FR-004). Adding actions to the existing asset is non-destructive. The GrapplingHook rewrite is a natural point to adopt Input System for grapple controls. Migrating PlayerController's legacy input is out of scope per the "build on top without breaking" requirement.

**New actions to add**:
- `Jump` (Button): Keyboard Space, Gamepad South button
- `Grapple` (Button): Mouse Left Button / Keyboard E, Gamepad Left Bumper
- `ReelIn` (Button): Keyboard Left Shift, Gamepad Left Trigger
- `ReelOut` (Button): Keyboard Left Ctrl, Gamepad Right Trigger

**Note**: The existing `Fire` action on Gamepad Right Trigger conflicts with `ReelOut`. Resolution: rebind `Fire` to Gamepad Right Bumper (if Fire is used) or remove it if Grapple replaces it. Since the existing code uses `Input.GetMouseButtonDown(0)` directly (not the Input System Fire action), this conflict has no runtime impact — the existing Fire action is unused by code.

## R4: Zero-Allocation Anchor Targeting

**Decision**: Use `Physics.OverlapSphereNonAlloc` with a pre-allocated `Collider[32]` buffer. Score candidates in a single pass without LINQ or temporary collections.

**Rationale**: Constitution Principle IV mandates zero per-frame GC allocations. The current implementation uses `Physics.OverlapSphere` which allocates a new `Collider[]` every call. The NonAlloc variant writes into a reusable buffer.

**Implementation pattern**:
```
// Pre-allocated in Awake
private readonly Collider[] _anchorBuffer = new Collider[32];

// In Update (targeting runs every frame for HUD feedback)
int count = Physics.OverlapSphereNonAlloc(position, maxRange, _anchorBuffer, anchorLayerMask);
// Score loop: iterate _anchorBuffer[0..count-1], no allocations
```

Buffer size 32 is sufficient — a typical rooftop level will have 5-15 grapple anchors within range. If more than 32 are within range, the furthest ones are naturally excluded (Physics returns nearest first in NonAlloc).

## R5: Tether Obstruction & Stuck-State Detection

**Decision**: Raycast from player to anchor each FixedUpdate. Track consecutive obstructed frames against a grace-period timer. Detect stuck state via velocity magnitude threshold while overlapping geometry (Physics.CheckSphere at player position).

**Rationale**:
- **Obstruction**: A single-frame raycast miss could be a false positive (e.g., player swings past a thin pole). The grace period (configurable, default 0.3s) prevents jittery auto-releases. After the timer expires, auto-release with current velocity.
- **Stuck state**: If `velocity.magnitude < threshold` (configurable, default 0.5 m/s) AND `Physics.CheckSphere` at player position detects overlap with level geometry, the player is stuck. Recovery: reposition to `lastValidPosition` (recorded each FixedUpdate when not overlapping), release grapple, apply a small upward impulse.

**Alternatives considered**:
- **SphereCast along tether**: More accurate but significantly more expensive. A simple raycast plus the grace period is sufficient for gameplay purposes.
- **OnCollisionStay detection**: Requires collision events, which CharacterController doesn't generate. Since we're using Rigidbody during swing, collision events are available, but the overlap check is simpler and more reliable than tracking collision enter/exit state.

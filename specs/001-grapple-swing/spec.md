# Feature Specification: Pendulum Grapple Swing & Variable Tether Control

**Feature**: `001-grapple-swing`

**Created**: 2026-11-13

**Status**: Draft

**Input**: User description: "Pendulum-Style Grapple Swinging & Variable Tether-Length Control — extend the existing grappling system with physics-correct pendulum arcs, reel-in/out tether control, smart anchor targeting, high-speed collision safety, and team integration contracts."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pendulum Swing from Rooftop Anchor (Priority: P1)

As a player traversing rooftops, I attach my grapple to a valid anchor point and swing in a smooth pendulum arc, using my momentum to cross gaps and reach distant platforms. The swing feels physically grounded — gravity pulls me through the arc's low point where I accelerate, and I decelerate as I climb on the opposite side.

**Why this priority**: The pendulum swing is the core traversal mechanic. Without responsive, physics-correct arcs, no other grapple feature delivers value.

**Independent Test**: Attach grapple to a single anchor in a test scene with flat ground. Player swings in a clean pendulum arc. Release at arc peak and observe momentum-preserving launch. Test passes when arc is visually smooth and release velocity matches swing direction and speed.

**Acceptance Scenarios**:

1. **Given** the player is airborne or on a rooftop edge near a tagged grapple anchor, **When** the player activates the grapple input, **Then** the tether attaches to the nearest valid anchor and the player begins swinging in a pendulum arc governed by gravity and radial constraint.
2. **Given** the player is mid-swing on a pendulum arc, **When** the player releases the grapple, **Then** the player launches with velocity matching their current swing direction and speed (with configurable boost), and ground locomotion resumes seamlessly.
3. **Given** the player is swinging, **When** the player provides directional input (WASD/left stick), **Then** a lateral swing-steering force is applied perpendicular to the rope without violating the radial tether constraint.

---

### User Story 2 - Reel In / Extend Tether Mid-Swing (Priority: P1)

As a player mid-swing, I shorten or lengthen my grapple tether to control my arc radius — reeling in to gain height and speed, extending to widen my arc and reach further horizontally. The tether length changes smoothly and respects configurable min/max bounds.

**Why this priority**: Variable tether length transforms a passive swing into an active, skill-expressive mechanic. It is tightly coupled to P1 swing physics and equally essential for satisfying traversal.

**Independent Test**: While swinging in the test scene, hold reel-in input and observe tether shortening (player rises toward anchor). Hold reel-out input and observe tether lengthening (arc widens). Verify tether stops at min/max bounds. Verify reel speed matches ScriptableObject value.

**Acceptance Scenarios**:

1. **Given** the player is attached and swinging, **When** the player holds the reel-in input, **Then** the tether length decreases at the configured reel-in speed until it reaches the minimum tether length.
2. **Given** the player is attached and swinging, **When** the player holds the reel-out input, **Then** the tether length increases at the configured reel-out speed until it reaches the maximum tether length.
3. **Given** the tether is at its minimum or maximum length, **When** the player continues to hold the reel input in that direction, **Then** the tether length remains clamped and no jitter or oscillation occurs.

---

### User Story 3 - Smart Anchor Targeting with HUD Feedback (Priority: P2)

As a player looking across a rooftop landscape, I see which grapple anchor is currently targeted before I fire. The system selects the best anchor based on proximity, line-of-sight, and how close it is to where I'm looking. The targeted anchor's position is exposed so the HUD team can render a crosshair or highlight on it.

**Why this priority**: Anchor targeting directly affects player agency — firing blind leads to frustration. However, the basic nearest-anchor system already works, making this an improvement rather than a prerequisite.

**Independent Test**: Place 3-4 anchors at varying distances and angles in a test scene. Walk and look around. Observe that the anchor closest to camera center (within max range and line-of-sight) is selected. Subscribe to the telemetry interface and verify `CurrentGrapplePoint` updates each frame. Verify no GC allocations via Profiler.

**Acceptance Scenarios**:

1. **Given** multiple anchors exist within max range, **When** the player looks toward one anchor more directly than others, **Then** the system selects the anchor with the best combined score of proximity and camera-alignment (view-cone bias).
2. **Given** the best-scored anchor is behind a wall, **When** the player aims toward it, **Then** the system skips it and selects the next-best anchor that has clear line-of-sight.
3. **Given** no anchors are within range or line-of-sight, **When** the player looks around, **Then** the system reports no valid target (null/none), and the HUD contract receives this state.

---

### User Story 4 - Collision Safety During High-Speed Swings (Priority: P2)

As a player swinging at high speed near rooftop edges and walls, I never clip through geometry or get permanently stuck inside a wall. If my tether path is obstructed (e.g., I swing behind a building that blocks the rope), the system auto-releases me safely rather than rubber-banding me through geometry.

**Why this priority**: Without collision safety, the pendulum swing becomes unfair and frustrating at the speeds the mechanic generates. However, it depends on P1 swing physics being in place first.

**Independent Test**: In a test scene with narrow gaps and thin walls, swing at maximum speed toward geometry. Verify the player never penetrates walls. Swing around a corner that occludes the anchor. Verify auto-release triggers and the player launches with preserved momentum instead of snapping through the obstacle.

**Acceptance Scenarios**:

1. **Given** the player is swinging at high velocity toward a wall, **When** the player reaches the wall, **Then** continuous collision detection prevents penetration and the player slides or bounces along the surface.
2. **Given** the player is attached and the tether line-of-sight to the anchor becomes fully obstructed, **When** the obstruction persists for longer than a configurable grace period, **Then** the system auto-releases the grapple and launches the player with their current velocity.
3. **Given** the player clips inside geometry due to an edge case, **When** the stuck-state detector triggers, **Then** the player is repositioned to the last known valid position and the grapple is released.

---

### User Story 5 - Team Integration Contracts (Priority: P2)

As Asfa (HUD/VFX), Hamza (AI), or Anusha (Level/GameLoop), I can read the player's live grapple state through a stable interface without referencing the grapple MonoBehaviour directly. I receive real-time telemetry (position, velocity, speed, swing state, current anchor) and can drive my Animator parameters and HUD elements from these contracts.

**Why this priority**: Integration contracts enable parallel work across the team. Without them, other members are blocked from building HUD, animation, and game-loop features that depend on grapple state.

**Independent Test**: In a test scene, attach a debug MonoBehaviour that subscribes to `IPlayerTelemetry`. Verify all properties update correctly during idle, running, swinging, and mid-air states. Verify Animator parameters are set each frame. Verify no compilation errors when referencing only the interface, not the concrete class.

**Acceptance Scenarios**:

1. **Given** the player is swinging, **When** an external system queries `IPlayerTelemetry`, **Then** it receives accurate, up-to-date values for Position, Velocity, CurrentSpeed, IsGrounded, IsSwinging, and CurrentGrapplePoint.
2. **Given** the player transitions between ground, air, and swing states, **When** the Animator parameters are read, **Then** Speed, VerticalVelocity, IsGrounded, IsSwinging, and IsLanding reflect the current state within the same frame.
3. **Given** a new team member script references only `IPlayerTelemetry`, **When** compiled, **Then** it compiles without any dependency on PlayerController or GrapplingHook concrete types.

---

### Edge Cases

- What happens when the player attaches to an anchor directly above them (zero-length pendulum arm)? The tether clamps to minimum length and the player hangs vertically; reel-out begins the swing.
- What happens when the anchor object is destroyed or deactivated mid-swing? The system auto-releases the grapple with current velocity preserved, identical to manual release.
- What happens when the player is grounded and attaches to an anchor at the same height? The tether engages and the player must jump or provide input to initiate swing motion; gravity alone won't start a pendulum at equilibrium.
- What happens when two anchors have identical scores (same distance and view-cone alignment)? The system deterministically selects the anchor with the lower collider instance ID to avoid flickering.
- What happens when the player swings into a kill plane / falls below the level? The existing boundary/kill-plane system handles respawn; the grapple auto-releases before respawn triggers.
- What happens when reel-in would pull the player into the anchor collider? The tether clamps at minimum length before contact; the player does not collide with the anchor point geometry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST constrain the player's radial distance from the grapple anchor using a rigid-length pendulum model, enforced in FixedUpdate via Rigidbody forces — not by teleporting or directly setting position.
- **FR-002**: System MUST apply gravity to the swinging player so that the pendulum arc accelerates through the nadir and decelerates on the upswing, preserving energy within configurable damping tolerances.
- **FR-003**: System MUST allow directional air-steering input during swings, applying forces perpendicular to the tether direction so the player can influence swing trajectory without violating the radial constraint.
- **FR-004**: System MUST allow the player to reel in (shorten) and reel out (lengthen) the tether at configurable speeds using Unity Input System bindings that support both keyboard and gamepad.
- **FR-005**: System MUST clamp tether length to configurable minimum and maximum bounds, preventing jitter or oscillation at the limits.
- **FR-006**: System MUST select the best grapple anchor using `Physics.OverlapSphereNonAlloc` with a pre-allocated collider buffer, scoring candidates by a weighted combination of distance and camera view-cone alignment.
- **FR-007**: System MUST verify line-of-sight to candidate anchors using layer-masked raycasts, rejecting any anchor occluded by level geometry.
- **FR-008**: System MUST expose the currently targeted anchor (or null if none) through the telemetry interface so HUD systems can render a targeting indicator without referencing grapple internals.
- **FR-009**: System MUST set the player's Rigidbody collision detection mode to ContinuousDynamic when swinging and revert it when grounded.
- **FR-010**: System MUST detect tether obstruction (line-of-sight from player to anchor blocked by geometry) and auto-release the grapple after a configurable grace period, launching the player with their current velocity.
- **FR-011**: System MUST detect stuck states (player velocity near zero while inside collision geometry) and recover by repositioning to last known valid position and releasing the grapple.
- **FR-012**: System MUST expose live player state through the `IPlayerTelemetry` interface: Position, Velocity, CurrentSpeed, IsGrounded, IsSwinging, CurrentGrapplePoint.
- **FR-013**: System MUST update agreed Animator parameters (Speed, VerticalVelocity, IsGrounded, IsSwinging, IsLanding) every frame to reflect current player state.
- **FR-014**: System MUST store all tunable values (swing gravity multiplier, swing damping, reel-in speed, reel-out speed, min tether length, max tether length, view-cone weight, distance weight, obstruction grace period, stuck-state velocity threshold) in a ScriptableObject asset.
- **FR-015**: System MUST produce zero per-frame GC allocations in all swing, tether, targeting, and collision-safety code paths.
- **FR-016**: System MUST preserve the existing ground locomotion (run, jump, air-steer) behavior — the grapple system activates only when attached and deactivates cleanly on release, returning full control to PlayerController.
- **FR-017**: On grapple release, system MUST transfer swing velocity (with configurable boost multiplier) into PlayerController's external velocity so momentum carries into ground/air locomotion seamlessly.

### Key Entities

- **Grapple Anchor**: A tagged collider in the scene (`GrapplePoint` tag) that serves as a valid attach target. Key attributes: world position (from collider bounds center), line-of-sight availability, targeting score.
- **Tether State**: Runtime state of the active grapple connection. Key attributes: current length, min/max bounds, attached/detached status, anchor reference, obstruction timer.
- **Swing Config (ScriptableObject)**: Data asset containing all tunable swing parameters. No behavior — pure data consumed by the grapple system at runtime.
- **Player Telemetry**: Read-only interface exposing live player state to other team subsystems. No setters — consumers observe but never mutate player state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Player completes a pendulum swing arc (attach, swing through nadir, release at peak) in under 3 seconds with visually smooth motion and no frame-rate hitches on target hardware (Intel UHD 620).
- **SC-002**: Tether reel-in from maximum to minimum length completes in a predictable, consistent duration matching the ScriptableObject-configured speed, with no visual jitter at the bounds.
- **SC-003**: Anchor targeting selects the correct anchor (closest to camera center with line-of-sight) within 1 frame of the player looking toward it, with selection stable (no flickering) when multiple anchors are similarly positioned.
- **SC-004**: Player swinging at maximum achievable speed toward thin geometry (1-unit-thick walls) never penetrates or clips through the surface across 100 consecutive test swings.
- **SC-005**: Auto-release triggers within the configured grace period (default: 0.3 seconds) when tether line-of-sight is obstructed, and the player launches cleanly without rubber-banding.
- **SC-006**: All grapple-related code paths produce zero GC allocations per frame as verified by Unity Profiler's GC Alloc column during a 60-second swing session.
- **SC-007**: External test scripts referencing only `IPlayerTelemetry` compile and receive accurate state updates without importing any concrete player or grapple class.
- **SC-008**: Existing ground locomotion (run, jump, air-steer) functions identically before and after the grapple feature integration — no regressions in movement speed, jump height, or air-steer responsiveness.

## Assumptions

- The existing `GrapplePoint` tag convention is retained for anchor identification; no new tagging system is introduced.
- The player GameObject will gain a Rigidbody component (set to kinematic when grounded) for swing physics, coexisting with the existing CharacterController — or the CharacterController will be disabled during swing and re-enabled on release. The specific co-existence strategy is an implementation detail.
- Unity Input System action bindings for reel-in and reel-out will be added to the project's Input Action Asset. Default keyboard bindings: Shift (reel-in), Ctrl (reel-out). Default gamepad bindings: Left Trigger (reel-in), Right Trigger (reel-out). Exact bindings are configurable.
- The `IPlayerTelemetry` interface is a new interface that extends or coexists with the existing `IPlayerState` — it does not replace `IPlayerState` or `IPlayerHUDData`.
- Animator parameter names (Speed, VerticalVelocity, IsGrounded, IsSwinging, IsLanding) are agreed team constants; the Animator Controller asset itself is owned by Asfa (HUD/Anim) and is not modified by this feature.
- The view-cone bias for anchor targeting uses the main camera's forward vector; no separate aiming reticle or cursor system is required for this feature.
- Kill plane / out-of-bounds recovery is handled by an existing or separate level system; this feature only ensures the grapple auto-releases before that system activates.

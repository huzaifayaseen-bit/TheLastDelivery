# Quickstart Validation Guide: Pendulum Grapple Swing & Variable Tether Control

**Date**: 2026-11-13
**Feature**: `001-grapple-swing`

## Prerequisites

- Unity Editor open with TheLastDelivery project
- A test scene containing:
  - Player prefab with PlayerController and GrapplingHook components
  - A Cinemachine follow camera targeting the player
  - 4-5 GameObjects tagged `GrapplePoint` placed at varying heights and distances (10-30m from player start)
  - A flat ground plane
  - At least one thin wall (1-unit thick) near a grapple anchor
  - At least one obstacle that can occlude a tether line (building between player swing path and anchor)
- A `SwingConfig` ScriptableObject asset created and assigned to GrapplingHook
- PlayerControls.inputactions updated with Jump, Grapple, ReelIn, ReelOut actions (see [contracts/SwingConfig.md](contracts/SwingConfig.md) for defaults)

## Validation Scenarios

### V1: Pendulum Swing Arc (FR-001, FR-002, SC-001)

1. Run the test scene
2. Navigate player near a grapple anchor (within 35m default range)
3. Press Grapple input (Left Mouse / E / Gamepad LB)
4. **Expected**: Player attaches to anchor, begins swinging in a smooth arc
5. Observe the swing: player accelerates downward through the low point, decelerates on the upswing
6. **Pass**: Arc is visually smooth, no teleporting, no jitter, completes full swing in < 3 seconds
7. **Fail**: Player snaps to position, rubber-bands, or arc is jerky/unnatural

### V2: Release Momentum Transfer (FR-017, SC-008)

1. While swinging (V1 still active), wait for near-peak of upswing
2. Release grapple (release Mouse / release E / Gamepad LB release / Space)
3. **Expected**: Player launches in the direction of swing velocity with boost multiplier
4. Player transitions smoothly to air locomotion, then lands normally
5. **Pass**: Momentum carries forward, ground movement resumes without stutter
6. **Fail**: Player drops straight down, velocity is lost, or ground locomotion is broken

### V3: Directional Swing Steering (FR-003)

1. Attach to an anchor and begin swinging
2. While swinging, hold left/right movement input (A/D or left stick)
3. **Expected**: Swing arc curves laterally in the input direction
4. **Pass**: Player can steer the swing without the rope length changing
5. **Fail**: Steering has no effect, or steering breaks the radial constraint

### V4: Reel In / Reel Out (FR-004, FR-005, SC-002)

1. Attach to an anchor and begin swinging
2. Hold ReelIn input (Left Shift / Gamepad LT)
3. **Expected**: Tether shortens, player rises toward anchor, arc radius decreases
4. Continue holding until minimum length reached
5. **Expected**: Tether stops shortening, no jitter
6. Release ReelIn, hold ReelOut input (Left Ctrl / Gamepad RT)
7. **Expected**: Tether lengthens, arc radius increases
8. Continue holding until maximum length reached
9. **Expected**: Tether stops lengthening, no jitter
10. **Pass**: Reel speed matches SwingConfig values, clamping is smooth
11. **Fail**: Jitter at bounds, reel speed doesn't match config, or tether length doesn't change

### V5: Anchor Targeting & View-Cone Bias (FR-006, FR-007, FR-008, SC-003)

1. Stand in view of 3+ grapple anchors at different angles
2. Look directly at the closest anchor
3. **Expected**: That anchor is selected (verify via IPlayerTelemetry.CurrentGrapplePoint or debug gizmo)
4. Slowly rotate camera toward a farther anchor that is more centered in view
5. **Expected**: Selection transitions to the view-centered anchor based on weight config
6. Look at an anchor behind a wall
7. **Expected**: That anchor is NOT selected; next-best visible anchor is chosen
8. Look away from all anchors
9. **Expected**: No anchor selected (CurrentGrapplePoint is null, HasGrappleTarget is false)
10. **Pass**: Selection is stable (no flickering), follows view-cone bias, respects LOS
11. **Fail**: Selection flickers, ignores view direction, or selects occluded anchors

### V6: Collision Safety — Wall Penetration (FR-009, SC-004)

1. Attach to an anchor near a thin wall (1-unit thick)
2. Swing at full speed toward the wall
3. **Expected**: Player collides with wall surface, does not pass through
4. Repeat 10+ times at various angles
5. **Pass**: Zero penetration across all attempts
6. **Fail**: Player clips through wall even once

### V7: Tether Obstruction Auto-Release (FR-010, SC-005)

1. Attach to an anchor
2. Swing around a corner so a building blocks the line between player and anchor
3. **Expected**: After ~0.3 seconds (configurable grace period), grapple auto-releases
4. Player launches with current velocity, no rubber-banding
5. **Pass**: Auto-release triggers within grace period, velocity preserved
6. **Fail**: No auto-release, or player snaps backward through the building

### V8: Stuck-State Recovery (FR-011)

1. Set up a scenario where the player could clip into geometry (use Scene view to manually position player inside a wall while attached)
2. **Expected**: Stuck detector fires (velocity below threshold while overlapping geometry)
3. Player repositioned to last valid position, grapple released
4. **Pass**: Player recovers to a safe position with no further sticking
5. **Fail**: Player remains stuck, or recovery position is invalid

### V9: IPlayerTelemetry Contract (FR-012, SC-007)

1. Create a test MonoBehaviour that takes a `MonoBehaviour` reference in Inspector
2. In Awake, cast to `IPlayerTelemetry` - do NOT import PlayerController or GrapplingHook
3. In Update, log: Position, Velocity, CurrentSpeed, IsGrounded, IsSwinging, CurrentGrapplePoint
4. Assign the Player GameObject to the reference field
5. Run the scene; walk, jump, swing
6. **Expected**: All values update correctly for each state
7. **Pass**: Compiles with only `TheLastDelivery.Interfaces` import; values are accurate
8. **Fail**: Requires concrete class import, or values are stale/incorrect

### V10: Animator Parameters (FR-013)

1. Add an Animator component to the player with parameters matching `AnimatorParameterNames` constants
2. Run the scene; walk, jump, swing
3. Open Animator window, watch parameter values
4. **Expected**: Speed, VerticalVelocity, IsGrounded, IsSwinging, IsLanding all update in real-time
5. **Fail**: Parameters don't exist, don't update, or use different names than constants

### V11: Zero GC Allocations (FR-015, SC-006)

1. Open Unity Profiler window, enable CPU module with "GC Alloc" column visible
2. Run test scene, perform 60 seconds of continuous swinging (attach, swing, reel, release, repeat)
3. Filter Profiler to PlayerController, GrapplingHook, and related scripts
4. **Expected**: GC Alloc column shows 0 B for all grapple-related frames
5. **Pass**: Zero allocations across the full 60-second session
6. **Fail**: Any non-zero GC allocation in grapple code paths

### V12: Ground Locomotion Regression (SC-008)

1. Run test scene WITHOUT attaching grapple
2. Walk, run, jump, air-steer — exercise all existing locomotion
3. Compare movement speed, jump height, air-steer responsiveness to pre-feature baseline
4. **Pass**: Identical behavior to baseline
5. **Fail**: Any measurable regression in movement feel or values

# Data Model: Pendulum Grapple Swing & Variable Tether Control

**Date**: 2026-11-13
**Feature**: `001-grapple-swing`

## Entities

### SwingConfig (ScriptableObject)

Pure data asset consumed by the grapple system. No behavior. One instance per project (or per difficulty preset if needed later).

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| swingGravityMultiplier | float | 1.0 | Scales Physics.gravity during swing arcs |
| swingDamping | float | 0.995 | Per-FixedUpdate velocity damping (1.0 = no damping) |
| swingSteerForce | float | 12.0 | Lateral steering force magnitude (m/s^2) |
| reelInSpeed | float | 8.0 | Tether shortening rate (m/s) |
| reelOutSpeed | float | 6.0 | Tether lengthening rate (m/s) |
| minTetherLength | float | 3.0 | Minimum allowed rope length (m) |
| maxTetherLength | float | 40.0 | Maximum allowed rope length (m) |
| releaseBoostMultiplier | float | 1.15 | Velocity multiplier on grapple release |
| maxAnchorRange | float | 35.0 | Maximum detection range for anchor targeting |
| viewConeWeight | float | 0.6 | Weight of camera alignment in anchor scoring (0-1) |
| distanceWeight | float | 0.4 | Weight of proximity in anchor scoring (0-1) |
| obstructionGracePeriod | float | 0.3 | Seconds of tether obstruction before auto-release |
| stuckVelocityThreshold | float | 0.5 | Velocity magnitude (m/s) below which stuck detection activates |
| stuckRecoveryImpulse | float | 5.0 | Upward impulse (m/s) applied on stuck-state recovery |
| anchorLayerMask | LayerMask | ~0 | Physics layers for anchor overlap queries |
| obstacleLayerMask | LayerMask | ~0 | Physics layers for line-of-sight and collision checks |

**Validation rules**:
- `minTetherLength` > 0 and < `maxTetherLength`
- `viewConeWeight` + `distanceWeight` should equal 1.0 (enforced via normalization at runtime, not hard error)
- All speed/force values > 0
- `obstructionGracePeriod` >= 0 (0 = instant release)

### TetherState (Runtime, not persisted)

Internal runtime state of the active grapple connection. Managed by the grapple system, not exposed directly — consumers read via `IPlayerTelemetry`.

| Field | Type | Description |
|-------|------|-------------|
| isAttached | bool | Whether grapple is currently connected |
| anchorPoint | Vector3 | World position of the attached anchor |
| anchorTransform | Transform | Reference to anchor's Transform (for destroyed detection) |
| currentLength | float | Current tether rope length |
| obstructionTimer | float | Seconds of continuous tether obstruction |
| lastValidPosition | Vector3 | Last player position not overlapping geometry |
| swingVelocity | Vector3 | Current velocity during swing (Rigidbody-driven) |

**State transitions**:
```
Detached ──[Grapple input + valid anchor]──▶ Attached
Attached ──[Release input]──▶ Detached (velocity transfer)
Attached ──[Obstruction timer expired]──▶ Detached (auto-release)
Attached ──[Stuck detected]──▶ Detached (recovery reposition)
Attached ──[Anchor destroyed/disabled]──▶ Detached (auto-release)
Attached ──[Kill plane proximity]──▶ Detached (auto-release)
```

### AnchorCandidate (Runtime, not persisted)

Transient scoring data during anchor selection. Computed each frame, never stored beyond the current frame.

| Field | Type | Description |
|-------|------|-------------|
| collider | Collider | Reference from OverlapSphereNonAlloc buffer |
| worldPosition | Vector3 | collider.bounds.center |
| distance | float | Distance from player to anchor |
| viewConeScore | float | Dot product of camera forward and direction to anchor (0-1) |
| combinedScore | float | Weighted sum of normalized distance and viewConeScore |
| hasLineOfSight | bool | Raycast clear from player to anchor |

### AnimatorParameterNames (Static constants)

Shared string constants for Animator parameter names. Defined once, referenced by all systems that drive the Animator.

| Constant | Value (string) | Type in Animator |
|----------|---------------|-----------------|
| Speed | "Speed" | Float |
| VerticalVelocity | "VerticalVelocity" | Float |
| IsGrounded | "IsGrounded" | Bool |
| IsSwinging | "IsSwinging" | Bool |
| IsLanding | "IsLanding" | Bool |

## Relationships

```
SwingConfig ──[read by]──▶ GrapplingHook (refactored)
SwingConfig ──[read by]──▶ Anchor targeting logic
PlayerController ──[implements]──▶ IPlayerState (existing)
PlayerController ──[implements]──▶ IPlayerTelemetry (new)
PlayerController ──[implements]──▶ IPlayerHUDData (existing)
GrapplingHook ──[writes to]──▶ TetherState
GrapplingHook ──[updates]──▶ IPlayerTelemetry.CurrentGrapplePoint
GrapplingHook ──[calls]──▶ PlayerController.LaunchWithVelocity()
GrapplingHook ──[disables/enables]──▶ CharacterController
GrapplingHook ──[controls]──▶ Rigidbody (kinematic toggle)
AnimatorParameterNames ──[referenced by]──▶ PlayerController (Animator.SetFloat/SetBool)
AnimatorParameterNames ──[referenced by]──▶ Asfa's animation/VFX scripts (read-only)
```

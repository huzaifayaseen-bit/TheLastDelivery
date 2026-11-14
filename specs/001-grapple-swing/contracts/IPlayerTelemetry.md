# Contract: IPlayerTelemetry

**Date**: 2026-11-13
**Feature**: `001-grapple-swing`
**Owner**: Huzaifa (Player/Grapple systems)
**Consumers**: Asfa (HUD/Anim/VFX), Hamza (AI/Drones), Anusha (Level/GameLoop)

## Purpose

Read-only interface exposing live player state to all team subsystems. Extends the existing `IPlayerState` contract with grapple-specific telemetry. Consumers observe but never mutate player state through this interface.

## Interface Definition

```csharp
namespace TheLastDelivery.Interfaces
{
    public interface IPlayerTelemetry : IPlayerState
    {
        // Inherited from IPlayerState:
        // Vector3 Position { get; }
        // Vector3 Velocity { get; }
        // bool IsGrounded { get; }
        // bool IsSwinging { get; }

        /// Current horizontal speed magnitude (m/s), suitable for HUD speedometer.
        float CurrentSpeed { get; }

        /// World position of the currently targeted grapple anchor, or null if none.
        Vector3? CurrentGrapplePoint { get; }

        /// Whether a valid grapple target is available (for HUD crosshair visibility).
        bool HasGrappleTarget { get; }
    }
}
```

## Contract Rules

1. **Read-only**: No setters. No methods that mutate state. Consumers receive a snapshot of current state each frame.
2. **Null safety**: `CurrentGrapplePoint` is `Vector3?` (nullable). Consumers MUST check `HasGrappleTarget` or null-check before using the position.
3. **Frame consistency**: All properties reflect the same point in time (read during or after Update, before next frame begins).
4. **No concrete dependencies**: Consumer scripts MUST reference `IPlayerTelemetry`, never `PlayerController` or `GrapplingHook` directly. This is enforced by assembly/namespace convention (Principle I).
5. **Backward compatibility**: `IPlayerTelemetry` extends `IPlayerState`. Any code already using `IPlayerState` continues to work without modification.

## Access Pattern

```csharp
// Consumer example (Asfa's HUD script):
[SerializeField] private MonoBehaviour playerReference; // Assign in Inspector
private IPlayerTelemetry telemetry;

void Awake()
{
    telemetry = playerReference as IPlayerTelemetry;
}

void Update()
{
    speedText.text = telemetry.CurrentSpeed.ToString("F1");
    crosshair.enabled = telemetry.HasGrappleTarget;
    if (telemetry.CurrentGrapplePoint.HasValue)
        crosshair.position = Camera.main.WorldToScreenPoint(telemetry.CurrentGrapplePoint.Value);
}
```

## Acceptance Criteria

- [ ] Interface compiles independently (no imports beyond UnityEngine and TheLastDelivery.Interfaces)
- [ ] `PlayerController` implements `IPlayerTelemetry` and all properties return accurate values
- [ ] `CurrentGrapplePoint` returns the targeted anchor position when one is available, null otherwise
- [ ] A test script referencing only `IPlayerTelemetry` compiles without importing Player namespace
- [ ] All properties produce zero GC allocations when read

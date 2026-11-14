# Contract: AnimatorParameterNames

**Date**: 2026-11-13
**Feature**: `001-grapple-swing`
**Owner**: Huzaifa (defines constants), Asfa (owns Animator Controller asset)
**Consumers**: All team members who drive or read Animator state

## Purpose

Single source of truth for Animator parameter name strings. Prevents typo-driven bugs across team boundaries (Constitution, Development Workflow section). All Animator.SetFloat/SetBool calls MUST use these constants instead of inline string literals.

## Definition

```csharp
namespace TheLastDelivery
{
    public static class AnimatorParameterNames
    {
        public const string Speed = "Speed";
        public const string VerticalVelocity = "VerticalVelocity";
        public const string IsGrounded = "IsGrounded";
        public const string IsSwinging = "IsSwinging";
        public const string IsLanding = "IsLanding";
    }
}
```

## Contract Rules

1. **Immutable**: `const string` fields only. No runtime modifications.
2. **Single file**: All shared Animator parameter names live in this one static class.
3. **Name = value**: The constant name matches the Animator parameter name exactly.
4. **Additions require team agreement**: Adding a new parameter name requires all team members to update their Animator Controller and consuming code.
5. **Hash caching**: For performance, consuming scripts SHOULD cache `Animator.StringToHash()` results in Awake rather than calling it every frame.

## Usage Pattern

```csharp
// In Awake:
private int speedHash;
private int isSwingingHash;

void Awake()
{
    speedHash = Animator.StringToHash(AnimatorParameterNames.Speed);
    isSwingingHash = Animator.StringToHash(AnimatorParameterNames.IsSwinging);
}

// In Update:
animator.SetFloat(speedHash, telemetry.CurrentSpeed);
animator.SetBool(isSwingingHash, telemetry.IsSwinging);
```

## Acceptance Criteria

- [ ] Static class compiles with no dependencies beyond System
- [ ] All Animator.Set* calls in the codebase use constants from this class (no inline strings)
- [ ] Animator Controller asset has matching parameters for each constant defined here

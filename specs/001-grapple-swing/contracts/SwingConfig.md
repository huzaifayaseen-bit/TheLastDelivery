# Contract: SwingConfig ScriptableObject

**Date**: 2026-11-13
**Feature**: `001-grapple-swing`
**Owner**: Huzaifa (defines structure), All team members (tune values)
**Consumers**: GrapplingHook, anchor targeting system

## Purpose

Data-only ScriptableObject asset containing all tunable swing/grapple parameters. Satisfies Constitution Principle II: no hardcoded physics constants. Level designers adjust values in the Unity Inspector without touching C# source.

## Definition

```csharp
namespace TheLastDelivery.Player
{
    [CreateAssetMenu(fileName = "SwingConfig", menuName = "TheLastDelivery/Swing Config")]
    public class SwingConfig : ScriptableObject
    {
        [Header("Pendulum Physics")]
        public float swingGravityMultiplier = 1.0f;
        public float swingDamping = 0.995f;
        public float swingSteerForce = 12.0f;

        [Header("Tether Control")]
        public float reelInSpeed = 8.0f;
        public float reelOutSpeed = 6.0f;
        public float minTetherLength = 3.0f;
        public float maxTetherLength = 40.0f;

        [Header("Release")]
        public float releaseBoostMultiplier = 1.15f;

        [Header("Anchor Targeting")]
        public float maxAnchorRange = 35.0f;
        [Range(0f, 1f)] public float viewConeWeight = 0.6f;
        [Range(0f, 1f)] public float distanceWeight = 0.4f;
        public LayerMask anchorLayerMask = ~0;

        [Header("Collision Safety")]
        public LayerMask obstacleLayerMask = ~0;
        public float obstructionGracePeriod = 0.3f;
        public float stuckVelocityThreshold = 0.5f;
        public float stuckRecoveryImpulse = 5.0f;
    }
}
```

## Contract Rules

1. **Pure data**: No methods, no behavior, no MonoBehaviour references. Fields only.
2. **Serialized defaults**: Every field has a sensible default so a newly created asset works out of the box.
3. **Public fields**: Used instead of `[SerializeField] private` for simplicity — ScriptableObjects are data containers, not encapsulated services.
4. **One asset per config profile**: Create via Assets > Create > TheLastDelivery > Swing Config.
5. **Runtime read-only**: Scripts read values from the asset; they never write to it at runtime.

## Asset Workflow

1. Create asset: Right-click in Project window > Create > TheLastDelivery > Swing Config
2. Assign to GrapplingHook component via Inspector (`[SerializeField] private SwingConfig config`)
3. Adjust values in Inspector; changes take effect immediately in Play mode (ScriptableObject hot-reload)

## Acceptance Criteria

- [ ] ScriptableObject creates successfully via the Create Asset menu
- [ ] All grapple/swing code reads from SwingConfig fields, not from hardcoded values
- [ ] Changing a value in the Inspector during Play mode takes effect immediately (next FixedUpdate)
- [ ] No per-frame allocations from reading ScriptableObject fields

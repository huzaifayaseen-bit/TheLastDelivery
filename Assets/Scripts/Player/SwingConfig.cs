using UnityEngine;

namespace TheLastDelivery.Player
{
    [CreateAssetMenu(fileName = "SwingConfig", menuName = "TheLastDelivery/Swing Config")]
    public class SwingConfig : ScriptableObject
    {
        [Header("Spider-Man Web Swing Physics")]
        public float swingGravityMultiplier = 0.65f;
        public float swingDamping = 0.996f;
        public float swingSteerForce = 22.0f;
        public float forwardSwingDrive = 16.0f;
        public float webPullForce = 28.0f;

        [Header("Tether Control")]
        public float reelInSpeed = 11.0f;
        public float reelOutSpeed = 6.0f;
        public float minTetherLength = 4.0f;
        public float maxTetherLength = 40.0f;

        [Header("Release Launch (Web Zip)")]
        public float releaseBoostMultiplier = 1.25f;
        public float releaseForwardBoost = 10.0f;
        public float releaseUpwardBoost = 8.5f;

        [Header("Anchor Targeting")]
        public float maxAnchorRange = 40.0f;
        [Range(0f, 1f)] public float viewConeWeight = 0.65f;
        [Range(0f, 1f)] public float distanceWeight = 0.35f;
        public LayerMask anchorLayerMask = ~0;

        [Header("Collision Safety")]
        public LayerMask obstacleLayerMask = ~0;
        public float obstructionGracePeriod = 0.3f;
        public float stuckVelocityThreshold = 0.5f;
        public float stuckRecoveryImpulse = 5.0f;
    }
}
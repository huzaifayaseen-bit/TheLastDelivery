using UnityEngine;
using TheLastDelivery.Interfaces;

namespace TheLastDelivery.Tests
{
    /// <summary>
    /// Test consumer verifying the IPlayerTelemetry integration contract (FR-012, SC-007, quickstart V9).
    /// References ONLY TheLastDelivery.Interfaces — has zero dependencies on concrete PlayerController or GrapplingHook types.
    /// </summary>
    public class PlayerTelemetryTestConsumer : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour playerReference;

        private IPlayerTelemetry telemetry;

        public IPlayerTelemetry Telemetry => telemetry;

        private void Awake()
        {
            if (playerReference != null)
            {
                telemetry = playerReference as IPlayerTelemetry;
            }
        }

        private void Update()
        {
            if (telemetry == null && playerReference != null)
            {
                telemetry = playerReference as IPlayerTelemetry;
            }

            if (telemetry != null)
            {
                // Verify all IPlayerTelemetry and IPlayerState contract properties compile and are readable
                Vector3 pos = telemetry.Position;
                Vector3 vel = telemetry.Velocity;
                float speed = telemetry.CurrentSpeed;
                bool grounded = telemetry.IsGrounded;
                bool swinging = telemetry.IsSwinging;
                bool hasTarget = telemetry.HasGrappleTarget;
                Vector3? targetPoint = telemetry.CurrentGrapplePoint;

                _ = pos;
                _ = vel;
                _ = speed;
                _ = grounded;
                _ = swinging;
                _ = hasTarget;
                _ = targetPoint;
            }
        }
    }
}

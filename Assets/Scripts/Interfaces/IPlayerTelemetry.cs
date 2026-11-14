using UnityEngine;

namespace TheLastDelivery.Interfaces
{
    public interface IPlayerTelemetry : IPlayerState
    {
        float CurrentSpeed { get; }
        Vector3? CurrentGrapplePoint { get; }
        bool HasGrappleTarget { get; }
    }
}

using UnityEngine;

namespace TheLastDelivery.Interfaces
{
    public interface IPlayerState
    {
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        bool IsGrounded { get; }
        bool IsSwinging { get; }
    }
}

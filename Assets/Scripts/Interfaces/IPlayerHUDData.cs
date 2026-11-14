namespace TheLastDelivery.Interfaces
{
    public interface IPlayerHUDData
    {
        float CurrentSpeed { get; }
        float IntegrityPercentage { get; }
        float RemainingTime { get; }
    }
}

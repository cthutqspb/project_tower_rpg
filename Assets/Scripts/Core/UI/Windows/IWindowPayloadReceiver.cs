namespace ProjectTowerRpg.Core.UI
{
    // Окно, которое принимает произвольные данные при открытии (payload).
    public interface IPayloadReceiver
    {
        void ReceivePayload(object payload);
    }
}

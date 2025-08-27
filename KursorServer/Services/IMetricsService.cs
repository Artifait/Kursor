namespace KursorServer.Services
{
    /// <summary>
    /// Интерфейс метрик — реализуется либо NoOp (по умолчанию), либо SimpleMetricsService при включении в конфиг.
    /// </summary>
    public interface IMetricsService
    {
        void IncPush();
        void IncDispatch();
        void IncDispatchError();
        void ObserveDispatchLatency(double ms);
        void SetRoomsCount(int count);
        /// <summary>
        /// Возвращает текстовую (простую) экспозицию метрик — plain text.
        /// </summary>
        string GetMetricsText();
    }
}

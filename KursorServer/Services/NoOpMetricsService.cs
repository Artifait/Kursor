namespace KursorServer.Services
{
    public class NoOpMetricsService : IMetricsService
    {
        public void IncDispatch() { }
        public void IncDispatchError() { }
        public void IncPush() { }
        public string GetMetricsText() => "# metrics disabled\n";
        public void ObserveDispatchLatency(double ms) { }
        public void SetRoomsCount(int count) { }
    }
}

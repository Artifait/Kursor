using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace KursorServer.Services
{
    /// <summary>
    /// Простая встроенная in-memory система метрик.
    /// При включении (Metrics:Enabled = true в appsettings или env) регистрируется вместо NoOp.
    /// Экспозиция /metrics возвращает простой plain-text (числа и пара ключей).
    /// Не предназначена для продвинутого мониторинга, но позволяет быстро включить сбор.
    /// </summary>
    public class SimpleMetricsService : IMetricsService
    {
        private long _pushCount;
        private long _dispatchCount;
        private long _dispatchErrors;
        private long _roomsCount;

        // latency samples (ring buffer)
        private readonly ConcurrentQueue<double> _latencySamples = new();
        private const int MaxLatencySamples = 1000;

        public void IncPush() => Interlocked.Increment(ref _pushCount);
        public void IncDispatch() => Interlocked.Increment(ref _dispatchCount);
        public void IncDispatchError() => Interlocked.Increment(ref _dispatchErrors);
        public void SetRoomsCount(int count) => Interlocked.Exchange(ref _roomsCount, count);

        public void ObserveDispatchLatency(double ms)
        {
            _latencySamples.Enqueue(ms);
            while (_latencySamples.Count > MaxLatencySamples && _latencySamples.TryDequeue(out _)) { }
        }

        public string GetMetricsText()
        {
            // compute simple stats
            var sb = new StringBuilder();
            sb.AppendLine("# kursor simple metrics");
            sb.AppendLine($"kursor_push_total {Interlocked.Read(ref _pushCount)}");
            sb.AppendLine($"kursor_dispatched_total {Interlocked.Read(ref _dispatchCount)}");
            sb.AppendLine($"kursor_dispatch_errors_total {Interlocked.Read(ref _dispatchErrors)}");
            sb.AppendLine($"kursor_rooms_current {Interlocked.Read(ref _roomsCount)}");

            // latency stats (avg, min, max)
            double avg = 0, min = double.MaxValue, max = double.MinValue;
            int n = 0;
            foreach (var v in _latencySamples)
            {
                n++;
                avg += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (n > 0)
            {
                avg /= n;
                sb.AppendLine($"kursor_dispatch_latency_avg_ms {avg:F2}");
                sb.AppendLine($"kursor_dispatch_latency_min_ms {min:F2}");
                sb.AppendLine($"kursor_dispatch_latency_max_ms {max:F2}");
                sb.AppendLine($"kursor_dispatch_latency_samples {n}");
            }
            else
            {
                sb.AppendLine("kursor_dispatch_latency_avg_ms 0");
                sb.AppendLine("kursor_dispatch_latency_min_ms 0");
                sb.AppendLine("kursor_dispatch_latency_max_ms 0");
                sb.AppendLine("kursor_dispatch_latency_samples 0");
            }

            return sb.ToString();
        }
    }
}

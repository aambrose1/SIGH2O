using app.Models;

namespace app.Services;

public class SensorService
{
    private readonly List<SensorLogs> _history = new();
    private readonly object _lock = new();

    public const double AlertThreshold = 20.0;

    public SensorLogs? Latest { get; private set; }
    public IReadOnlyList<SensorLogs> History
    {
        get { lock (_lock) { return _history.AsReadOnly(); } }
    }

    public bool IsAlert => Latest is not null && Latest.MoistureValue < AlertThreshold;

    public event Action? OnChange;

    public void Push(SensorLogs log)
    {
        lock (_lock)
        {
            _history.Add(log);
            if (_history.Count > 200)
                _history.RemoveAt(0);
        }
        Latest = log;
        OnChange?.Invoke();
    }

    public void Seed(IEnumerable<SensorLogs> logs)
    {
        lock (_lock)
        {
            _history.Clear();
            _history.AddRange(logs.TakeLast(200));
        }
        Latest = _history.LastOrDefault();
        OnChange?.Invoke();
    }
}
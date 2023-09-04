using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Worker;

public class EventConsumerMetrics : IDisposable
{
    public const string MeterName = "Sample.KafkaConsumer";

    private readonly TimeProvider _timeProvider;
    private readonly Meter _meter;
    private readonly UpDownCounter<long> _activeEventHandlingCounter;
    private readonly Histogram<double> _eventHandlingDuration;

    public EventConsumerMetrics(
        IMeterFactory meterFactory,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _meter = meterFactory.Create(MeterName);

        _activeEventHandlingCounter = _meter.CreateUpDownCounter<long>(
            "kafka_consumer_event_active",
            unit: "{event}",
            description: "Number of events currently being handled");

        _eventHandlingDuration = _meter.CreateHistogram<double>(
            "kafka_consumer_event_duration",
            unit: "s",
            description: "Measures the duration of inbound events");
    }

    public long EventHandlingStart(string topic)
    {
        if (_activeEventHandlingCounter.Enabled)
        {
            var tags = new TagList { { "topic", topic } };
            _activeEventHandlingCounter.Add(1, tags);
        }

        return _timeProvider.GetTimestamp();
    }

    public void EventHandlingEnd(string topic, long startingTimestamp)
    {
        var tags = _activeEventHandlingCounter.Enabled || _eventHandlingDuration.Enabled
            ? new TagList { { "topic", topic } }
            : default;

        if (_activeEventHandlingCounter.Enabled)
        {
            _activeEventHandlingCounter.Add(-1, tags);
        }

        if (_eventHandlingDuration.Enabled)
        {
            var elapsed = _timeProvider.GetElapsedTime(startingTimestamp);
            
            _eventHandlingDuration.Record(
                elapsed.TotalSeconds,
                tags);
        }
    }

    public void Dispose() => _meter.Dispose();
}
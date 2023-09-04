using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api;

public sealed class EventPublisherMetrics : IDisposable
{
    public const string MeterName = "Sample.KafkaProducer";
    
    private readonly Meter _meter;
    private readonly Counter<long> _publishedEventsCounter;
    

    public EventPublisherMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        
        _publishedEventsCounter = _meter.CreateCounter<long>(
            "kafka.publisher.published_events",
            unit: "{event}",
            description: "The number of events published by the API");
        
    }

    public void EventPublished(string topic)
    {
        if (_publishedEventsCounter.Enabled)
        {
            var tags = new TagList { { "topic", topic } };
            _publishedEventsCounter.Add(1, tags);
        }
    }
    
    public void Dispose() => _meter.Dispose();
}
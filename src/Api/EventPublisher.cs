using Confluent.Kafka;
using Shared;

namespace Api;

public class EventPublisher
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly IProducer<Guid, StuffHappened> _producer;
    private readonly EventPublisherMetrics _metrics;

    public EventPublisher(
        KafkaSettings kafkaSettings,
        IProducer<Guid, StuffHappened> producer,
        EventPublisherMetrics metrics)
    {
        _kafkaSettings = kafkaSettings;
        _producer = producer;
        _metrics = metrics;
    }

public async Task PublishAsync(StuffHappened @event)
{
    using var activity = EventPublisherActivitySource.StartActivity(
        _kafkaSettings.Topic,
         @event);

    await _producer.ProduceAsync(
        _kafkaSettings.Topic,
         new Message<Guid, StuffHappened>
    {
        Key = @event.Id,
        Value = @event,
        Headers = EventPublisherActivitySource.EnrichHeadersWithTracingContext(
            activity,
             new Headers())
    });

    _metrics.EventPublished(_kafkaSettings.Topic);
}

    
}
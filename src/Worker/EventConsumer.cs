using Confluent.Kafka;
using Dapper;
using Npgsql;
using Shared;

namespace Worker;

public class EventConsumer : BackgroundService
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly NpgsqlDataSource _dataSource;
    private readonly EventConsumerMetrics _metrics;
    private readonly ILogger<EventConsumer> _logger;

    public EventConsumer(
        KafkaSettings kafkaSettings,
        NpgsqlDataSource dataSource,
        EventConsumerMetrics metrics,
        ILogger<EventConsumer> logger)
    {
        _kafkaSettings = kafkaSettings;
        _dataSource = dataSource;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // polling for messages is a blocking operation,
        // so using Task.Run to do it in the background
        // ReSharper disable once MethodSupportsCancellation - trying to stop gracefully inside
        await Task.Run(async () =>
        {
            var consumerConfig = new ConsumerConfig
            {
                GroupId = "worker",
                BootstrapServers = _kafkaSettings.BootstrapServers,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                AutoCommitIntervalMs = 0
            };

            using var consumer = new ConsumerBuilder<Guid, StuffHappened>(consumerConfig)
                .SetKeyDeserializer(new GuidSerde())
                .SetValueDeserializer(new JsonEventSerde<StuffHappened>())
                .Build();

            consumer.Subscribe(_kafkaSettings.Topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    await HandleConsumeResultAsync(consumer, consumeResult, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Shutting down gracefully");
                }
                catch (Exception ex)
                {
                    // TODO: implement error handling/retry logic
                    // like this, the failed message will eventually be "marked as processed"
                    // (commit to a newer offset) even though it failed
                    _logger.LogError(ex, "Error occurred when consuming event!");
                }
            }
        });
    }

    private async Task HandleConsumeResultAsync(
        IConsumer<Guid, StuffHappened> consumer,
        ConsumeResult<Guid, StuffHappened> consumeResult,
        CancellationToken stoppingToken)
    {
        var startingTimestamp = _metrics.EventHandlingStart(_kafkaSettings.Topic);
        
        try
        {
            using var activity = EventConsumerActivitySource.StartActivity(
                _kafkaSettings.Topic,
                consumeResult.Message.Value,
                consumeResult.Message.Headers);

            await HandleEventAsync(consumeResult.Message.Value, stoppingToken);

            consumer.Commit(); // note: committing every time can have a negative impact on performance
        }
        finally
        {
            _metrics.EventHandlingEnd(_kafkaSettings.Topic, startingTimestamp);
        }
    }

    // this is the actual "business logic", the rest is infra and would be abstracted if this wasn't a demo ;)
    private async Task HandleEventAsync(StuffHappened @event, CancellationToken ct)
    {
        _logger.LogInformation("\"{@What}\" happened!", @event.What);

        await using var connection = _dataSource.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO StuffThanHappened(What) VALUES(@what)",
            new { what = @event.What });

        await DoRandomStupidThingsToSpiceUpTheDemo(ct);
    }

    private async Task DoRandomStupidThingsToSpiceUpTheDemo(CancellationToken ct)
    {
        // to get some variation in the percentiles
        var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        await Task.Delay(delay, ct);
        _logger.LogDebug("Event handling delay {Delay}", delay);

        // creating some garbage for the GC to get to work (and we see in the graphs)
        var small = new byte[Random.Shared.Next(1, 1024)];
        var big = new byte[Random.Shared.Next(1024, 1024 * 1024 * 10)];
        _logger.LogDebug("{GarbageBytes} bytes", small.Length + big.Length);
    }
}
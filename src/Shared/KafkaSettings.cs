namespace Shared;

public record KafkaSettings
{
    public required string Topic { get; init; }
    public required string BootstrapServers { get; init; }
}
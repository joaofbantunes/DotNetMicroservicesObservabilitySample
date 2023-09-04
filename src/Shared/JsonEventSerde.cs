using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace Shared;

public class JsonEventSerde<T> : ISerializer<T>, IDeserializer<T> where T : class
{
    public byte[] Serialize(T data, SerializationContext context)
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));

    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        => isNull ? null : JsonSerializer.Deserialize<T>(data);
}
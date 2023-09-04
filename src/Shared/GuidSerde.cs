using Confluent.Kafka;

namespace Shared;

public class GuidSerde : ISerializer<Guid>, IDeserializer<Guid>
{
    public byte[] Serialize(Guid data, SerializationContext context) => data.ToByteArray();

    public Guid Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context) => new(data);
}
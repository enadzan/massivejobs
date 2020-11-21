using System;
using System.Text;
using System.Text.Json;

namespace MassiveJobs.Core.Serialization
{
    public class DefaultSerializer : BaseSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IgnoreNullValues = true, IgnoreReadOnlyProperties = true };

        protected override byte[] SerializeEnvelope(SerializedEnvelope<object> envelope)
        {
            var json = JsonSerializer.Serialize(envelope, Options);
            return Encoding.UTF8.GetBytes(json);
        }

        protected override object DeserializeEnvelope(ReadOnlySpan<byte> data, Type envelopeType)
        {
            return JsonSerializer.Deserialize(data, envelopeType, Options);
        }
    }
}

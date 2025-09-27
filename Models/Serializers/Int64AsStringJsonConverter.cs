using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Models.Serializers
{
    public sealed class Int64AsStringJsonConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String when long.TryParse(reader.GetString(), out var value) => value,
                JsonTokenType.Number => reader.GetInt64(),
                _ => throw new JsonException($"Unable to convert \"{reader.GetString()}\" to System.Int64.")
            };
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}

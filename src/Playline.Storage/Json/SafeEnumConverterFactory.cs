using System.Text.Json;
using System.Text.Json.Serialization;

namespace Playline.Storage.Json;

internal sealed class SafeEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)Activator.CreateInstance(
            typeof(SafeEnumConverter<>).MakeGenericType(typeToConvert),
            nonPublic: true)!;
    }

    private sealed class SafeEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<TEnum>(reader.GetString(), ignoreCase: true, out var stringValue)
                && Enum.IsDefined(stringValue))
            {
                return stringValue;
            }

            if (reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt64(out var numericValue))
            {
                var numericEnum = (TEnum)Enum.ToObject(typeof(TEnum), numericValue);
                if (Enum.IsDefined(numericEnum))
                {
                    return numericEnum;
                }
            }

            return (TEnum)Enum.ToObject(typeof(TEnum), -1);
        }

        public override void Write(
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

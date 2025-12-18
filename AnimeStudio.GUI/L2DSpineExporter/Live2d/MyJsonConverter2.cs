using System;
using Newtonsoft.Json;

namespace UnityLive2DExtractor
{
    public class MyJsonConverter2 : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(float);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Convert(writer, (float)value);
        }

        private void Convert(JsonWriter writer, float value)
        {
            writer.WriteRawValue(value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

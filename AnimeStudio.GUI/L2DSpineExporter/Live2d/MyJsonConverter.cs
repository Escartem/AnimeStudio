using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace UnityLive2DExtractor
{
    public class MyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<float>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            Convert(writer, (List<float>)value);
            writer.WriteEndArray();
        }

        private void Convert(JsonWriter writer, List<float> array)
        {
            foreach (var n in array)
            {
                var v = n.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                writer.WriteRawValue(v);
            }
        }
    }
}
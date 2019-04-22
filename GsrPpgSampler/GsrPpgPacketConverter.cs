using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InsLab.Signal
{
    internal class GsrPpgPacketConverter : JsonConverter<GsrPpgPacket>
    {
        public override GsrPpgPacket ReadJson(JsonReader reader, Type objectType, GsrPpgPacket existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = serializer.Deserialize<JToken>(reader) as JObject;

            string typeName = obj["TypeName"]?.ToString();

            switch (typeName)
            {
                case nameof(GsrPpgPacket):
                    return obj.ToObject<GsrPpgPacket>();
                default:
                    break;
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, GsrPpgPacket value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}

using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InsLab.Signal
{
    using GsrPpgDataCollection = Dictionary<string, List<GsrPpgPacket>>;

    public static class GsrPpgUtil
    {
        public static GsrPpgDataCollection ReadGsrPpgData(string path)
        {
            var jsonString = File.ReadAllText(path);
            var jsonObj = JObject.Parse(jsonString);

            foreach (var property in jsonObj.Properties().ToList())
            {
                // if it's not the property of GSR/PPG
                if (property.Name != "GSR" && property.Name != "PPG")
                {
                    property.Remove();
                }
            }

            var serializer = new JsonSerializer();
            serializer.Converters.Add(new GsrPpgPacketConverter());

            var gsrPpgDataCollection = jsonObj.ToObject<GsrPpgDataCollection>(serializer);

            return gsrPpgDataCollection;
        }
    }
}

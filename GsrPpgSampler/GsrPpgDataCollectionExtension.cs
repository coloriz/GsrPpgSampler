using System;
using System.Collections.Generic;
using System.Linq;

namespace InsLab.Signal
{
    using GsrPpgDataCollection = Dictionary<string, List<GsrPpgPacket>>;

    public static class GsrPpgDataCollectionExtension
    {
        public static GsrPpgDataCollection SelectByTime(this GsrPpgDataCollection data, TimeSpan start, TimeSpan span)
        {
            var end = start + span;
            var result = new GsrPpgDataCollection();

            foreach (var d in data)
            {
                result[d.Key] = d.Value.Where(e => start.Ticks <= e.Timestamp && e.Timestamp <= end.Ticks).ToList();
            }

            return result;
        }
    }
}

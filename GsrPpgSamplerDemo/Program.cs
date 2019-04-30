using System;
using System.Linq;
using InsLab.Signal;

namespace GsrPpgSamplerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            //var sampler = new GsrPpgSampler("COM3", 115200, SamplingRate.SR500Hz, SamplingRate.SR500Hz);
            //sampler.GsrDataArrived += (s, e) =>
            //{
            //    Console.WriteLine($"[GSR] Timestamp = {e.Timestamp}, Data = {e.Data}");
            //};

            //sampler.PpgDataArrived += (s, e) =>
            //{
            //    Console.WriteLine($"[Ppg] Timestamp = {e.Timestamp}, Data = {e.Data}");
            //};

            //sampler.StartReading();
            //Console.ReadKey();
            //sampler.StopReading();

            var data = GsrPpgUtil.ReadGsrPpgData("190430.json");

            //var filteredData = GsrPpgUtil.PPGfiltering(data["PPG"].Select(e => e.Value).ToArray());
            var filteredData = GsrPpgUtil.GSRfiltering(data["GSR"]);

            //var filteredData = GsrPpgUtil.GetPPGdata(data["PPG"], TimeSpan.FromMilliseconds(100));

            foreach (var item in filteredData)
            {
                Console.WriteLine(item.Value);
            }
        }
    }
}

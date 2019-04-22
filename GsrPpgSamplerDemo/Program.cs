using System;
using InsLab.Signal;

namespace GsrPpgSamplerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var sampler = new GsrPpgSampler("COM3", 115200, SamplingRate.SR500Hz, SamplingRate.SR500Hz);
            sampler.GsrDataArrived += (s, e) =>
            {
                Console.WriteLine($"[GSR] Timestamp = {e.Timestamp}, Data = {e.Data}");
            };

            sampler.PpgDataArrived += (s, e) =>
            {
                Console.WriteLine($"[Ppg] Timestamp = {e.Timestamp}, Data = {e.Data}");
            };

            sampler.StartReading();
            Console.ReadKey();
            sampler.StopReading();
        }
    }
}

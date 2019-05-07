using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NWaves.Windows;
using NWaves.Signals;
using NWaves.Filters;

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

        static private int ppg_frame_rate = 500;
        static private int ppg_max = 1024;
        static private int ppg_min_bpm = 60;
        static private int ppg_max_bpm = 220;
        static private int ppg_min_peak_distance = ppg_frame_rate / ppg_max_bpm * 60;
        static private int ppg_max_peak_diff = ppg_max / 5;

        static private int gsr_frame_rate = 500;
        static private int gsr_subtract_sample = (gsr_frame_rate* 4) + 1;
        static private int gsr_gaussian_kernel = 51;
        static private int gsr_max = 32676;

        //static private int interval = 300;

        static public List<GsrPpgPacket> GetPPGdata(List<GsrPpgPacket> data, TimeSpan interval)
        {
            var result = new List<GsrPpgPacket>();
            var currentTime = TimeSpan.FromTicks(data.First().Timestamp);
            var lastTime = TimeSpan.FromTicks(data.Last().Timestamp);
            int idx = 0;
            while (currentTime < lastTime)
            {
                var timeList = data.Where(p => ((currentTime - TimeSpan.FromSeconds(5)).Ticks <= p.Timestamp && p.Timestamp <= currentTime.Ticks)).ToList();

                // process
                if (timeList.Count() < 2400)
                {
                    result.Add(new GsrPpgPacket(0, timeList.Last().Timestamp));
                    idx++;
                }
                else
                {
                    result.Add(new GsrPpgPacket((int)PPGfiltering(timeList.Select(e => e.Value).ToArray()).Average(), timeList.Last().Timestamp));
                }
                currentTime += interval;
            }
            
            for (int i = 0; i < idx; i++)
                result[i].Value = result[idx].Value;

            //for (int i = 0; i < result.Count(); i++)
            //    Console.WriteLine($"{TimeSpan.FromTicks(result[i].Timestamp).ToString("g")} : {result[i].Value}");

            return result;
        }

        static private List<int> PPGfiltering(int[] ppgData)
        {
            float lowcut = (float)ppg_min_bpm / 60.0f / ppg_frame_rate / 2.0f;
            float highcut = (float)ppg_max_bpm / 60.0f / ppg_frame_rate / 2.0f;

            var LPfilter = new NWaves.Filters.OnePole.LowPassFilter(highcut);
            var HPfilter = new NWaves.Filters.OnePole.HighPassFilter(lowcut);

            var filteredPPG = LPfilter.ApplyTo(new DiscreteSignal(1, ppgData));
            filteredPPG = HPfilter.ApplyTo(filteredPPG);

            /******************** Median Filtering ********************/
            NWaves.Filters.MedianFilter mFilter = new MedianFilter(167);
            DiscreteSignal mfilteredPPG = mFilter.ApplyTo(filteredPPG);

            float[] arrayPPG = SigToArr(mfilteredPPG);

            var peakIdx = PPGfindPeaks(arrayPPG);

            var inlier = PPGremoveOutofRange(arrayPPG, peakIdx);

            List<float> bpm = new List<float>();

            for (int i = 0; i < inlier.Count() - 1; i++)
            {
                float curBPM = ((float)ppg_frame_rate / ((float)inlier[i + 1] - (float)inlier[i])) * 60.0f;

                if (curBPM > ppg_max_bpm)
                    bpm.Add(0);
                else if (curBPM < ppg_min_bpm)
                    bpm.Add(0);
                else
                    bpm.Add(curBPM);
            }

            List<float> output = new List<float>();
            /*output.Add(90);
            for (int i = 1; i < bpm.Count() - 1; i++)
            {
                if (bpm[i] == 0)
                    output.Add(output[output.Count() - 1]);
                else if (30 < (bpm[i] - bpm[i - 1]))
                    output.Add(output[output.Count() - 1]);
                else
                    output.Add(bpm[i]);
            }*/
            for (int i = 0; i < bpm.Count() - 1; i++)
            {
                if (bpm[i] != 0)
                    output.Add(bpm[i]);
            }
            if (output.Count() == 0)
                output.Add(90);

            //var outputG = GaussianFiltering(7, output.ToArray());

            List<int> toList = new List<int>();
            for (int i = 0; i < output.Count(); i++)
                toList.Add((int)output[i]);

            return toList;
        }

        static public List<GsrPpgPacket> GSRfiltering(List<GsrPpgPacket> GSRdata)
        {
            var gsrData_int = GSRdata.Select(e => gsr_max - e.Value).ToArray();
            var gsrData = Array.ConvertAll(gsrData_int, item => (float)item);

            /******************** Gaussian Filtering ********************/
            var filtered = GaussianFiltering(gsr_gaussian_kernel, gsrData);

            /******************** Median Filtering ********************/
            MedianFilter mFilter = new MedianFilter(gsr_subtract_sample);
            DiscreteSignal tonic_GSR = mFilter.ApplyTo(filtered);

            var phasic_GSR = filtered.Subtract(tonic_GSR);

            /******************* 앞/뒤 데이터 자르는 과정인듯 ********************/
            if (phasic_GSR.Length > gsr_subtract_sample * 2)
            {
                for (int i = 0; i < gsr_subtract_sample; i++)
                    phasic_GSR[i] = 0;
                for (int i = phasic_GSR.Length - 1; i > phasic_GSR.Length - gsr_subtract_sample; i--)
                    phasic_GSR[i] = 0;
            }
            else
            {
                for (int i = 0; i < phasic_GSR.Length; i++)
                    phasic_GSR[i] = 0;
            }

            /******************** Find peak ********************/
            bool checkFlag = false;
            float max = 0;

            int idx1 = 0;

            var findPeak = GSRdata.Select(e => new GsrPpgPacket(0, e.Timestamp)).ToList();
            findPeak[0].Value = 0;

            for (int i = 1; i < filtered.Length; i++)
            {

                if (checkFlag)
                {
                    if (phasic_GSR[i] > 0 && phasic_GSR[i - 1] <= 0)
                    {
                        for (int j = idx1; j < i; j++)
                        {
                            findPeak[j].Value = (int)max;
                        }
                        checkFlag = false;
                    }
                    if (phasic_GSR[i] > max)
                    {
                        max = phasic_GSR[i];
                    }
                }
                else
                {
                    if (phasic_GSR[i] > 0 && phasic_GSR[i - 1] > 0)
                    {
                        idx1 = i;
                        max = 0;
                        checkFlag = true;
                    }
                }
            }

            /******************** Gaussian Filtering ********************/

            //var filteredData = GaussianFiltering(gsr_gaussian_kernel * 10, findPeak.ToArray());
            return findPeak;
        }

        static private int[] PPGremoveOutofRange(float[] signalData, List<int> Idx)
        {
            List<int> inlier = new List<int>();

            NWaves.Filters.MedianFilter mFilter = new MedianFilter();
            DiscreteSignal medianSig = mFilter.ApplyTo(new DiscreteSignal(1, signalData));

            int i = 0;
            for (i = 0; i < Idx.Count(); i++)
            {
                if (signalData[Idx[i]] < (medianSig[Idx[i]] + SampleStandardDeviation(signalData, signalData[Idx[i]])))
                {
                    inlier.Add(Idx[i]);
                }
            }

            return inlier.ToArray();
        }

        static private List<int> PPGfindPeaks(float[] signalData)
        {
            List<int> peakIdx = new List<int>();
            for (int i = 1; i < signalData.Length - 1; i++)
            {
                //Console.WriteLine(signalData[i - 1] + ", " + signalData[i] + ", " + signalData[i + 1]);
                if ((0 > (signalData[i - 1] - signalData[i]) && 0 < (signalData[i] - signalData[i + 1]))
                    || ((signalData[i] - signalData[i - 1]) > 0) && ((signalData[i] - signalData[i + 1]) == 0))
                {
                    peakIdx.Add(i);
                }
            }
            return peakIdx;
        }

        static private float[] SigToArr(DiscreteSignal sig)
        {
            float[] outData = new float[sig.Length];
            for (int i = 0; i < sig.Length; i++)
            {
                outData[i] = sig[i];
            }
            return outData;
        }

        static private DiscreteSignal GaussianFiltering(int kernelSize, float[] signalData)
        {
            float[] PaddingSig = new float[signalData.Count() + (kernelSize - 1) * 2];
            for (int i = 0; i < signalData.Count() + (kernelSize - 1) * 2; i++)
            {
                if (i < (kernelSize - 1))
                {
                    PaddingSig[i] = signalData[0];
                }
                else if (i > PaddingSig.Count() - 1 - (kernelSize - 1))
                {
                    PaddingSig[i] = signalData[signalData.Count() - 1];
                }
                else
                {
                    PaddingSig[i] = signalData[i - (kernelSize - 1)];
                }
            }

            /******************** Gaussian Filtering ********************/
            var gaussFilt = Window.Gaussian(kernelSize);
            DiscreteSignal gKernel = new DiscreteSignal(1, gaussFilt);

            float sum = 0;
            for (int i = 0; i < gKernel.Length; i++)
                sum += gKernel[i];
            // Normalization 
            for (int i = 0; i < gKernel.Length; i++)
                gKernel[i] = gKernel[i] / sum;

            NWaves.Operations.Convolution.Convolver convolver = new NWaves.Operations.Convolution.Convolver();
            var filtered = convolver.Convolve(new DiscreteSignal(1, PaddingSig), gKernel);

            List<float> removePad = new List<float>();
            for (int i = 0; i < signalData.Count(); i++)
            {
                removePad.Add(filtered[i + ((kernelSize - 1))]);
            }

            return new DiscreteSignal(1, removePad);
        }

        static private double SampleStandardDeviation(float[] signalData, float x)
        {
            List<float> numberSet = new List<float>(signalData);

            double sdSum = signalData.Select(val => (val - x) * (val - x)).Sum();

            return Math.Sqrt(sdSum / (signalData.Length - 1));
        }
    }
}

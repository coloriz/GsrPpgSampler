using System;
using System.ComponentModel;
using System.Threading;
using System.IO.Ports;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;

namespace InsLab.Signal
{
    public enum SamplingRate { SR100Hz = 100, SR500Hz = 500, SR1000Hz = 1000 }

    [DesignerCategory("Code")]
    public class GsrPpgSampler : SerialPort
    {
        public SamplingRate GsrSamplingRate { protected set; get; }
        public SamplingRate PpgSamplingRate { protected set; get; }

        public event EventHandler<DataArrivedEventArgs> GsrDataArrived;
        public event EventHandler<DataArrivedEventArgs> PpgDataArrived;

        private Thread sampler = null;
        private bool reading = false;

        private Dictionary<string, List<GsrPpgPacket>> dataCollection;

        public GsrPpgSampler(string portName, int baudRate, SamplingRate gsr, SamplingRate ppg) : base(portName, baudRate)
        {
            GsrSamplingRate = gsr;
            PpgSamplingRate = ppg;

            base.Open();
            if (!base.IsOpen)
            {
                throw new Exception("Connection failed!");
            }

            byte[] srBytes1 = BitConverter.GetBytes((int)GsrSamplingRate);
            byte[] srBytes2 = BitConverter.GetBytes((int)PpgSamplingRate);

            byte[] dataToSend = new byte[9];
            dataToSend[0] = (byte)'!';
            Buffer.BlockCopy(srBytes1, 0, dataToSend, 1, srBytes1.Length);
            Buffer.BlockCopy(srBytes2, 0, dataToSend, srBytes1.Length + 1, srBytes2.Length);
            base.Write(dataToSend, 0, dataToSend.Length);
        }

        public string ConvertDataToJson()
        {
            var jsonString = JsonConvert.SerializeObject(dataCollection);
            return jsonString;
        }

        public void StartReading()
        {
            if (sampler != null && sampler.IsAlive)
            {
                return;
            }

            reading = true;
            sampler = new Thread(ReadData);
            sampler.Start();
        }

        public void StopReading()
        {
            reading = false;
            sampler?.Join();
        }

        private void ReadData()
        {
            dataCollection = new Dictionary<string, List<GsrPpgPacket>>()
            {
                ["GSR"] = new List<GsrPpgPacket>(),
                ["PPG"] = new List<GsrPpgPacket>()
            };

            int data_rx = 0;
            bool receivingData = false;
            bool wasPPG = false;
            int byteCount = 0;
            int data = 0;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (reading)
            {
                data_rx = base.ReadByte();

                // start bit detected. ready to receive data
                if (!receivingData && (data_rx & 0x1) == 0x0)
                {
                    byteCount = 0;
                    receivingData = true;
                }
                // receiving data
                else if (receivingData && (data_rx & 0x1) == 0x1)
                {
                    switch (byteCount)
                    {
                        case 0:
                            // check if this data is PPG
                            wasPPG = (data_rx & 0x02) != 0;
                            data = (data_rx & 0xfc) >> 2;
                            byteCount += 1;
                            break;
                        case 1:
                            // check if data is continuous. if not, it's an error
                            if (wasPPG != ((data_rx & 0x02) != 0))
                            {
                                byteCount = 0;
                                receivingData = false;
                                break;
                            }
                            data += (data_rx & 0xfc) << 4;
                            byteCount += 1;
                            break;
                        case 2:
                            // check if data is continuous. if not, it's an error
                            if (wasPPG != ((data_rx & 0x02) != 0))
                            {
                                byteCount = 0;
                                receivingData = false;
                                break;
                            }
                            data += (data_rx & 0xfc) << 10;
                            var packetReceivedTickCount = stopwatch.ElapsedTicks;
                            DataArrivedEventArgs args = new DataArrivedEventArgs { Timestamp = packetReceivedTickCount, Data = data };
                            if (wasPPG)
                            {
                                dataCollection["PPG"].Add(new GsrPpgPacket(data, packetReceivedTickCount));
                                OnPpgDataArrived(args);
                            }
                            else
                            {
                                dataCollection["GSR"].Add(new GsrPpgPacket(data, packetReceivedTickCount));
                                OnGsrDataArrived(args);
                            }
                            // clean up
                            byteCount = 0;
                            receivingData = false;
                            break;
                    }
                }
                // dirty data, ready for receive new data
                else
                {
                    byteCount = 0;
                    receivingData = false;
                }
            }
        }

        protected virtual void OnGsrDataArrived(DataArrivedEventArgs e)
        {
            GsrDataArrived?.Invoke(this, e);
        }

        protected virtual void OnPpgDataArrived(DataArrivedEventArgs e)
        {
            PpgDataArrived?.Invoke(this, e);
        }
    }

    public class DataArrivedEventArgs : EventArgs
    {
        public long Timestamp { get; set; }
        public int Data { get; set; }
    }
}

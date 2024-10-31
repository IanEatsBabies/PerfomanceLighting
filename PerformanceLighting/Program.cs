using System;
using System.Diagnostics;
using System.Collections.Generic;
using AuraServiceLib;
using System.Management;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel;

namespace AuraExample
{
    class Program
    {
        static IAuraSyncDevice _ledStrip = null;
        private static List<float> samples = new List<float>();
        private static BackgroundWorker worker = new BackgroundWorker();

        private static float _cpuUsage = 0f;
        private static float _gpuUsage = 0f;

        static void Main(string[] args)
        {
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.WorkerReportsProgress = false;
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerAsync();

            // Initialize Aura SDK
            AuraSdk auraSdk = new AuraSdk();
            auraSdk.SwitchMode();

            // Find the LED strip connected to ADD_HEADER
            
            foreach (IAuraSyncDevice device in auraSdk.Enumerate(0))
            {
                if (device.Name.Contains("AddressableStrip 1"))
                {
                    _ledStrip = device;
                    break;
                }
            }

            if (_ledStrip == null)
            {
                Console.WriteLine("No LED strip connected to ADD_HEADER found.");
                return;
            }

            while (true)
            {
                try
                {
                    var cpuUsage = GetCpuUsage();
                    if ((cpuUsage > -1f) && (cpuUsage < 101))
                        _cpuUsage = cpuUsage;
                }
                catch { Console.WriteLine("CPU Usage error"); }
                try 
                { 
                    var gpuCounters = GetGPUCounters();
                    //_gpuUsage = GetGPUUsage(gpuCounters);
                    var gpuUsage = GetGPUUsage(gpuCounters);
                    if((gpuUsage > -1f)&&(gpuUsage<200))
                        _gpuUsage = gpuUsage;
                }
                catch { Console.WriteLine("GPU Usage error"); }
                Console.WriteLine("CPU Usage: {0}% - GPU Usage: {1}%", ((float)Math.Round(_cpuUsage)), ((float)Math.Round(_gpuUsage)));
                SetLedColors(_cpuUsage, _gpuUsage);
                //System.Threading.Thread.Sleep(1000); // Pause for 1 second
            }


        }

        private static void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            float idleCpuUsage = 0f;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");

            ManagementObject obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (obj != null)
            {
                if (float.TryParse(obj["PercentIdleTime"]?.ToString(), out idleCpuUsage))
                {
                    lock (samples)
                    {
                        samples.Add(idleCpuUsage); // Store the sample in the list
                    }
                }
            }
        }

        private static void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Console.WriteLine("Error: " + e.Error.Message);
            }
            else
            {
                // Restart worker for continuous sampling
                if (!worker.CancellationPending)
                {
                    worker.RunWorkerAsync();
                }
            }
        }

        // Function to get CPU usage
        static float GetCpuUsage()
        {
            List<float> snapshot;
            lock (samples)
            {
                snapshot = new List<float>(samples);
                samples.Clear(); // Clear the list after taking the snapshot
            }

            float totalIdleCpuUsage = 0f;
            int sampleCount = snapshot.Count;

            // Calculate the total idle CPU usage from the samples
            foreach (var sample in snapshot)
            {
                totalIdleCpuUsage += sample;
            }

            float idleCpuUsage = totalIdleCpuUsage / sampleCount;
            //return (float)Math.Round(100 - idleCpuUsage); 
       
            return (100 - idleCpuUsage);
        }

        public static List<PerformanceCounter> GetGPUCounters()
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var counterNames = category.GetInstanceNames();

            var gpuCounters = counterNames
                                .Where(counterName => counterName.EndsWith("engtype_3D"))
                                .SelectMany(counterName => category.GetCounters(counterName))
                                .Where(counter => counter.CounterName.Equals("Utilization Percentage"))
                                .ToList();

            return gpuCounters;
        }

        public static float GetGPUUsage(List<PerformanceCounter> gpuCounters)
        {
            gpuCounters.ForEach(x => x.NextValue());

            Thread.Sleep(300);

            var result = gpuCounters.Sum(x => x.NextValue());

            return result;
        }

        // Function to set LED colors based on CPU and GPU usage
        static void SetLedColors(float cpuUsage, float gpuUsage)
        {
            int cpuLedsToLight = (int)(cpuUsage / 9);
            int gpuLedsToLight = (int)(gpuUsage / 9);

            for (int i = 0; i < _ledStrip.Lights.Count; i++)
            {
                if (i < 9)
                {
                    // Set CPU LEDs (first 9 LEDs)
                    if (i < cpuLedsToLight)
                    {
                        _ledStrip.Lights[i].Red = 5;
                        _ledStrip.Lights[i].Green = 0;
                        _ledStrip.Lights[i].Blue = 0;
                    }
                    else
                    {
                        _ledStrip.Lights[i].Red = 0;
                        _ledStrip.Lights[i].Green = 5;
                        _ledStrip.Lights[i].Blue = 0;
                    }
                }
                else if (i >= 9 && i < 18)
                {
                    // Set GPU LEDs (next 9 LEDs)
                    if (i - 9 < gpuLedsToLight)
                    {
                        _ledStrip.Lights[i].Red = 5;
                        _ledStrip.Lights[i].Green = 0;
                        _ledStrip.Lights[i].Blue = 0;
                    }
                    else
                    {
                        _ledStrip.Lights[i].Red = 0;
                        _ledStrip.Lights[i].Green = 5;
                        _ledStrip.Lights[i].Blue = 0;
                    }
                }
            }
            _ledStrip.Apply();
        }
    }
}

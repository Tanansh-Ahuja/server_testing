using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ADL_Automation
{
    public static class PriceSimulator
    {
        // --- NATIVE METHODS FOR PINNING ---
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
        // ----------------------------------

        static Process pythonProcess;
        public static bool neonFeedStarted = false;

        public static void Connect()
        {
            _ = Task.Run(() => Start());
        }

        static async Task Start()
        {
            // 1. PIN TO LAST CORE
            try
            {
                int coreCount = Environment.ProcessorCount;
                IntPtr affinityMask = new IntPtr(1 << (coreCount - 1));
                SetThreadAffinityMask(GetCurrentThread(), affinityMask);
            }
            catch { }

            string scriptPath = "neon_client.py";
            string scriptArgs = "--instruments EUR/USD";

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-u {scriptPath} {scriptArgs}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            try
            {
                using (pythonProcess = Process.Start(start))
                {
                    _ = ReadErrorStream(pythonProcess.StandardError);

                    await ReadFastLoop(pythonProcess.StandardOutput.BaseStream);

                    if (!pythonProcess.HasExited)
                    {
                        pythonProcess.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceSimulator] Start Failed: {ex.Message}");
            }
        }

        static async Task ReadErrorStream(StreamReader reader)
        {
            try
            {
                char[] buffer = new char[256];
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string err = new string(buffer, 0, read);
                    // TODO: Log this error
                }
            }
            catch { }
        }

        static async Task ReadFastLoop(Stream stream)
        {
            byte[] ioBuffer = new byte[4096];
            char[] lineBuffer = new char[2048];
            int linePos = 0;
            int bytesRead;

            try
            {
                while ((bytesRead = await stream.ReadAsync(ioBuffer, 0, ioBuffer.Length)) > 0)
                {
                    double latestBatchPrice = double.NaN;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        char c = (char)ioBuffer[i];

                        if (c == '\n')
                        {
                            double priceFound = ParsePriceOnly(lineBuffer, linePos);
                            if (!double.IsNaN(priceFound))
                            {
                                latestBatchPrice = priceFound;
                            }
                            linePos = 0;
                        }
                        else if (c != '\r')
                        {
                            if (linePos < lineBuffer.Length) lineBuffer[linePos++] = c;
                        }
                    }

                    if (!double.IsNaN(latestBatchPrice))
                    {
                        FireHotPath(latestBatchPrice);
                        
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceSimulator] Loop Error: {ex.Message}");
            }
        }

        private static double ParsePriceOnly(char[] buffer, int length)
        {
            ReadOnlySpan<char> lineSpan = new ReadOnlySpan<char>(buffer, 0, length);
            ReadOnlySpan<char> searchKey = "midprice\":".AsSpan();

            if (lineSpan.Length > 20 && lineSpan[0] == '{')
            {
                int keyIndex = lineSpan.IndexOf(searchKey);
                if (keyIndex > -1)
                {
                    int valueStart = keyIndex + searchKey.Length;
                    ReadOnlySpan<char> remaining = lineSpan.Slice(valueStart);

                    int commaIndex = remaining.IndexOf(',');
                    int braceIndex = remaining.IndexOf('}');

                    int valueEnd = -1;
                    if (commaIndex > -1) valueEnd = commaIndex;
                    else if (braceIndex > -1) valueEnd = braceIndex;

                    if (valueEnd > 0)
                    {
                        ReadOnlySpan<char> numberSpan = remaining.Slice(0, valueEnd);
                        if (double.TryParse(numberSpan.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double price))
                        {
                            return price;
                        }
                    }
                }
            }
            return double.NaN;
        }

        private static void FireHotPath(double price)
        {
            if (MainForm._neonFeedConnected)
            {
                MainForm.OnFastPriceUpdate(price);
            }
            else
            {
                MainForm._liveNeonValue = price;
                MainForm.NeonFeedConnected();
            }
        }

        public static void Stop()
        {
            try
            {
                if (pythonProcess != null && !pythonProcess.HasExited)
                {
                    pythonProcess.Kill();
                }
                neonFeedStarted = false;
            }
            catch { }
        }
    }
}
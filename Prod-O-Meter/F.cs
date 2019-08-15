using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using EasyConsole;
using Console = Colorful.Console;

namespace Prod_O_Meter
{
    internal static class F
    {
        private static Color PromptColor => Color.DodgerBlue;

        private static string ProcessBuffer { get; set; }

        public static IEnumerable<Tuple<string, Action>> CreateMenu(this StringCollection collection, Action<int> callback)
        {
            int index = 0;
            foreach (var item in collection)
            {
                var index1 = index;
                yield return new Tuple<string, Action>(item, () => callback(index1));

                ++index;
            }
        }

        public static Menu AddOptions(this Menu menu, params Tuple<string, Action>[] options)
        {
            foreach (var option in options)
            {
                menu.Add(option.Item1, option.Item2);
            }

            return menu;
        }

        private static void GetExecutingString(ProcessStartInfo info)
        {
            Console.WriteLineFormatted("Executing: '{0}' at '{1}'", PromptColor, Color.White, $"{info.FileName} {info.Arguments}", info.WorkingDirectory);
        }

        public static string CreateProcess(string fileName, string arguments, string workingDir)
        {
            return CreateProcess(fileName, arguments, workingDir, null);
        }

        private static string CreateProcess(string fileName, string arguments, string workingDir,
            Func<bool> continueFunc)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            //if (string.IsNullOrEmpty(arguments))
            //    throw new ArgumentNullException(nameof(arguments));

            if (string.IsNullOrEmpty(workingDir))
                throw new ArgumentNullException(nameof(workingDir));

            ProcessBuffer = string.Empty;

            using (var process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo(fileName, arguments)
                        {
                            WorkingDirectory = workingDir,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                })
            {
                if (continueFunc?.Invoke() == true)
                    return string.Empty;

                GetExecutingString(process.StartInfo);

                process.OutputDataReceived += ProcessDataReceived;
                process.ErrorDataReceived += ProcessDataReceived;
                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                return ProcessBuffer;
            }
        }

        private static void ProcessDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                ProcessBuffer += Environment.NewLine;
                return;
            }

            ProcessBuffer += e.Data + Environment.NewLine;
        }

        public static void AnyKeyToExit()
        {
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
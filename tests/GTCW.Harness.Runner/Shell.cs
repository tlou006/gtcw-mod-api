using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GTCW.Harness.Runner
{
    /// <summary>Runs child processes with a time limit, keeping their output.</summary>
    internal static class Shell
    {
        public sealed class Result
        {
            public int ExitCode { get; init; }

            public bool TimedOut { get; init; }

            public string Output { get; init; } = string.Empty;
        }

        public static Result Run(
            string file,
            IEnumerable<string> arguments,
            string? workingDirectory = null,
            TimeSpan? timeout = null,
            IDictionary<string, string>? environment = null,
            string? logFile = null)
        {
            var info = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            };

            foreach (string a in arguments)
            {
                info.ArgumentList.Add(a);
            }

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> pair in environment)
                {
                    info.Environment[pair.Key] = pair.Value;
                }
            }

            var output = new StringBuilder();
            using var process = new Process { StartInfo = info };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            bool finished = process.WaitForExit((int)(timeout ?? TimeSpan.FromMinutes(30)).TotalMilliseconds);
            if (!finished)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                process.WaitForExit(10000);
            }
            else
            {
                process.WaitForExit();
            }

            string text;
            lock (output)
            {
                text = output.ToString();
            }

            if (logFile != null)
            {
                File.WriteAllText(logFile, text);
            }

            return new Result { ExitCode = finished ? process.ExitCode : -1, TimedOut = !finished, Output = text };
        }

        /// <summary>
        /// Mirrors one folder into another with robocopy. Only ever called with a
        /// destination inside HarnessDir, which Settings has already proven is
        /// outside the repo and the install.
        /// </summary>
        public static void Mirror(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            Result r = Run("robocopy", new[] { source, destination, "/MIR", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/R:1", "/W:1" },
                           timeout: TimeSpan.FromMinutes(10));
            if (r.TimedOut || r.ExitCode >= 8)
            {
                throw new HarnessException($"robocopy {source} -> {destination} failed (exit {r.ExitCode}): {r.Output.Trim()}");
            }
        }
    }
}

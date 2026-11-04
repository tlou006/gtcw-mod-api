using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace GTCW.Harness.Runner
{
    /// <summary>One test result, as a host wrote it.</summary>
    internal sealed class TestRecord
    {
        public string Tier { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Outcome { get; init; } = string.Empty;

        public long Milliseconds { get; init; }

        public string? Message { get; init; }

        public string[] Fixes { get; init; } = Array.Empty<string>();

        public string[] Notes { get; init; } = Array.Empty<string>();

        public string[] Log { get; init; } = Array.Empty<string>();

        public bool Passed => Outcome == "passed";
    }

    /// <summary>
    /// Everything every host reported in this run: printed as a summary and
    /// written as JUnit XML for anything that reads it.
    /// </summary>
    internal sealed class RunReport
    {
        private readonly List<TestRecord> _tests = new List<TestRecord>();
        private readonly List<TestRecord> _errors = new List<TestRecord>();

        public int Failed => _tests.Count(t => !t.Passed) + _errors.Count;

        public int Passed => _tests.Count(t => t.Passed);

        /// <summary>
        /// Reads a host's results file. Returns whether it reached its closing
        /// "done" record; a host that crashed or timed out never writes one.
        /// </summary>
        public bool Read(string path, string unit)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            bool done = false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                using JsonDocument doc = JsonDocument.Parse(line);
                JsonElement e = doc.RootElement;
                string kind = e.GetProperty("kind").GetString() ?? string.Empty;
                string tier = e.TryGetProperty("tier", out JsonElement t) ? t.GetString() ?? "" : "";
                switch (kind)
                {
                    case "test":
                        _tests.Add(new TestRecord
                        {
                            Tier = tier,
                            Name = e.GetProperty("name").GetString() ?? "",
                            Outcome = e.GetProperty("outcome").GetString() ?? "",
                            Milliseconds = e.GetProperty("ms").GetInt64(),
                            Message = e.TryGetProperty("message", out JsonElement m) ? m.GetString() : null,
                            Fixes = Strings(e, "fixes"),
                            Notes = Strings(e, "notes"),
                            Log = Strings(e, "log"),
                        });
                        break;
                    case "error":
                        AddError(tier, $"{unit}: {e.GetProperty("name").GetString()}", e.GetProperty("message").GetString() ?? "");
                        break;
                    case "info":
                        Console.WriteLine($"  [{tier}] {e.GetProperty("message").GetString()}");
                        break;
                    case "done":
                        done = true;
                        break;
                }
            }

            return done;
        }

        public void AddTest(TestRecord record) => _tests.Add(record);

        public void AddError(string tier, string name, string message) =>
            _errors.Add(new TestRecord { Tier = tier, Name = name, Outcome = "error", Message = message });

        public void Print()
        {
            Console.WriteLine();
            foreach (TestRecord test in _tests)
            {
                string fixes = test.Fixes.Length == 0 ? string.Empty : "  [" + string.Join(", ", test.Fixes) + "]";
                Console.WriteLine($"  {(test.Passed ? "ok  " : "FAIL")}  {test.Tier,-8} {test.Name}{fixes}  ({test.Milliseconds} ms)");
                if (!test.Passed)
                {
                    Console.WriteLine("        " + (test.Message ?? "(no message)"));
                    foreach (string line in test.Log.Take(12))
                    {
                        Console.WriteLine("        log: " + line);
                    }
                }

                foreach (string note in test.Notes)
                {
                    Console.WriteLine("        " + note);
                }
            }

            foreach (TestRecord error in _errors)
            {
                Console.WriteLine($"  ERROR {error.Tier,-8} {error.Name}");
                Console.WriteLine("        " + error.Message);
            }

            Console.WriteLine();
            foreach (IGrouping<string, TestRecord> tier in _tests.Concat(_errors).GroupBy(t => t.Tier))
            {
                int ok = tier.Count(t => t.Passed);
                Console.WriteLine($"{tier.Key}: {ok} passed, {tier.Count() - ok} failed");
            }

            Console.WriteLine(Failed == 0 ? "ALL PASSED" : $"{Failed} FAILED");
        }

        public void WriteJUnit(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var suites = new XElement("testsuites");
            foreach (IGrouping<string, TestRecord> tier in _tests.Concat(_errors).GroupBy(t => t.Tier))
            {
                var suite = new XElement("testsuite",
                    new XAttribute("name", "GTCW.BugFixes." + tier.Key),
                    new XAttribute("tests", tier.Count()),
                    new XAttribute("failures", tier.Count(t => t.Outcome == "failed")),
                    new XAttribute("errors", tier.Count(t => t.Outcome == "error")));
                foreach (TestRecord test in tier)
                {
                    var testcase = new XElement("testcase",
                        new XAttribute("classname", "GTCW.BugFixes." + tier.Key),
                        new XAttribute("name", test.Name),
                        new XAttribute("time", (test.Milliseconds / 1000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)));
                    if (!test.Passed)
                    {
                        testcase.Add(new XElement(test.Outcome == "error" ? "error" : "failure",
                            new XAttribute("message", test.Message ?? string.Empty),
                            string.Join(Environment.NewLine, test.Log)));
                    }

                    if (test.Notes.Length > 0)
                    {
                        testcase.Add(new XElement("system-out", string.Join(Environment.NewLine, test.Notes)));
                    }

                    suite.Add(testcase);
                }

                suites.Add(suite);
            }

            new XDocument(suites).Save(path);
        }

        private static string[] Strings(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement array)
                ? array.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
    }
}

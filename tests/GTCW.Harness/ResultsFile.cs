using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GTCW.Harness
{
    /// <summary>
    /// One JSON object per line, flushed as it is written, so a host that dies
    /// part way still leaves every finished result behind. The runner reads it;
    /// the closing <c>done</c> record is how it tells a finished run from a
    /// crashed one, since the game's own exit code is meaningless.
    /// </summary>
    internal sealed class ResultsFile : IDisposable
    {
        private readonly StreamWriter _writer;

        public ResultsFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            _writer = new StreamWriter(path, append: true, new UTF8Encoding(false)) { AutoFlush = true };
        }

        public void Test(
            Tier tier,
            string name,
            IEnumerable<string> fixes,
            string outcome,
            long milliseconds,
            string? message,
            IEnumerable<string> notes,
            IEnumerable<string> log)
        {
            var sb = new StringBuilder("{\"kind\":\"test\"");
            Field(sb, "tier", tier.ToString().ToLowerInvariant());
            Field(sb, "name", name);
            Array(sb, "fixes", fixes);
            Field(sb, "outcome", outcome);
            sb.Append(",\"ms\":").Append(milliseconds.ToString(CultureInfo.InvariantCulture));
            if (message != null)
            {
                Field(sb, "message", message);
            }

            Array(sb, "notes", notes);
            Array(sb, "log", log);
            _writer.WriteLine(sb.Append('}').ToString());
        }

        public void Error(Tier tier, string context, string message)
        {
            var sb = new StringBuilder("{\"kind\":\"error\"");
            Field(sb, "tier", tier.ToString().ToLowerInvariant());
            Field(sb, "name", context);
            Field(sb, "message", message);
            _writer.WriteLine(sb.Append('}').ToString());
        }

        public void Info(Tier tier, string message)
        {
            var sb = new StringBuilder("{\"kind\":\"info\"");
            Field(sb, "tier", tier.ToString().ToLowerInvariant());
            Field(sb, "message", message);
            _writer.WriteLine(sb.Append('}').ToString());
        }

        public void Done(Tier tier, int passed, int failed)
        {
            var sb = new StringBuilder("{\"kind\":\"done\"");
            Field(sb, "tier", tier.ToString().ToLowerInvariant());
            sb.Append(",\"passed\":").Append(passed).Append(",\"failed\":").Append(failed);
            _writer.WriteLine(sb.Append('}').ToString());
        }

        public void Dispose() => _writer.Dispose();

        private static void Field(StringBuilder sb, string name, string value) =>
            sb.Append(",\"").Append(name).Append("\":").Append(Quote(value));

        private static void Array(StringBuilder sb, string name, IEnumerable<string> values)
        {
            sb.Append(",\"").Append(name).Append("\":[");
            bool first = true;
            foreach (string v in values)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(Quote(v));
                first = false;
            }

            sb.Append(']');
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}

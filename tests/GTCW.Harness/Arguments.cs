using System;

namespace GTCW.Harness
{
    /// <summary>The handful of options both hosts take, by name.</summary>
    internal sealed class Arguments
    {
        private readonly string[] _args;

        public Arguments(string[] args) => _args = args;

        public string? this[string name]
        {
            get
            {
                for (int i = 0; i < _args.Length - 1; i++)
                {
                    if (string.Equals(_args[i], name, StringComparison.Ordinal))
                    {
                        return _args[i + 1];
                    }
                }

                return null;
            }
        }

        public string Require(string name) =>
            this[name] ?? throw new ArgumentException($"missing {name}");
    }
}

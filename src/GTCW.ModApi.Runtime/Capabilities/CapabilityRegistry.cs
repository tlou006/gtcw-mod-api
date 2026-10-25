using System;
using System.Collections.Generic;
using System.Linq;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Capabilities
{
    /// <summary>
    /// Probes every feature, installs the ones this build supports, and answers
    /// <see cref="ICapabilities"/> for mods.
    /// </summary>
    /// <remarks>
    /// This is the mechanism that replaces a hard version gate. A hash gate
    /// alone would disable every mod in the ecosystem on every Steam patch; the
    /// hash instead identifies the build, and the probes decide what still
    /// works. Never hard-crash someone's game over a version bump.
    /// </remarks>
    internal sealed class CapabilityRegistry : ICapabilities
    {
        private readonly IModApiLog _log;
        private readonly List<Feature> _features = new List<Feature>();
        private readonly Dictionary<Capability, string> _unavailable =
            new Dictionary<Capability, string>();
        private readonly HashSet<Capability> _available = new HashSet<Capability>();

        public CapabilityRegistry(IModApiLog log) => _log = log;

        public void Add(Feature feature) => _features.Add(feature);

        public bool Has(Capability capability) => _available.Contains(capability);

        public string Explain(Capability capability) =>
            _unavailable.TryGetValue(capability, out string reason)
                ? reason
                : string.Empty;

        /// <summary>
        /// Probe and install everything. Each feature is isolated: a probe that
        /// throws counts as a failed probe, and a patch that throws disables
        /// only its own feature.
        /// </summary>
        public void ApplyAll()
        {
            foreach (Feature feature in _features)
            {
                ProbeResult result;
                try
                {
                    result = feature.Probe();
                }
                catch (Exception ex)
                {
                    result = ProbeResult.Fail($"probe threw: {ex.Message}");
                }

                if (!result.Ok)
                {
                    _unavailable[feature.Id] = result.Reason;
                    continue;
                }

                try
                {
                    feature.Apply();
                    _available.Add(feature.Id);
                }
                catch (Exception ex)
                {
                    _unavailable[feature.Id] = $"patching failed: {ex.Message}";
                    _log.Error($"[{feature.Id}] disabled — {ex}");
                }
            }
        }

        /// <summary>
        /// The capability report. Printed once at startup so a bug report
        /// arrives with the answer already in it.
        /// </summary>
        public void Report()
        {
            string on = _available.Count == 0
                ? "(none)"
                : string.Join(", ", _available.OrderBy(c => c.ToString()));

            _log.Info($"Capabilities on: {on}");

            foreach (KeyValuePair<Capability, string> pair in
                     _unavailable.OrderBy(p => p.Key.ToString()))
            {
                _log.Warning($"Capability off: {pair.Key} — {pair.Value}");
            }
        }
    }
}

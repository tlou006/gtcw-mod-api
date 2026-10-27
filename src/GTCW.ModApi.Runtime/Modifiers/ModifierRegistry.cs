using System;
using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Modifiers
{
    /// <summary>One registered support contribution.</summary>
    internal sealed class SupportModifier
    {
        public SupportModifier(
            string key,
            Side side,
            int stateIndex,
            Func<ISupportContext, float> ratePerYear)
        {
            Key = key;
            Side = side;
            StateIndex = stateIndex;
            RatePerYear = ratePerYear;
        }

        public string Key { get; }

        public Side Side { get; }

        /// <summary>A single state, or -1 for every state.</summary>
        public int StateIndex { get; }

        public Func<ISupportContext, float> RatePerYear { get; }

        public bool AppliesToAllStates => StateIndex < 0;
    }

    /// <summary>One registered alliance-scoped contribution.</summary>
    internal sealed class AllianceRateModifier
    {
        public AllianceRateModifier(string key, Side side, Func<IAllianceContext, float> ratePerYear)
        {
            Key = key;
            Side = side;
            RatePerYear = ratePerYear;
        }

        public string Key { get; }
        public Side Side { get; }
        public Func<IAllianceContext, float> RatePerYear { get; }
    }

    /// <summary>
    /// A set of alliance-scoped contributions. One instance per quantity, so
    /// national morale and treasury share the mechanism without sharing a list.
    /// </summary>
    internal sealed class AllianceRateSet : IAllianceRateModifiers
    {
        private readonly IModApiLog _log;
        private readonly string _label;
        private readonly List<AllianceRateModifier> _modifiers = new List<AllianceRateModifier>();

        public AllianceRateSet(IModApiLog log, string label)
        {
            _log = log;
            _label = label;
        }

        public IReadOnlyCollection<string> Keys
        {
            get
            {
                var keys = new List<string>(_modifiers.Count);
                foreach (AllianceRateModifier m in _modifiers)
                {
                    keys.Add(m.Key);
                }

                return keys;
            }
        }

        internal IReadOnlyList<AllianceRateModifier> Modifiers => _modifiers;

        internal bool HasAny => _modifiers.Count > 0;

        public void Add(string key, Side side, Func<IAllianceContext, float> ratePerYear)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A contribution key is required.", nameof(key));
            }

            if (ratePerYear == null)
            {
                throw new ArgumentNullException(nameof(ratePerYear));
            }

            if (side == Side.None)
            {
                throw new ArgumentException(
                    $"Contribution '{key}' names no side, so it would never apply.", nameof(side));
            }

            var modifier = new AllianceRateModifier(key, side, ratePerYear);

            int existing = IndexOf(key);
            if (existing >= 0)
            {
                _modifiers[existing] = modifier;
                _log.Info($"{_label} contribution '{key}' replaced.");
                return;
            }

            _modifiers.Add(modifier);
        }

        public bool Remove(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                return false;
            }

            _modifiers.RemoveAt(index);
            return true;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (string.Equals(_modifiers[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// Holds the registered contributions. Deliberately dumb: the folding and
    /// all the game knowledge live in the patches.
    /// </summary>
    internal sealed class ModifierRegistry : IModifierRegistry, ISupportModifiers
    {
        private readonly IModApiLog _log;

        // Insertion-ordered so the fold order is stable and explainable. A mod
        // re-adding under the same key replaces in place rather than jumping to
        // the end.
        private readonly List<SupportModifier> _support = new List<SupportModifier>();

        private readonly AllianceRateSet _nationalMorale;
        private readonly AllianceRateSet _treasury;

        private readonly ValueModifierSet _adminCost;
        private readonly ValueModifierSet _subsidyCost;
        private readonly ValueModifierSet _subsidyFactor;
        private readonly ValueModifierSet _taxRevenue;
        private readonly ValueModifierSet _financialBalance;

        public ModifierRegistry(IModApiLog log)
        {
            _log = log;
            _nationalMorale = new AllianceRateSet(log, "National morale");
            _treasury = new AllianceRateSet(log, "Treasury");

            _adminCost = new ValueModifierSet(log, "admin cost");
            _subsidyCost = new ValueModifierSet(log, "subsidy cost");
            _subsidyFactor = new ValueModifierSet(log, "subsidy factor");
            _taxRevenue = new ValueModifierSet(log, "tax revenue");
            _financialBalance = new ValueModifierSet(log, "financial balance");
        }

        public ISupportModifiers Support => this;

        public IAllianceRateModifiers NationalMorale => _nationalMorale;

        public IAllianceRateModifiers Treasury => _treasury;

        public IValueModifiers AdminCost => _adminCost;

        public IValueModifiers SubsidyCost => _subsidyCost;

        public IValueModifiers SubsidyFactor => _subsidyFactor;

        public IValueModifiers TaxRevenue => _taxRevenue;

        public IValueModifiers FinancialBalance => _financialBalance;

        internal AllianceRateSet NationalMoraleSet => _nationalMorale;

        internal AllianceRateSet TreasurySet => _treasury;

        internal ValueModifierSet AdminCostSet => _adminCost;

        internal ValueModifierSet SubsidyCostSet => _subsidyCost;

        internal ValueModifierSet SubsidyFactorSet => _subsidyFactor;

        internal ValueModifierSet TaxRevenueSet => _taxRevenue;

        internal ValueModifierSet FinancialBalanceSet => _financialBalance;

        /// <summary>True when anything at all is registered, for a cheap early out.</summary>
        internal bool HasAnything =>
            HasSupportModifiers || _nationalMorale.HasAny || _treasury.HasAny;

        public IReadOnlyCollection<string> Keys
        {
            get
            {
                var keys = new List<string>(_support.Count);
                foreach (SupportModifier m in _support)
                {
                    keys.Add(m.Key);
                }

                return keys;
            }
        }

        public void Add(string key, Side side, Func<ISupportContext, float> ratePerYear) =>
            AddInternal(key, -1, side, ratePerYear);

        public void AddForState(string key, int stateIndex, Side side, Func<ISupportContext, float> ratePerYear)
        {
            if (stateIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stateIndex),
                    "A state index must be zero or greater. Use Add to contribute to every state.");
            }

            AddInternal(key, stateIndex, side, ratePerYear);
        }

        private void AddInternal(string key, int stateIndex, Side side, Func<ISupportContext, float> ratePerYear)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A contribution key is required.", nameof(key));
            }

            if (ratePerYear == null)
            {
                throw new ArgumentNullException(nameof(ratePerYear));
            }

            if (side == Side.None)
            {
                throw new ArgumentException(
                    $"Contribution '{key}' names no side, so it would never apply.", nameof(side));
            }

            var modifier = new SupportModifier(key, side, stateIndex, ratePerYear);

            int existing = IndexOf(key);
            if (existing >= 0)
            {
                _support[existing] = modifier;
                _log.Info($"Support contribution '{key}' replaced.");
                return;
            }

            _support.Add(modifier);
        }

        public bool Remove(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                return false;
            }

            _support.RemoveAt(index);
            return true;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < _support.Count; i++)
            {
                if (string.Equals(_support[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The contributions, in fold order.</summary>
        internal IReadOnlyList<SupportModifier> SupportModifiers => _support;

        internal bool HasSupportModifiers => _support.Count > 0;
    }

    /// <summary>Context handed to a support contribution.</summary>
    internal sealed class SupportContext : ISupportContext
    {
        public Side Side { get; internal set; }
        public int Alliance { get; internal set; }
        public int StateIndex { get; internal set; }
        public float CurrentSupport { get; internal set; }
        public float DeltaYears { get; internal set; }
    }

    /// <summary>Context handed to an alliance-scoped contribution.</summary>
    internal sealed class AllianceContext : IAllianceContext
    {
        public Side Side { get; internal set; }
        public int Alliance { get; internal set; }
        public float CurrentValue { get; internal set; }
        public float DeltaYears { get; internal set; }
    }
}

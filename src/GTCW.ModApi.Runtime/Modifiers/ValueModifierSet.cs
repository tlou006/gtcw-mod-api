using System;
using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Modifiers
{
    /// <summary>Whether a value modifier scales or shifts.</summary>
    internal enum ValueModifierKind
    {
        Multiply,
        Offset,
    }

    /// <summary>One registered value modifier.</summary>
    internal sealed class ValueModifier
    {
        public ValueModifier(
            string key,
            Side side,
            ValueModifierKind kind,
            Func<IValueContext, float> value)
        {
            Key = key;
            Side = side;
            Kind = kind;
            Value = value;
        }

        public string Key { get; }
        public Side Side { get; }
        public ValueModifierKind Kind { get; }
        public Func<IValueContext, float> Value { get; }
    }

    /// <summary>
    /// Modifiers for one computed value. One instance per game function.
    /// </summary>
    /// <remarks>
    /// A key owns at most one modifier, of either kind, so registering a
    /// multiplier over an existing offset under the same key replaces it. That
    /// keeps <c>Remove(key)</c> unambiguous, which matters because a mod
    /// unregistering its effect should not have to remember which kind it used.
    /// </remarks>
    internal sealed class ValueModifierSet : IValueModifiers
    {
        private readonly IModApiLog _log;
        private readonly string _label;
        private readonly List<ValueModifier> _modifiers = new List<ValueModifier>();

        public ValueModifierSet(IModApiLog log, string label)
        {
            _log = log;
            _label = label;
        }

        /// <summary>Hot path: checked before any other work on every call.</summary>
        internal bool HasAny => _modifiers.Count > 0;

        internal IReadOnlyList<ValueModifier> Modifiers => _modifiers;

        internal string Label => _label;

        public IReadOnlyCollection<string> Keys
        {
            get
            {
                var keys = new List<string>(_modifiers.Count);
                foreach (ValueModifier m in _modifiers)
                {
                    keys.Add(m.Key);
                }

                return keys;
            }
        }

        public void Multiply(string key, Side side, Func<IValueContext, float> factor) =>
            AddInternal(key, side, ValueModifierKind.Multiply, factor);

        public void Offset(string key, Side side, Func<IValueContext, float> amount) =>
            AddInternal(key, side, ValueModifierKind.Offset, amount);

        private void AddInternal(
            string key,
            Side side,
            ValueModifierKind kind,
            Func<IValueContext, float> value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A modifier key is required.", nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (side == Side.None)
            {
                throw new ArgumentException(
                    $"Modifier '{key}' names no side, so it would never apply.", nameof(side));
            }

            var modifier = new ValueModifier(key, side, kind, value);

            int existing = IndexOf(key);
            if (existing >= 0)
            {
                _modifiers[existing] = modifier;
                _log.Info($"{_label} modifier '{key}' replaced.");
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

    /// <summary>Context handed to a value modifier.</summary>
    internal sealed class ValueContext : IValueContext
    {
        public Side Side { get; internal set; }
        public int Alliance { get; internal set; }
        public float BaseValue { get; internal set; }
        public int Index { get; internal set; }
    }
}

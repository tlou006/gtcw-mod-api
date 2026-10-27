using System;
using System.Collections.Generic;

namespace GTCW.ModApi
{
    /// <summary>
    /// The write path. Mods declare contributions and the library folds them
    /// into the game's own passes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not a set of setters.</b> Most campaign state cannot be
    /// written directly and made to stick. Two of the game's three state idioms
    /// defeat it outright: a double-buffered sweep walks one entity per frame
    /// into a shadow field and overwrites the published one on wrap, so a write
    /// is simply undone; and a reset-then-accumulate pass zeroes around
    /// eighteen fields and refills them from the unit list, so a write survives
    /// at most twelve game hours. A mod that assigns a field sees its change
    /// vanish with no error and no clue.
    /// </para>
    /// <para>
    /// A contribution avoids all of that by being re-applied, by the library, at
    /// the point in the game's own pass where the value is next written. It is
    /// expressed as a <i>rate per game year</i> and scaled by the elapsed time
    /// the pass itself measured, so it does not depend on frame rate, game
    /// speed, or how often the pass happens to run.
    /// </para>
    /// </remarks>
    public interface IModifierRegistry
    {
        /// <summary>Contributions to state support.</summary>
        ISupportModifiers Support { get; }
    }

    /// <summary>
    /// Contributions to a state's support for one side.
    /// </summary>
    /// <remarks>
    /// Support is the campaign's manpower tap — volunteers scale with it inside
    /// a power law, drafts are gated on it, and contract renewal reads it
    /// directly — so a contribution here is one of the most far-reaching things
    /// a mod can do. It is folded through the game's own
    /// <c>Nation.AdjustSupport</c>, which clamps to 0–1 and recomputes the
    /// actual delta, so a contribution can never push support out of range.
    /// </remarks>
    public interface ISupportModifiers
    {
        /// <summary>
        /// Contribute to one side's support in <b>every</b> state.
        /// </summary>
        /// <param name="key">
        /// Your stable key, conventionally <c>"yourmod.something"</c>. Adding
        /// twice under one key replaces the first.
        /// </param>
        /// <param name="side">Whose support to move. <see cref="Side.Both"/> moves each side's own.</param>
        /// <param name="ratePerYear">
        /// Support per game year, as a fraction of full support — so
        /// <c>0.01f</c> is one percentage point a year. May be negative.
        /// Called once per state per fold, so keep it cheap and free of side
        /// effects.
        /// </param>
        void Add(string key, Side side, Func<ISupportContext, float> ratePerYear);

        /// <summary>
        /// Contribute to one side's support in a single state.
        /// </summary>
        /// <param name="key">As <see cref="Add"/>.</param>
        /// <param name="side">As <see cref="Add"/>.</param>
        /// <param name="ratePerYear">As <see cref="Add"/>.</param>
        /// <param name="stateIndex">
        /// The game's own state index. Deliberately not a name: the game
        /// rewrites <c>nation[].name</c> at runtime when the active state set
        /// changes, so anything matching a state by string silently stops
        /// matching. The state count also varies by scenario (52, 53 or 54), so
        /// never hardcode a count.
        /// </param>
        void AddForState(string key, int stateIndex, Side side, Func<ISupportContext, float> ratePerYear);

        /// <summary>Removes a contribution. Returns false if the key was not registered.</summary>
        bool Remove(string key);

        /// <summary>Registered keys, for diagnostics.</summary>
        IReadOnlyCollection<string> Keys { get; }
    }

    /// <summary>What a support contribution is being asked about.</summary>
    public interface ISupportContext
    {
        /// <summary>Whose support this invocation concerns.</summary>
        Side Side { get; }

        /// <summary>Raw alliance index, for callers that want the game's numbering.</summary>
        int Alliance { get; }

        /// <summary>The state being adjusted.</summary>
        int StateIndex { get; }

        /// <summary>That state's current support for <see cref="Side"/>, 0 to 1.</summary>
        float CurrentSupport { get; }

        /// <summary>
        /// Game years since this contribution was last folded. Already applied
        /// to your returned rate — it is here for contributions that need to
        /// reason about the interval themselves.
        /// </summary>
        float DeltaYears { get; }
    }
}

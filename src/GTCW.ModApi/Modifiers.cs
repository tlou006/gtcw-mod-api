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

        /// <summary>
        /// Contributions to national morale — <b>the game's only loss
        /// condition</b>.
        /// </summary>
        /// <remarks>
        /// A side that reaches zero national morale loses the war; battles
        /// move it by rounding errors next to towns and state support. So a
        /// contribution here is the most consequential thing in this API, and a
        /// careless sign or magnitude can decide a campaign. Folded through
        /// <c>AdjustNationalMorale</c>, which clamps to 0–1 and recomputes the
        /// delta, and credited to the morale breakdown.
        /// </remarks>
        IAllianceRateModifiers NationalMorale { get; }

        /// <summary>
        /// Contributions to an alliance's treasury, in currency per game year.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Folded through <c>Alliance.AdjustTreasury</c>, the only writer the
        /// game intends, which means two of its behaviours come along:
        /// </para>
        /// <para>
        /// <b>There is no clamp.</b> Treasury is allowed to go arbitrarily
        /// negative — that is how debt works — so a negative contribution has
        /// no floor and will not stop itself.
        /// </para>
        /// <para>
        /// <b>The AI gets a discount.</b> A negative adjustment for any
        /// alliance other than the player's is reduced by the campaign
        /// difficulty bonus, up to about 15 % at maximum. So a contribution
        /// that drains an opponent drains less than it asks, by a margin the
        /// player's difficulty setting decides. This is the game's own
        /// handicap, inherited deliberately rather than worked around, since
        /// bypassing <c>AdjustTreasury</c> would mean writing the field
        /// directly.
        /// </para>
        /// <para>
        /// <b>Above about 50 million, income becomes debt repayment rather than
        /// cash.</b> The Treasury runs automatically twice a game day: below
        /// zero it issues 25 million of bonds, and at or above 50 million it
        /// buys 25 million back while any debt remains. That check runs
        /// <i>earlier in the same pass</i> this fold happens in, so a positive
        /// contribution large enough to cross the trigger is clawed back on the
        /// following beat. The cash balance therefore oscillates in a band
        /// rather than climbing, and a contribution's real effect while a side
        /// is in debt is to retire that debt faster. It only accumulates as
        /// cash once the debt reaches zero.
        /// </para>
        /// </remarks>
        IAllianceRateModifiers Treasury { get; }

        /// <summary>
        /// Administrative cost per year. A <b>negative</b> quantity: the game
        /// returns costs as negatives so its balance can simply sum them, and
        /// this one is already clamped so it can never come back positive. A
        /// multiplier below 1 therefore makes government <i>cheaper</i>.
        /// </summary>
        IValueModifiers AdminCost { get; }

        /// <summary>
        /// Subsidy cost per year, also <b>negative</b>. The context's
        /// <see cref="IValueContext.Index"/> is the subsidy category, or -1 when
        /// the game is asking for the total across all of them.
        /// </summary>
        IValueModifiers SubsidyCost { get; }

        /// <summary>
        /// A subsidy category's effective factor — the slider times its
        /// policy-unlocked ceiling. <see cref="IValueContext.Index"/> is the
        /// category.
        /// </summary>
        IValueModifiers SubsidyFactor { get; }

        /// <summary>
        /// Tax revenue per year, a <b>positive</b> quantity.
        /// <see cref="IValueContext.Index"/> is the tax type, or -1 for the
        /// total.
        /// </summary>
        IValueModifiers TaxRevenue { get; }

        /// <summary>
        /// The net financial balance per year.
        /// </summary>
        /// <remarks>
        /// <b>This one composes with the others, so mind the double count.</b>
        /// The game computes the balance by calling the tax, admin and subsidy
        /// functions above, so a modifier on any of those already shows up
        /// here. A modifier registered on the balance itself applies
        /// <i>on top of</i> the already-modified components. Modify the
        /// component you actually mean, and reach for this only when you want
        /// to move the bottom line irrespective of where it came from.
        /// </remarks>
        IValueModifiers FinancialBalance { get; }
    }

    /// <summary>
    /// Contributions to a value the game computes on demand, rather than to a
    /// field it stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are functions of state — admin cost, tax revenue, the financial
    /// balance — so a rate per year makes no sense for them. What applies is a
    /// multiplier, an offset, or both.
    /// </para>
    /// <para>
    /// <b>Composition is fixed and order-independent:</b>
    /// </para>
    /// <code>
    /// final = base * (every multiplier multiplied together)
    ///              + (every offset added together)
    /// </code>
    /// <para>
    /// Multipliers commute and offsets commute, so the result does not depend
    /// on which mod registered first — unlike a pipeline of arbitrary
    /// transforms, where it would. The cost of that guarantee is that you
    /// cannot express "offset first, then scale"; register the scaled value as
    /// your offset instead.
    /// </para>
    /// <para>
    /// These functions are called from UI and AI code, potentially many times
    /// per frame, so a callback here must be cheap. It must also be free of
    /// side effects: the game may call the same function several times in one
    /// frame, and nothing promises how often.
    /// </para>
    /// </remarks>
    public interface IValueModifiers
    {
        /// <summary>
        /// Scale the value. Returning 1 leaves it alone. Multipliers from
        /// several mods multiply together.
        /// </summary>
        void Multiply(string key, Side side, Func<IValueContext, float> factor);

        /// <summary>
        /// Shift the value, in its own units, after all multipliers have
        /// applied. Offsets from several mods add together.
        /// </summary>
        void Offset(string key, Side side, Func<IValueContext, float> amount);

        /// <summary>
        /// Removes whatever was registered under the key, multiplier or offset.
        /// Returns false if the key was not registered.
        /// </summary>
        bool Remove(string key);

        /// <summary>Registered keys, for diagnostics.</summary>
        IReadOnlyCollection<string> Keys { get; }
    }

    /// <summary>What a value modifier is being asked about.</summary>
    public interface IValueContext
    {
        /// <summary>Whose value this is.</summary>
        Side Side { get; }

        /// <summary>Raw alliance index, for callers that want the game's numbering.</summary>
        int Alliance { get; }

        /// <summary>
        /// The value before any modifier applied. Always the game's own
        /// result, never a partially modified one, so a callback cannot
        /// accidentally depend on another mod's contribution.
        /// </summary>
        float BaseValue { get; }

        /// <summary>
        /// The category the game is asking about — a subsidy or tax id — or -1
        /// when it wants a total or the function takes no category.
        /// </summary>
        int Index { get; }
    }

    /// <summary>
    /// Contributions to a quantity held per alliance rather than per state.
    /// </summary>
    /// <remarks>
    /// Scoped to the two belligerents. The game keeps these fields for all
    /// eight alliances, foreign powers included, but <see cref="Side"/> has no
    /// name for Britain or France, so contributing to them is not offered here.
    /// </remarks>
    public interface IAllianceRateModifiers
    {
        /// <summary>
        /// Contribute a rate per game year. Adding twice under one key replaces
        /// the first.
        /// </summary>
        /// <param name="key">Your stable key, conventionally <c>"yourmod.something"</c>.</param>
        /// <param name="side">Whose quantity to move. <see cref="Side.Both"/> moves each side's own.</param>
        /// <param name="ratePerYear">
        /// Change per game year, in the quantity's own units. Called once per
        /// fold, so keep it cheap and free of side effects. May be negative.
        /// </param>
        void Add(string key, Side side, Func<IAllianceContext, float> ratePerYear);

        /// <summary>Removes a contribution. Returns false if the key was not registered.</summary>
        bool Remove(string key);

        /// <summary>Registered keys, for diagnostics.</summary>
        IReadOnlyCollection<string> Keys { get; }
    }

    /// <summary>What an alliance-scoped contribution is being asked about.</summary>
    public interface IAllianceContext
    {
        /// <summary>Whose quantity this invocation concerns.</summary>
        Side Side { get; }

        /// <summary>Raw alliance index, for callers that want the game's numbering.</summary>
        int Alliance { get; }

        /// <summary>
        /// The present value of the quantity being contributed to — national
        /// morale as 0 to 1, treasury in currency.
        /// </summary>
        float CurrentValue { get; }

        /// <summary>
        /// Game years since this contribution was last folded. Already applied
        /// to your returned rate.
        /// </summary>
        float DeltaYears { get; }
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

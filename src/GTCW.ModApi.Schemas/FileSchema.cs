using System.Collections.Generic;

namespace GTCW.ModApi.Schemas
{
    /// <summary>
    /// How a <c>Config/</c> file is laid out. The format is positional and
    /// line-oriented: a count header, then one field per line in a fixed order,
    /// with list fields packed into fixed-width slots.
    /// </summary>
    /// <remarks>
    /// Shipped conventions, from <c>code-findings/06-config-file-format.md</c>:
    /// list entries are <c>partlen</c> characters plus a one-character
    /// separator (<c>;</c>) including a trailing one; dates are
    /// <c>dd-mm-yyyy</c>, day first; line endings are CRLF and the encoding is
    /// <c>Encoding.Default</c> — system ANSI, <b>not</b> UTF-8.
    /// <para>
    /// The format is brittle by construction: field count, order, line count
    /// and character offsets are all load-bearing, and the game's parses are
    /// unguarded. A missing line does not degrade — it throws, or silently
    /// shifts every subsequent field. Which is why the override pipeline
    /// validates before it injects.
    /// </para>
    /// </remarks>
    public sealed class FileSchema
    {
        public FileSchema(string path, IReadOnlyList<FieldSchema> fields)
        {
            Path = path;
            Fields = fields;
        }

        /// <summary>Relative path as the game writes it, e.g. <c>Config/projects.dat</c>.</summary>
        public string Path { get; }

        /// <summary>Fields in file order. Order is the addressing scheme.</summary>
        public IReadOnlyList<FieldSchema> Fields { get; }
    }

    /// <summary>One line of a record.</summary>
    public sealed class FieldSchema
    {
        public FieldSchema(string name, FieldKind kind, int partLength = 0)
        {
            Name = name;
            Kind = kind;
            PartLength = partLength;
        }

        /// <summary>The field's name in the game's own parser, not its UI label.</summary>
        /// <remarks>
        /// Matching a field by its label is unsafe: four pairs of distinct
        /// fields share label text, one live and one dead
        /// (<c>code-findings/29-dead-prefs-audit.md</c>).
        /// </remarks>
        public string Name { get; }

        public FieldKind Kind { get; }

        /// <summary>Slot width for list fields; zero otherwise. Shipped default is 3.</summary>
        public int PartLength { get; }
    }

    /// <summary>The field types the parsers produce.</summary>
    public enum FieldKind
    {
        Int,
        Float,
        Bool,
        String,

        /// <summary><c>dd-mm-yyyy</c>. <c>Tools.Date</c> clamps rather than throwing.</summary>
        Date,

        /// <summary>
        /// Fixed-width packed ints. <c>Tools.GetIntPartitions</c> uses
        /// <c>int.TryParse</c> and silently drops anything unparseable, so a
        /// typo'd id is invisible today — the validator reports it instead.
        /// </summary>
        IntList,

        /// <summary>Fixed-width packed strings.</summary>
        StringList,
    }
}

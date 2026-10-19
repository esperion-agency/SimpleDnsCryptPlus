namespace SimpleDnsCrypt.Utils.Models
{
    /// <summary>
    ///     One <c>##</c>-delimited entry of a resolver description file: the name on the first line, the
    ///     description on the lines that follow, and the stamp that made the entry worth keeping.
    /// </summary>
    public class StampFileEntry
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     Null until a line of the entry decodes. <see cref="StampTools.ReadStampFileEntries"/> only
        ///     keeps an entry that produced one, so a reader that has an entry in hand can rely on it —
        ///     but the type says where that guarantee comes from.
        /// </summary>
        public Stamp? Stamp { get; set; }
    }
}

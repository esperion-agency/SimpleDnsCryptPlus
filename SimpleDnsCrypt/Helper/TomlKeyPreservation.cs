using Nett;
using System;
using System.Linq;

namespace SimpleDnsCrypt.Helper
{
    /// <summary>
    ///     Carries over the keys a TOML document holds that the application's typed model has no property
    ///     for, so that saving the settings the GUI owns does not delete the settings it does not.
    ///     <para>
    ///         <see cref="DnscryptProxyConfigurationManager.SaveConfiguration"/> writes the model, and dnscrypt-proxy
    ///         accepts keys the GUI never exposed — <c>enable_hot_reload</c>, <c>bootstrap_resolvers</c>,
    ///         <c>[ip_encryption]</c>, <c>[captive_portals]</c>, <c>[schedules]</c> among them. Before this existed,
    ///         a user who added one of those by hand lost it on the next save from the application.
    ///     </para>
    ///     <para>
    ///         The carried-over keys are <c>file-only by decision (Ardy, 2026-10-19)</c>: they survive a save and
    ///         are deliberately not promoted to UI settings. Adding one to <see cref="DnscryptProxyConfiguration"/>
    ///         is a decision to make, not a cleanup to do — the writer emits the C# default for every property the
    ///         loaded file never mentioned, so a modelled <c>bool enable_hot_reload</c> would start writing
    ///         <c>false</c> into configurations that never spoke of the feature. Promote one key at a time, with a
    ///         control in the UI and with dnscrypt-proxy's own default as the initial value.
    ///     </para>
    /// </summary>
    public static class TomlKeyPreservation
    {
        /// <summary>
        ///     Copies every key held by <paramref name="source"/> that <paramref name="target"/> does not own
        ///     into <paramref name="target"/>, descending into tables both sides hold. A key the target owns
        ///     is never overwritten: the model decides what the application writes.
        ///     <para>
        ///         Keys inside a table array the model owns (a <c>[[key]]</c> element) are not merged
        ///         element by element. There is no identity to align an element on — the GUI owns those lists
        ///         wholesale, and reordering one would graft a preserved key onto the wrong entry — so an
        ///         element the target lacks is copied whole and one the target holds is left as written.
        ///         A key added by hand inside an existing entry of a list the GUI edits is therefore not
        ///         covered; a key in a section the GUI has never heard of is.
        ///     </para>
        /// </summary>
        public static void CopyUnknownKeys(TomlTable target, TomlTable source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var row in source.Rows.ToList())
            {
                TomlObject targetValue;
                if (!target.TryGetValue(row.Key, out targetValue))
                {
                    // A differently-cased spelling of a key the model owns is the model's key, not an
                    // unknown one: TOML keys are case-sensitive, dnscrypt-proxy documents them lowercase,
                    // and emitting both spellings of one setting into one file would be a new bug.
                    if (target.Keys.Any(owned => string.Equals(owned, row.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    target[row.Key] = row.Value;
                    continue;
                }

                TomlTable? sourceTable, nestedTarget;
                if (TryGetTable(row.Value, out sourceTable) && TryGetTable(targetValue, out nestedTarget))
                {
                    CopyUnknownKeys(nestedTarget, sourceTable);
                }
            }
        }

        private static bool TryGetTable(TomlObject? value,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TomlTable? table)
        {
            table = value != null && value.TomlType == TomlObjectType.Table ? value.Get<TomlTable>() : null;
            return table != null;
        }
    }
}

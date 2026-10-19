using System;

namespace SimpleDnsCrypt.Utils
{
    public static class UpdateChecker
    {
        /// <summary>
        /// True when <paramref name="candidateVersion"/> is strictly newer than
        /// <paramref name="current"/>. Anything unparseable is treated as "no update" rather than
        /// guessed, because a wrong answer here either spams the user or, worse, offers a downgrade.
        ///
        /// Requires at least major.minor.patch: "1.0" is rejected rather than read as "1.0.0", so a
        /// malformed manifest cannot quietly become an upgrade offer.
        /// </summary>
        public static bool IsNewer(string candidateVersion, Version current)
        {
            if (current == null || string.IsNullOrWhiteSpace(candidateVersion))
            {
                return false;
            }

            var candidate = ParseStrict(candidateVersion);

            return candidate != null && candidate > current;
        }

        /// <summary>
        /// The version, or null when the text is not a two-to-four part numeric version. Returned
        /// rather than handed through an out parameter so the caller cannot read a value that the
        /// shape check rejected.
        /// </summary>
        private static Version? ParseStrict(string text)
        {
            var parts = text.Trim().Split('.');

            if (parts.Length < 3 || parts.Length > 4)
            {
                return null;
            }

            foreach (var part in parts)
            {
                if (part.Length == 0 || !int.TryParse(part, out var n) || n < 0)
                {
                    return null;
                }
            }

            // Version.TryParse accepts "1.0" and things like "1.0.0.0.0"; the shape check above has
            // already narrowed the input, so this only has to produce the value.
            return Version.TryParse(text.Trim(), out var parsed) ? parsed : null;
        }
    }
}

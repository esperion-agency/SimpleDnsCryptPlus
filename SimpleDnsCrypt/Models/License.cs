using System;

namespace SimpleDnsCrypt.Models
{
    /// <summary>
    ///     Object to represent a license in the license view.
    /// </summary>
    public class License
    {
        /// <summary>
        ///     Visible header Text.
        /// </summary>
        public string LicenseHeaderText { get; set; } = string.Empty;

        /// <summary>
        ///     Link to the code (github).
        ///     May stay unset: guarded by a <c>null</c> check in <c>AboutViewModel.HyperlinkRequestNavigate</c>.
        /// </summary>
        public LicenseLink? LicenseCodeLink { get; set; }

        /// <summary>
        ///     Link to publisher website.
        ///     May stay unset: licenses without a website (e.g. "Simple DNSCrypt") do not assign it.
        /// </summary>
        public LicenseLink? LicenseRegularLink { get; set; }

        /// <summary>
        ///     The license text.
        ///     May be <c>null</c>: <c>AboutViewModel.LoadLicense</c> returns <c>null</c> if the embedded resource is missing.
        /// </summary>
        public string? LicenseText { get; set; }
    }

    /// <summary>
    ///     A license link inside the a license.
    /// </summary>
    public class LicenseLink
    {
        /// <summary>
        ///     Visible Text.
        /// </summary>
        public string LinkText { get; set; } = string.Empty;

        /// <summary>
        ///     Uri to browse.
        ///     Nullable because <c>AboutViewModel.HyperlinkRequestNavigate</c> tests it for <c>null</c> before opening.
        /// </summary>
        public Uri? LinkUri { get; set; }
    }
}

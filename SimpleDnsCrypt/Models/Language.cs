namespace SimpleDnsCrypt.Models
{
    public class Language
    {
        /// <summary>
        ///		The name of the language.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        ///		Example: en
        /// </summary>
        public string ShortCode { get; set; } = string.Empty;
        /// <summary>
        ///     Example: en-US
        /// </summary>
        public string CultureCode { get; set; } = string.Empty;
    }
}

using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// The update manifest published as a release asset. Deliberately JSON, not YAML: this project
    /// already depends on Newtonsoft.Json, so a JSON manifest adds no dependency, whereas the old
    /// YAML one kept a whole package alive for a schema nothing else reads.
    ///
    /// The signature is carried inline rather than pointed at, because a separate signature URL is
    /// a second thing an attacker with release-write could swap independently of the payload.
    /// </summary>
    public sealed class UpdateManifest
    {
        private static readonly Regex HexSha256 =
            new Regex("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

        /// <summary>
        /// Only <see cref="Parse"/> can build one: every member is a value that has already cleared the
        /// whole rejection list, so a caller never has to wonder whether a half-read manifest is in hand.
        /// </summary>
        private UpdateManifest(string format, string version, string releaseDate, Uri downloadUri,
            string sha256, string signatureArmored)
        {
            Format = format;
            Version = version;
            ReleaseDate = releaseDate;
            DownloadUri = downloadUri;
            Sha256 = sha256;
            SignatureArmored = signatureArmored;
        }

        [JsonProperty("format")]
        public string Format { get; }

        [JsonProperty("version")]
        public string Version { get; }

        [JsonProperty("releaseDate")]
        public string ReleaseDate { get; }

        [JsonProperty("downloadUri")]
        public Uri DownloadUri { get; }

        [JsonProperty("sha256")]
        public string Sha256 { get; }

        [JsonProperty("signatureArmored")]
        public string SignatureArmored { get; }

        /// <summary>
        /// Returns null for anything that is not a complete, well-formed, https-only manifest.
        /// Never throws, so a hostile or truncated response cannot crash the caller.
        /// </summary>
        public static UpdateManifest? Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception)
            {
                return null;
            }

            var format = ReadString(root, "format");
            var version = ReadString(root, "version");
            var releaseDate = ReadString(root, "releaseDate");
            var downloadUri = ReadString(root, "downloadUri");
            var sha256 = ReadString(root, "sha256");
            var signature = ReadString(root, "signatureArmored");

            if (format != "zip" || version == null || releaseDate == null ||
                downloadUri == null || sha256 == null || signature == null)
            {
                return null;
            }

            if (!Uri.TryCreate(downloadUri, UriKind.Absolute, out Uri? parsedUri) ||
                parsedUri == null || parsedUri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            if (!HexSha256.IsMatch(sha256))
            {
                return null;
            }

            return new UpdateManifest(format, version, releaseDate, parsedUri,
                sha256.ToLowerInvariant(), signature);
        }

        /// <summary>
        /// A missing, wrongly-typed or blank property is null: the manifest is hostile input, so
        /// "absent" and "empty" are both reasons to reject rather than values to substitute.
        /// </summary>
        private static string? ReadString(JObject root, string property)
        {
            var token = root[property];

            if (token == null || token.Type != JTokenType.String)
            {
                return null;
            }

            var value = token.Value<string>();

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

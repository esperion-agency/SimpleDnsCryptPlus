# Backlog triage

## Read this first: two trackers, same numbers

Issues live in two places, and their numbers collide. `#19` is a RAM leak in one tracker and an
unrelated closed support ticket in the other. Always write the repository:

| Shorthand | Tracker | Open issues at the time of writing |
|---|---|---|
| `DNSCrypt/SimpleDnsCrypt#N` | upstream, dormant since 2020 apart from binary bumps | 146 |
| `instantsc/SimpleDnsCrypt#N` | the .NET 8 fork this project merged | 11 |

Anything in this file that does not name its repository is a bug in this file.

## Closed by the work in this fork

| Issue | Complaint | Where it stands |
|---|---|---|
| `instantsc#19` | 4 GB RAM usage | Fixed. The three log views are bounded by `BoundedObservableCollection<T>` (default 1000 lines), enforced at the type so a revert is a compile error. `Tests/BoundedLogCollectionTests.cs`. |
| `DNSCrypt#287` | Query log dead until restart | Fixed. `LogTailReader.ResolveResumeOffset` restarts at 0 when the file shrinks, instead of seeking past EOF and re-recording a stale offset every 500 ms. `Tests/LogTailReaderTests.cs`. |
| `instantsc#16` | Please ship a portable build | Addressed. CI publishes `SimpleDNSCryptPlus-{x64,x86}-<ver>-portable.zip`. |
| `instantsc#20` | x86 version? | Addressed. Both architectures are in the CI matrix; `fetch-proxy.ps1` pins the win32 archive too. |
| `instantsc#21` | Update dnscrypt-proxy | Addressed. 2.1.5 → 2.1.18, fetched at build time against pinned digests. |
| `DNSCrypt#585` | Stop committing external binaries | Addressed, and by a different route than proposed: binaries are downloaded at build time and SHA-256-checked against `tools/dnscrypt-proxy.lock.json`, so the build is reproducible without a submodule's "source is the truth" semantics. |
| `DNSCrypt#576` | Config carries the legacy v2 resolver list | Covered twice: instantsc's integer-versioned migration already rewrites `public-resolvers`/`relays` to v3, and this fork narrowed that rewrite so it only fires on an untouched v2 default and stops destroying custom `sources`. |
| `instantsc#27`, `DNSCrypt#554`, `DNSCrypt#559`, `DNSCrypt#581`, `DNSCrypt#537` | Is this abandoned? What are the alternatives? | Answered by the fork existing. The open half of this — announcing it into those threads — has not been done yet, and it is a human decision, not a code task. |

## Investigated, not fixed

**`instantsc#8` — "Not working if DoH Path is something other than `/dns-query`".**

The reporter's own data points away from the GUI: with `/dns-query` everything works, and with any
other path the Windows query log shows resolver `-` at 0 ms while *their server* logs endless
`<random>.test.dnscrypt` NS queries. That probe is dnscrypt-proxy's resolver validation
(`nsec/version`), so the reading is: the client reaches a URL that is not the resolver endpoint, the
probe never succeeds, and the resolver is never marked usable.

Why no GUI change fixes it: a custom resolver in this application is nothing but a stamp.
`SimpleDnsCrypt/Models/DnscryptProxyConfiguration.cs:1075` —

```csharp
public class Static
{
    public string stamp { get; set; }
}
```

One property. There is no path field to write, because a DNS Stamp does not carry an HTTPS path.
So the question is not "where is the bug in `StampTools`" — and for the record, `StampTools.Encode`
has **no caller anywhere in the application**; the only thing that invokes it is its own unit test.
The UI validates a stamp by *decoding* it (`AddCustomResolverViewModel.cs:29`), so an encode-side fix
would change nothing a user can see. The real question is "does dnscrypt-proxy accept a non-default DoH
path at all, and through which key".

Next step, in order: confirm against dnscrypt-proxy's own configuration documentation/source; if it
supports one, add that key to `Static` and surface it; if it does not, this is an upstream feature
request and the honest answer to the reporter is that, not a silent close.

**`instantsc#13` — "Trouble generating the DNS stamp".** Not a defect. The user has a RethinkDNS
link and needs the resolver's hash/hostname to build a stamp. `dnscrypt.info/stamps` is jedisct1's
tool and answers that. The app's behaviour — telling them "no hash, no hostname" — is correct for a
stamp that genuinely lacks those fields. Worth a documentation note about where a self-hosted resolver's
stamp comes from; not worth code.

**`instantsc#17` — a shut-down resolver is still selectable.** Correct observation, and it is *data*:
the resolver list is downloaded from `public-resolvers.md`, curated upstream. Nothing in this
repository can remove an entry without forking the list. The fix belongs in the DNSCrypt resolvers
repository.

**Uninstall still leaves DNS on the cards it cannot see at the time you press the button — the half that
is left.** `Uninstall.exe` was rewritten to restore from the pre-change snapshot across every adapter
regardless of link state (see the CHANGELOG entry), and `docs/testing/r0-r2-checklist.md` section 2.6
tests it. What is *not* fixed is the application's own in-app uninstall: `MainViewModel` still recovers
interfaces through `LocalNetworkInterfaceManager.GetLocalNetworkInterfaces` with its default filter
(operational and non-blacklisted), and then re-finds the card with `Description.Equals(...)`, where
`Description` carries the adapter's address list — so a DHCP change between the two enumerations makes
that row a silent no-op. Point it at `InterfaceDnsSnapshot.BuildRestoreCommands` the same way the tool
does. Needs an **R2** sandbox to prove any fix; this is not a code review change.

**`Global.NetworkInterfaceBlacklist` is now an alias, not a second list.** The third copy — the private
array inside `Uninstall/Program.cs` — is gone, so the portable and installed channels can no longer
disagree about which adapters to restore. `Tests/InterfaceDnsSnapshotTests` pins both that the shared
list still holds its 14 entries and that the two read the same array instance.

**One main-view heading had no translation key.** Fixed: `Text="Proxy Logging"` in
`SimpleDnsCrypt/Views/MainView.xaml` became `{lex:Loc Key=advanced_settings_proxy_logging}`. Doing it with a
scanned instrument rather than by hand found **17** untranslated literals across the markup, not one — the
Proxy Logging heading and its description line, the resolvers grid's seven column headers, the tray menu's
"Show Window", the "Install" and "Add custom resolver" buttons, the empty-resolvers message, the whitelist
tooltip and the three labels in the custom-resolver dialog. All 17 now resolve through the neutral file (the
"Enabled" header reuses upstream's `enabled` key, so it arrives in 34 languages for free), and
`Tests/LocalizationCoverageTests.cs` holds the line twice over: no literal prose in `Text`/`Content`/`Header`,
and every `{lex:Loc Key=…}` the markup names resolves for every offered language — `OutputMissingKeys="False"`
means a mistyped key shows as a blank label, which the resx files alone cannot catch.

**Translations are still frozen; new strings ship English-only.** The 16 keys added for those literals exist
only in the neutral `Translation.resx`, which reaches every culture through `ResourceManager`'s invariant
fallback. A German user therefore reads "Proxy Logging" in English — which is what they read before, except
that it is now a key someone can supply a translation for without touching markup.

**The dialog that asks for a rule label said "Rule:" in code.** `QueryLogViewModel` and
`DomainBlockLogViewModel` each passed a literal to `ShowInputAsync`; both use `dialog_message_rule` now.
Two fire-and-forget calls that the build was warning about (`CS4014`) are awaited: `SaveAdvancedSettings`
and `ClearDomainBlackList`. The remaining `CS0067` is a genuinely unused `IDataErrorInfo.ErrorsChanged`
event on `MainViewModel`.

**Saving emits defaults for keys the file never held.** `SaveConfiguration()` writes the whole typed model,
so every property the loaded file did not supply comes out as its C# default. Observed directly: saving a
document that carried 12 top-level keys and 5 tables produced additional keys the input never had —
`ipv6_servers`, `doh_servers`, `require_nolog`, `log_level`, `use_syslog`, `netprobe_timeout`,
`cache_min_ttl`, `reject_ttl` and an empty `[static]` among them. In practice the file is created from
`dnscrypt-proxy.toml.example`, which already lists those keys, so the emission is invisible; it is not
harmless in general, because a proxy default that differs from C#'s `false`/`0` gets written as the wrong
value. This is also why the 2.1.18 keys the GUI has no control for were left *unmodelled*: `TomlKeyPreservation`
keeps them verbatim, while adding `bool enable_hot_reload` to the model would start emitting `false` into
every file that never mentioned it.

**Decided 2026-10-19 (Ardy): those keys stay file-only.** `TomlKeyPreservation` carries them through a save
verbatim and they are deliberately *not* promoted to UI settings — the blocker the 1.0.0 plan named (a
hand-added key deleted on save) is closed by preservation, not by modelling. Raising one of them into
`DnscryptProxyConfiguration` is therefore a decision to make, not a cleanup to do: it must come one key at a
time, with a control in the UI and with the proxy's own default as the property's initial value, because the
moment it is in the model it is emitted into every file. The phrase `file-only by decision (Ardy, 2026-10-19)`
appears in the XML docs of `TomlKeyPreservation` and `DnscryptProxyConfigurationManager` so neither a
developer nor an AI re-litigates it by accident.
`Tests/ProxyConfigurationRoundTripTests.EveryKeyInTheSavedFile_ComesFromTheFileOrFromTheModel`
guards the boundary this fix is responsible for and deliberately does not assert anything about defaults.

**The loader folds a mis-cased key onto the property it resembles.** `TomlSettings` maps properties with
`UseTargetPropertySelector(standardSelectors => standardSelectors.IgnoreCase)`
(`SimpleDnsCrypt/Helper/DnscryptProxyConfigurationManager.cs:15`), so a hand-written `REQUIRE_DNSSEC = false`
is read as `require_dnssec` and, arriving later in the document, wins over the real `require_dnssec = true`.
The save then writes one line with the folded value. The preservation fix at least guarantees the file never
ends up with two spellings of the same setting, and
`Tests/ProxyConfigurationRoundTripTests.DifferentlyCasedKeyTheModelOwns_IsNotPreservedAlongsideTheWrittenOne`
pins that. Making the read case-sensitive is a separate decision: it would start ignoring keys that are
legitimately spelled with another case today.

**`StampTools.TryDecode` reported success for a stamp that decoded to nothing — fixed.** It returned `true`
whenever `Decode` did not throw, and `Decode` returns null at six points: a string without the `sdns://`
prefix, an empty payload, and each recognized protocol whose bytes are truncated
(`SimpleDnsCrypt.Utils/StampTools.cs:73-244`), so `TryDecode("not a stamp at all", out var s)` answered
`true` with `s == null` — the "try" half of the name was wrong about its own failure mode. It now returns
`stamp != null`. No caller changed behavior: `ReadStampFileEntries` ignores the bool and gates on
`stamp != null` (`:279-283`), and the resolver dialog validates through `Decode(s)?.ValidationIssues`
(`SimpleDnsCrypt/ViewModels/AddCustomResolverViewModel.cs:29`). Pinned both ways by
`Tests/StampDecodeTests.TryDecode_StampThatDecodesToNothing_ReportsFailure` (five shapes) and
`TryDecode_ValidStamp_ReportsSuccess`, so a permanent `return false` would not pass either. Each failure
case asserts `Decode(...) Is.Null` before asserting on `TryDecode`, so a red means the defect and not an
exception escaping the parse.

**Widening the warnings-as-errors gate.** `Directory.Build.props` promotes the `CS86xx` nullable family
to errors in Release (`WarningsAsErrors=nullable`, with `TreatWarningsAsErrors` left **false**). The
analyzer rules the plan named are configured in `.editorconfig` at `warning`, and measured against the
whole solution with those severities on, counting each site once (the WPF compiler runs the analyzers a
second time over a `*_wpftmp` project, which would otherwise double every number):

| Rule | Sites | Files |
|---|---|---|
| CA1031 — catch a general exception type | 92 | 36 |
| CA1062 — validate an external argument before dereferencing | 62 | 28 |
| IDE0005 — unnecessary using directive | not reported during a build at all | — |

Those are not defects to sweep. CA1031 in this codebase is mostly the deliberate "a service, a file or the
network misbehaved and a GUI must still start" catch, and each one wants a decision about what the user is
told, not a mechanical `throw`. Promote one rule at a time, per project: fix its count to zero, then move
that rule from `warning` to `error` in `.editorconfig` and let `WarningsAsErrors` grow. Note that IDE0005
is reported by the IDE but not during `dotnet build` even with `EnforceCodeStyleInBuild=true`, so a claim
about it needs a `dotnet format --verify-changes` run, not a build log — that is why the third row says
"not reported" rather than "zero".

**The encoding contract is undecided, and `.editorconfig` says so rather than guessing.** Line endings
are settled: every tracked text file the repository cares about is CRLF in the working tree, and the rule
is scoped to the text families so it can never be applied to a `.png`. UTF-8 BOM is not: measured over
`git ls-files`, 95 of 127 `.cs`, 25 of 27 `.xaml`, 19 of 19 `.txt`, 2 of 2 `.manifest` and 1 of 15 `.md`
carry one, while 32 `.cs`, 34 of 36 `.resx` and every `.yml`, `.json`, `.ps1`, `.py` and `.wxs` do not.
Adding the BOM to the minority is a 32-file whitespace commit that no test asks for; removing it from the
majority is the same commit in the other direction and changes what Windows PowerShell 5.1 assumes about
the `.ps1` files. Either is a decision for whoever next touches those files, and it should come with the
`.gitattributes` line that makes it stick (`*.cs text working-tree=crlf`) rather than with a rule an editor
enforces one file at a time.

**`ManageFallbackResolvers` commits when the user presses Cancel.** The dialog edits
`fallback_resolvers`, but the "what was it before" snapshot taken two lines earlier is
`listen_addresses` (`SimpleDnsCrypt/ViewModels/MainViewModel.cs:869`), and the cancel path at `:877`
compares the proposed list against that wrong snapshot. Fallback resolvers are not the listen addresses
in any normal configuration, so the comparison almost never matches and the method writes the list and
calls `SaveAdvancedSettings()` even though the dialog returned `false`. `ListenAddresses()` right below
it uses the same shape correctly (`:887` snapshots `listen_addresses` for the `listen_addresses` dialog).
The fix is one identifier at `:869` plus a test that opens the dialog, changes nothing, cancels, and
asserts no save happened — which needs the dialog result to be injectable, so it is a small design job
and not a drive-by. Not touched by the nullable pass, which only made the snapshot null-safe.

## Needs a reproduction before anyone touches it

| Issue | Complaint | What is missing |
|---|---|---|
| `DNSCrypt#564` | Whitelist rules do not work | Which list (`domain-whitelist.txt` vs `cloaking`), and whether `query_log` shows the name being seen at all. Our Stage 2 log changes make this easier to answer. |
| `DNSCrypt#517` | Rejected queries flood the log; add a filter | A real feature request. The 1000-line cap stops the memory growth but does not filter content. |
| `DNSCrypt#542` | Unresponsive query log table | Retest against a build with bounded collections and report the result. The unbounded list is a plausible cause; unproven. |
| `DNSCrypt#548` | Freeze on the Resolvers tab | Retest: the resolver list is ~195 entries and the tab builds a filtered view over it. Report OS and adapter count. |
| `DNSCrypt#565` | Wi-Fi card missing from the list | Almost always the blacklist. With `SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS=1` (or ticking "show hidden") a reporter can now tell us whether the filter is the cause. Virtual adapters are now *marked* instead of hidden, which keeps the two complaints apart: "I cannot tell what this card is" has a different answer from "my card is missing", and `NetworkInterfaceBlacklist` is pinned at its 14 entries by `Tests/NetworkInterfaceFilterTests.cs` so fixing the first cannot deepen the second. |

## Will not be fixed here, with reasons

| Issue | Why |
|---|---|
| `DNSCrypt#574` | Antivirus false positives. No Authenticode certificate exists for this project; see `docs/AV-FALSE-POSITIVES.md`. This stays open as a known cost, not a solved problem. |
| `instantsc#25` | Artifact weight. ~90 MB is self-contained WPF: `Microsoft.WindowsDesktop.App` cannot be trimmed. CI asserts a size budget so it cannot get *worse* silently. Lighter means a different UI toolkit, which is a rewrite. |
| `DNSCrypt#573` | macOS client. This is WPF. |
| `DNSCrypt#516`, `DNSCrypt#563`, `instantsc#18` | "Spotify breaks", "DNS leak test fails with VPN", "WireGuard". Configuration interactions on the reporter's network, usually resolvable with `forwarding-rules` or a excluded app; not reproducible here, and answering them correctly needs their setup. |
| `DNSCrypt#513` | The Chocolatey package is a third-party publication, not ours. |
| `DNSCrypt#526` | Asks for an AppVeyor build-date badge. There is no AppVeyor project; CI is GitHub Actions now. |
| `DNSCrypt#507` | Move language resources to one folder. Cosmetic churn across every `.resx` reference for no user-visible gain. |
| `instantsc#24` | Dark mode. Real, and MahApps.Metro 2.4.x tops out at the stable release we are pinned to — theming work here is a UI project, not a setting. |

## What "triaged" means in this repository

An issue moves out of the piles above only when there is a written reproduction someone else can run,
or a test that fails before the change and passes after. "I think it is the same cause" is a note, not
a fix, and closing on a note is how this project accumulated 146 open issues in the first place.

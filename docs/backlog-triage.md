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

**Uninstall leaves DNS on the cards it cannot see at the time you press the button.**
`SimpleDnsCrypt/ViewModels/MainViewModel.cs:1321-1332` says in a comment that it clears "also the
hidden and down cards", then calls `GetLocalNetworkInterfaces` with its defaults, which filter to
operational and non-blacklisted. Any interface that is down or blacklisted when uninstall runs keeps
its static DNS servers — the one failure mode `docs/testing/dns-safety.md` is written around. The
same block then re-finds the card to update by `Description.Equals(...)`, and `Description` carries
the adapter's address list, so a DHCP change between the two enumerations makes the row a silent
no-op. Needs an **R2** sandbox to prove any fix; this is not a code review change.

**`Uninstall/Program.cs` keeps its own third copy of the interface filter.** Lines 59-95 re-implement
the blacklist and the operational check instead of calling `LocalNetworkInterfaceManager`, and the
MSI runs it as a deferred custom action — so the portable and installed channels can disagree about
which adapters to restore. Recorded now because a *marker* list was added beside the blacklist:
anyone merging them has to decide which of the three copies survives. Also R2-gated, so it stays
separate from the visual work that surfaced it.

**One main-view heading has no translation key.** `SimpleDnsCrypt/Views/MainView.xaml:732` sets
`Text="Proxy Logging"` while seventeen of the eighteen other headings in the file go through
`{lex:Loc}` (the nineteenth binds the resolver count, so it has nothing to translate). Found
while moving headings onto a shared style, and deliberately not folded into that change: it needs a
resx key and a decision about which log the section actually is. Cosmetic in English, missing in the
other 33 languages.

**Saving emits defaults for keys the file never held.** `SaveConfiguration()` writes the whole typed model,
so every property the loaded file did not supply comes out as its C# default. Observed directly: saving a
document that carried 12 top-level keys and 5 tables produced additional keys the input never had —
`ipv6_servers`, `doh_servers`, `require_nolog`, `log_level`, `use_syslog`, `netprobe_timeout`,
`cache_min_ttl`, `reject_ttl` and an empty `[static]` among them. In practice the file is created from
`dnscrypt-proxy.toml.example`, which already lists those keys, so the emission is invisible; it is not
harmless in general, because a proxy default that differs from C#'s `false`/`0` gets written as the wrong
value. This is also why the 2.1.18 keys the GUI has no control for were left *unmodelled*: `TomlKeyPreservation`
keeps them verbatim, while adding `bool enable_hot_reload` to the model would start emitting `false` into
every file that never mentioned it. Add those keys one at a time, with a control and with the proxy's own
default, not in a bulk pass. `Tests/ProxyConfigurationRoundTripTests.EveryKeyInTheSavedFile_ComesFromTheFileOrFromTheModel`
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

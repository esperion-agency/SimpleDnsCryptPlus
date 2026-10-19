# Changelog

All notable changes to **Simple DNSCrypt Plus** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions `0.1` through `0.7.1` belong to upstream Simple DNSCrypt by Christian Hermann, and
`0.7.2.3` through `0.8.2` to instant.sc's fork. They are listed below for continuity only — see
their respective repositories for authoritative histories.

## [Unreleased]

### Added
- **`docs/testing/r0-r2-checklist.md` — the run-sheet for the two manual gates.** The R0 pass (tray menu,
  the three drag-drop surfaces, the newly-keyed strings, section alignment) and the R2 pass in a VM or
  Windows Sandbox, with the PowerShell to run at each step: baseline capture, proof that the card's DNS is
  `127.0.0.1` and that the service owns port 53, a firewall block on outbound UDP/53 as the decisive
  offline test that no query leaves the box in clear text, the proxy's own peers listed to show they are
  on 443/5353, the stopped-proxy fallback and its recovery time, both uninstall channels separately, and
  the MSI identity checks. It ends in a results table — the row that cannot pass today (a disconnected
  adapter left behind by the application's own in-app uninstall) is named as such, and the release-notes
  caveat comes off only once the R2 rows are filled. `docs/testing/dns-safety.md` links to it as the
  procedure; that file stays the reasons.
- **Virtual adapters are marked, not hidden.** The interface list filters against
  `Global.NetworkInterfaceBlacklist`, a list of names a 2010 Windows used, so a Hyper-V or WSL
  switch, a Bluetooth PAN and any VPN whose `NetworkInterfaceType` .NET cannot name were drawn with
  the same cable icon as a physical card. On a machine with containers or a VPN active that looked
  like a filter that had silently switched itself off — it had not, the blacklist simply never knew
  those adapters. They now get a cube icon and a "Virtual or tunnel adapter" tooltip line. Nothing
  was added to the blacklist, so no adapter that used to appear has disappeared
  (`DNSCrypt/SimpleDnsCrypt#565` is the complaint that keeps this the right shape;
  `Tests/NetworkInterfaceFilterTests.cs` pins both halves). `Global.VirtualAdapterMarkers`
  decorates; `IsFilteredOut` still decides what is hidden, and the two are deliberately held to
  different standards.
- The tooltip's explanation is English-only in the neutral `Translation.resx`, which reaches every
  culture through `ResourceManager`'s invariant fallback.
  `Tests/LocalizationCoverageTests.cs` now measures that rather than trusting it, because the
  tooltip is exactly the kind of string that fails in silence.

### Changed
- **`.editorconfig` is a real contract now, and the Release build fails on a new nullable warning.** The
  file used to hold seven lines. It now carries the line-ending rule the working tree already follows
  (CRLF, scoped to the text families the repository tracks so the rule cannot be applied to a `.png`),
  using-directive placement and ordering, the `var` and expression-bodied preferences it already hinted
  at, qualification and predefined-type rules, and the severity of the analyzers the 1.0.0 plan named:
  `CA1031`, `CA1062` and `IDE0005` as `warning`. Encoding is deliberately **not** declared: the tree is a
  measured mix (95 of 127 `.cs` files carry a UTF-8 BOM and 32 do not), so the choice is recorded as open
  in `docs/backlog-triage.md` instead of being made by a line nobody verified.
  `Directory.Build.props` turns the analyzers and `EnforceCodeStyleInBuild` on for every configuration so
  an editor and CI see the same thing, and in Release sets `WarningsAsErrors=nullable` while leaving
  `TreatWarningsAsErrors` **false** — promoting everything would turn ninety-odd deliberate
  `catch (Exception)` blocks into build failures on the day they were first measured, not on the day
  someone decides about them. CI builds `-c Release` in both `ci.yml` and `release.yml`, so the gate is
  the CI gate. `docs/backlog-triage.md` records the counts and the one-step-per-rule way to widen it.
- **`SimpleDnsCrypt` and `Uninstall` are compiled with `<Nullable>enable</Nullable>` too, and the whole
  solution is at zero nullable warnings.** The app project is where the inherited code lives, so this is
  the part that took editing rather than switching: fields that a `[SetUp]`-equivalent fills became
  explicitly nullable (`MainViewModel.Instance`, `QueryLogViewModel`'s selected line), `IDisposable`
  service handles became `ServiceController?` behind a `[NotNullWhen(true)]` try-pattern, and where the
  loader has already proved a value exists the code says so with `!` and a documented invariant
  (`DnscryptProxyConfigurationManager.DnscryptProxyConfiguration` is non-null once `LoadConfiguration()`
  returns true) rather than inventing a guard that can never fire.
- **The test project keeps `<Nullable>enable</Nullable>`.** It was switched off in the working tree to
  make 76 errors disappear, which is the wrong kind of quiet: the guards would have gone on asserting
  things the compiler could no longer check. They are annotated instead — `= null!` on fields NUnit
  assigns in `[SetUp]`, deliberate null-probes written as `null!` so the intent survives, and an explicit
  `Assert.That(x, Is.Not.Null)` before a dereference that used to be able to throw
  `NullReferenceException` instead of naming what the scan did not find.
- **`SimpleDnsCrypt.Utils` is compiled with `<Nullable>enable</Nullable>` and is warning-free.** The 92
  nullable warnings that appeared when it was switched on are fixed at the source rather than silenced:
  types that genuinely can be absent became `?` (`Stamp.Address`, `UpdateManifest.Parse`,
  `DomainBlacklist.FetchRemoteListAsync`, `UpdateApplyResult.ArtifactPath`), a half-built manifest is no
  longer constructible at all (`UpdateManifest` now has one private constructor that only `Parse` calls),
  and `UpdateChecker` returns the parsed `Version` instead of an out-parameter that could be read on the
  failure path. `StampTools.Encode` now names the field a dnscrypt stamp is missing instead of throwing
  `NullReferenceException` from inside `Encoding.GetByteCount`.

### Fixed
- **`Uninstall.exe` no longer forgets the adapters that were down at uninstall time, and it no longer
  keeps its own copy of the adapter filter.** The tool had a third private copy of
  `NetworkInterfaceBlacklist` and enumerated only `OperationalStatus.Up` interfaces, so a card that was
  unplugged, disabled, or a VPN that was not connected when you pressed Uninstall kept the static DNS
  servers the application had written to it — and the next time it came up it still pointed at a proxy
  that was gone. `SimpleDnsCrypt.Utils.NetworkInterfaceFilter` is now the single list both channels read
  (`Global.NetworkInterfaceBlacklist` is an alias for it, and
  `Tests/InterfaceDnsSnapshotTests.TheApplicationAndTheUninstallerReadTheSameList` fails the moment they
  drift apart), and the restore walks every adapter regardless of link state. Writing a persistent DNS
  setting does not need the link up, and **nothing in the restore enables, disables or reconnects an
  adapter** — pinned by `RestoreCommands_NeverTryToEnableAnAdapter`.
- **A snapshot of each adapter's DNS as it was before the first write is now kept, once.**
  `InterfaceDnsSnapshot.Capture` runs immediately before `SetNameservers` touches a card and refuses to
  overwrite itself on later calls, because by then the file on disk holds the proxy's own address. So a
  port that had `192.168.1.1` before this software got it back, a machine on DHCP-supplied DNS goes back
  to that, and an adapter the application never wrote to is left alone instead of being cleared — the
  old tool deleted the static servers on *every* visible card, including one the user had configured for
  other reasons. Installations that predate the file keep the previous behaviour (clear the static
  entries), which is what `RestoreCommands_WithoutASnapshot_FallBackToDeleteOnly` pins. The netsh forms
  are the documented ones (`set dnsservers`, `add dnsservers … index=N validate=no`); `validate=no` is
  what makes writing a disconnected adapter possible at all.
- **The service status tracker can now report `NotInstalled`.** It built a tuple of
  `(TryGetDnsCryptService(out var service), service.Status)`, so the second element dereferenced the
  service handle on the failure path as well: the first tick on a machine without the `dnscrypt-proxy`
  service would have thrown `NullReferenceException` instead of yielding the `NotInstalled` the first
  switch arm was written to produce. It is now an ordinary `if (!try) return NotInstalled;`. Nothing
  subscribes to `ServiceStatusTracker` today — the enum and the observable are otherwise dead code, and
  this is a latent defect, not one a user ever met.
- **Seventeen strings in the interface can now be translated.** Scanning the markup for literals rather
  than fixing the one heading someone happened to notice found `Text="Proxy Logging"` and its description
  line, the resolvers grid's seven column headers, the tray menu's "Show Window", the "Install" and
  "Add custom resolver" buttons, the empty-resolvers message, the whitelist tooltip and the three labels of
  the custom-resolver dialog. Each is a `{lex:Loc Key=…}` reference now; the "Enabled" header reuses
  upstream's existing `enabled` key, so it arrives translated in 34 languages while the other sixteen ship
  English-only through the neutral file, as every new string here does. Two guards keep the class closed:
  `Tests/LocalizationCoverageTests.NoViewOrWindowCarriesAnUntranslatedProseLiteral` (parsed from the XAML
  tree, because `SizeToContent="WidthAndHeight"` matches a `Content=` regex and a `TextBlock` can carry its
  text as element content) and `EveryKeyReferencedByTheMarkupResolvesForEveryOfferedLanguage`, which asks
  the running application's own lookup — `OutputMissingKeys="False"` renders a mistyped key as a blank
  label, invisible to any check that reads only the `.resx` files.
- **The rule dialogs no longer ask "Rule:" in C#.** `QueryLogViewModel.BlockQueryLogEntry` and
  `DomainBlockLogViewModel.UnblockBlockLogEntry` each carried a `//TODO: translate` over a literal passed
  straight to `ShowInputAsync`; both read `dialog_message_rule` now.
- `SaveAdvancedSettings` discarded `SaveDnsCryptConfiguration()` with `_ =` next to a
  `//TODO: do something about this`, so a failed write left no trace and the button gave no signal. It is
  awaited, as the five other call sites of that method already were. `ClearDomainBlackList` awaited
  `BuildBlacklist()` for the same reason; both were `CS4014` warnings.
- **A save no longer deletes settings the GUI does not own.** `SaveConfiguration()` wrote the typed model
  and nothing else, so any key absent from `DnscryptProxyConfiguration` was erased from
  `dnscrypt-proxy.toml` the first time the application saved — including the settings dnscrypt-proxy 2.1.18
  documents but this GUI has no control for (`enable_hot_reload`, `bootstrap_resolvers`,
  `odoh_servers`, `pqdnscrypt`, `[ip_encryption]`, `[captive_portals]`, `[schedules]`, `[monitoring_ui]`,
  `[local_doh]`). `SimpleDnsCrypt/Helper/TomlKeyPreservation.cs` now reads the file that is on disk through
  Nett's own document model and carries over every key the model does not own, descending into tables the
  model does own so a hand-added key inside `[query_log]` survives too. A key the model owns is never
  overwritten by the preserved copy, so the UI cannot be silently undone by a stale file. Nett 0.15.0 has
  no extension-data attribute and cannot deserialize an unknown-shape value into a `Dictionary<string, object>`
  property, which is why the fix sits in the writer rather than in a catch-all property on the model.
  `Tests/ProxyConfigurationRoundTripTests.cs` covers a top-level key, an array-valued key, a key inside an
  owned table, a whole table, a table array, and the case that already existed: the model's own value wins.
- The main view drew its sections as thirteen copies of one checkered `Border`, each with its margin
  and padding tuned by hand. The vertical rhythm was split between 14px and 10px as a result, and
  section headings and their right-hand switches disagreed by a few pixels from tab to tab. Sections
  now share `SimpleDnsCrypt/Styles/SectionStyles.xaml`, and `Tests/MainViewStructureTests.cs` reads
  the markup as XML so a copy cannot creep back in unnoticed.
- The network cards were placed in a `UniformGrid` of four fixed columns while each tile was pinned
  to 170 device-independent pixels, so the gap between tiles never matched the gap at the edge and a
  maximized window showed mostly empty space. They now wrap at a fixed tile width with an even
  gutter, centred in the section.

## [0.9.0-rc.2] - 2026-09-28

The first release whose installers are produced by the pipeline that also signs and audits them.
`v0.9.0-rc.1` deliberately received no MSI: rebuilding that tag's payload here produced 468 runtime
files from .NET 10.0.11 against the 10.0.12 the published zips contain (`mscordaccore_amd64_amd64_
10.0.1126.37416.dll` vs `..._10.0.1226.42308.dll`), and shipping an older runtime patch beside a
newer one inside a single release is not a difference worth hiding behind the same version number.

### Added
- **A per-machine MSI per architecture**, built from the same publish layout as the portable zip so
  the two channels cannot contain different code. It registers in Apps & features, stops and removes
  the `dnscrypt-proxy` service on uninstall, restores per-interface DNS by running `Uninstall.exe` as
  a deferred custom action, and supports `msiexec /i … /qn`. See
  `docs/adr/0003-msi-as-a-second-channel-with-wix-v3.md`; the portable zip remains the only thing the
  in-app updater downloads, because the updater cannot apply an MSI.
- `build/build-msi.ps1`, `build/fetch-wix.ps1`, `build/inspect-msi.ps1` and `tools/wix.lock.json`.
  The WiX toolset is now a digest-pinned build dependency the same way `dnscrypt-proxy` already is,
  and every build audits the finished package's own tables (identity, `ALLUSERS`, the service-control
  row, the licence page, the embedded cabinet, the file count) rather than trusting the linker.
- `Tests/InstallerIdentityTests.cs`, which refuses a package carrying instant.sc's `UpgradeCode` or
  `AllowDowngrades="yes"`, and proves itself able to fail against a canary copy of their project.
- `.github/workflows/verify-release.yml`, which re-downloads a published release and asserts that
  every asset is checksummed, that every payload verifies against the committed public key, that a
  tampered copy is rejected, and that the update manifests still point only at signed zips.

### Fixed
- The README still described the pre-candidate state of the fork: its dnscrypt-proxy badge read
  2.1.5 while `tools/dnscrypt-proxy.lock.json` pins 2.1.18, its release badge resolved to
  "no releases or repo not found" because the only published release is a prerelease, and the
  comparison table listed `.NET 10` and a running CI pipeline as work still to come. All three
  have shipped.
- The published verification command no longer fails on copy-paste. `update.pub` is not inside the
  portable zip — `build/build-portable.ps1` archives only the publish folder — so the snippet now
  fetches the key from the release tag first, and the archive names carry the real `-rc.1` suffix.
  `releases/latest` is left empty on purpose until a stable release exists, so the download links
  now point at the release list.
- `Tests/ReleaseDocConsistencyTests.cs` pins the README's proxy badge to the lock file, so the
  version claim cannot go stale in silence again.

## [0.9.0-rc.1] - 2026-09-27

The first published artifact of this fork, and deliberately a release candidate rather than `0.9.0`:
no person has driven the tray menu or the three drag-drop surfaces yet, the memory-bound fix has not
been soaked, and the settings GUI covers only part of what dnscrypt-proxy 2.1.18 accepts. What the
candidate contains:

### Added
- Fork of `DNSCrypt/SimpleDnsCrypt` merged with all 58 commits of `instantsc/SimpleDnsCrypt`, so
  neither upstream's resolver-list migration nor instantsc's modernization is lost.
- `global.json` pinning the .NET 10 SDK so local and CI builds agree.
- `NOTICE.md`, `SECURITY.md`, and this file.
- A README that states plainly what this fork is and what it cannot do.

### Changed
- Product identity is now **Simple DNSCrypt Plus**: `Global.ApplicationName`, assembly title and
  product, `AssemblyName` → `SimpleDnsCryptPlus` (so the executable is
  `SimpleDnsCryptPlus.exe`), manifest assembly identity, and copyright attribution extended to
  include both predecessors by name.
- Version baseline set to `0.9.0`. It has to sort above the `0.7.1` and `0.8.2` builds still in
  circulation so no one is offered a downgrade, and that is the whole claim the number makes: `.NET 10`,
  a new update channel and a real CI pipeline are all in place, but the tray and drag-drop surfaces
  have never been driven by a human, the memory-bound fix has not been soaked, and the GUI still
  writes only part of what dnscrypt-proxy can be told. `1.0.0` is reserved for when those close.

### Removed
- Upstream's `README.md` badges pointing at a personal AppVeyor project, portable-download links
  pointing at a personal Azure Blob storage account, and a PayPal donation address belonging to
  the original author.
- `.github/FUNDING.yml`, which routed to the original author's Open Collective.
- `SimpleDnsCrypt/DNSx64/` and `SimpleDnsCrypt/DNSx86/` — superseded by instantsc's relocated
  `SimpleDnsCrypt/dnscrypt-proxy/dnscrypt-proxy{64,86}.exe`.

### Intentionally **not** renamed
The Windows service name `dnscrypt-proxy`, the single-instance mutex string `"SimpleDnsCrypt"`,
and every configuration/rule/log filename (`dnscrypt-proxy.toml`, `query.log`, `blocked.log`,
`configVersion.txt`, …) keep their original names on purpose. Renaming any of them would orphan
existing user configuration or let two managers of the same service run at once. C# namespaces and
project folder names likewise stay put.

### Changed
- Target framework moved to `net10.0-windows10.0.19041` across all four projects. The platform
  version is load-bearing: plain `net10.0-windows` makes NuGet silently fall back to
  ReactiveUI.WPF 19.5.1's .NET Framework 4.8 assets.
- Dependency updates: MahApps.Metro 2.4.10→2.4.11, ReactiveProperty 9.3.4→9.9.0,
  NLog 5.2.5→6.2.1, Hardcodet.NotifyIcon.Wpf 1.1.0→2.0.1, gong-wpf-dragdrop 3.2.1→4.0.0,
  Caliburn.Micro 4.0.212→5.0.258, and `minisign-net` 1.0.0 added for the update channel.
  `ReactiveUI.WPF` deliberately stays at 19.5.1 — see `docs/adr/0001-stay-on-reactiveui-19.md`.
- The `dnscrypt-proxy` executables are no longer committed. They are downloaded by
  `build/fetch-proxy.ps1` and verified against SHA-256 digests pinned in
  `tools/dnscrypt-proxy.lock.json`, failing closed on mismatch. Bundled version goes 2.1.5 → 2.1.18.
- `build/build-portable.ps1` assembles the shipped zip and enforces an artifact size budget
  (measured: x64 93.3 MB, x86 88.1 MB).

### Fixed
- Query, domain-block and address-block log views grew without bound for the life of the process,
  behind [instantsc/SimpleDnsCrypt#19](https://github.com/instantsc/SimpleDnsCrypt/issues/19)
  ("4gb ram usage!"). Now capped at 1000 entries each, enforced at the type level so reverting it is
  a compile error.
- The log tail readers stalled permanently after dnscrypt-proxy rotated a log file —
  [DNSCrypt/SimpleDnsCrypt#287](https://github.com/DNSCrypt/SimpleDnsCrypt/issues/287), "Query log is
  broken until Simple DNSCrypt is restarted". The seek landed past EOF and the stale offset was
  re-recorded every 500 ms forever.
- The configuration migration overwrote a user's custom resolver `sources` list unconditionally;
  it now only replaces the untouched v2 default pair. This also removes a latent
  `IndexOutOfRangeException` on a single-URL source list.
- Tagalog was an unusable translation: `Translation.tgl.resx` produced a satellite that could never
  be found, because `CultureInfo("tgl").Name` normalises to `tl`. Renamed to
  `Translation.tl.resx` and now offered in the language dropdown.

### Added
- GitHub Actions `ci.yml` and `release.yml`. The test step asserts a non-zero test count, so a
  silently-skipped test project cannot produce a green build.
- `Uninstall.exe` ships in the portable zip. It was previously harvested by the MSI, and it is what
  restores each interface to DHCP-supplied DNS.
- Tests: 5 → 60. New guards cover Caliburn view resolution, translation coverage per culture, and
  consistency between `<AssemblyName>` and the 13 WPFLocalizeExtension references that depend on it.
- `docs/adr/0001-stay-on-reactiveui-19.md`, `docs/adr/0002-portable-only-no-msi.md`,
  `docs/proxy-supply-chain.md`.
- An in-app update channel authenticated by **this project's own** minisign key
  (`tools/keys/update.pub`, compiled in as `UpdateChannel.TrustedPublicKey`). Release zips carry a
  detached `.minisig`, and `update-x64.json` / `update-x86.json` published as release assets name the
  artifact, its SHA-256 and its signature. `tools/minisign-tool` generates and applies signatures with
  no `minisign` install needed, and CI checks its output against the reference CLI — including a
  deliberately tampered copy that the CLI must reject.
- `ApplicationUpdater` on the app side: an opt-out check at startup offers a newer release, which is
  downloaded into a sibling `_update\<version>\` folder and unpacked only after both the published
  hash and the signature check out. It never replaces the running executable.
- `settings_check_for_updates` and the updater strings. These live only in the neutral
  `Translation.resx`, and a test asserts every offered language still resolves them, which is what
  proves the invariant fallback a frozen translation set depends on.

### Removed
- `YamlDotNet` and the dead `UriYamlTypeConverter`. The update manifest was their only user, and the
  manifest is JSON now, so the dependency and its About-screen entry left with it.

### Added
- `docs/testing/dns-safety.md` (the R0/R1/R2 protocol for testing software that rewrites your DNS),
  `docs/AV-FALSE-POSITIVES.md`, `translations/README.md` and `docs/backlog-triage.md`.
- A translation key-drift guard: the 34 language files still carry an identical set of 191 keys, and a
  test fails if one diverges. `SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS=1` lifts the adapter blacklist so a
  VM guest lists interfaces at all, which is what makes the DNS-writing paths testable.

### Still open before `1.0.0`
- **The proxy's configuration surface is only partly ours.** `example-dnscrypt-proxy.toml` from 2.1.18
  documents settings we never model, and the notable omissions are the ones that define the 2.1 line:
  `pqdnscrypt` (post-quantum), `odoh_servers`, `enable_hot_reload`, `bootstrap_resolvers`, plus whole
  sections with no representation at all — `[schedules]`, `[monitoring_ui]`, `[ip_encryption]`,
  `[local_doh]`, `[captive_portals]` — and the `allowed_*` / `blocked_ips` half of the blacklist.
  Because `DnscryptProxyConfigurationManager` deserialises to a typed object and writes that object
  back, a key set by hand is deleted on the next save, comments included. Either the round-trip has to
  preserve what it does not understand, or the keys above have to become settings; until one of those
  is true, "bundles dnscrypt-proxy 2.1.18" must not be read as "exposes dnscrypt-proxy 2.1.18".
- A person has to look at the tray icon menu and the three drag-drop surfaces. Both libraries took a
  major bump, the app runs elevated, and UIPI stops a non-elevated automation from driving it.
- Announcing the fork where the users are (`DNSCrypt/SimpleDnsCrypt#554`/`#581`,
  `instantsc/SimpleDnsCrypt#27`). Drafted, deliberately not sent.
- The update channel's dry run against a real published release, which needs the first tag.

## [0.8.2] - 2023-11-17 — instant.sc
- Updated to .NET 8. Updated dependencies and dnscrypt-proxy to 2.1.5. Markup fixes and cleaner
  IP address display.

## [0.7.1] - 2020-04-11 — Christian Hermann
- Last release of the original project. dnscrypt-proxy 2.0.42, fallback-resolver dialog, tray
  mode, window-size memory.

[Unreleased]: https://github.com/xardyx2/SimpleDnsCryptPlus/compare/v0.9.0-rc.2...HEAD
[0.9.0-rc.2]: https://github.com/xardyx2/SimpleDnsCryptPlus/releases/tag/v0.9.0-rc.2
[0.9.0-rc.1]: https://github.com/xardyx2/SimpleDnsCryptPlus/releases/tag/v0.9.0-rc.1
[0.8.2]: https://github.com/instantsc/SimpleDnsCrypt/releases/tag/0.8.2
[0.7.1]: https://github.com/DNSCrypt/SimpleDnsCrypt/releases/tag/0.7.1

# R0 and R2 execution checklist — the manual gate before 0.9.0 GA

Companion to [dns-safety.md](dns-safety.md), which defines the levels and why. This file is the
run-sheet: what to click, what to run, and what to write down. Nothing here needs a code change, and
nothing here can be done by the agent — the app manifest requests `requireAdministrator`, so
UI Access Isolation blocks synthetic input and scripted clicking. Screenshots work; a headless
verifier must say "not run" rather than imply it looked.

Print or paste the recovery block from dns-safety.md into the second elevated window **before**
starting R2. Do not R2 a machine whose work you would mind losing, and do not R2 with uncommitted
changes in this repository.

Record the outcome in the table at the bottom as you go. A blank row is a row that did not happen.

---

## R0 — read-only pass (host is fine)

Prerequisite: `build/build-portable.ps1` output, or the portable zip from the release page, unzipped.
Launch `SimpleDnsCryptPlus.exe` and accept UAC. Do **not** click Install service, Start service, or
apply anything on the Route tab.

- [ ] Main view renders: each section has the same vertical rhythm, the header icon and the switch on
      its right line up on the same row, and the network cards sit in an even gutter with a centred
      last row when the window is maximized. (This is `SectionStyles.xaml` — thirteen hand-tuned
      copies became one style, and `Tests/MainViewStructureTests.cs` only proves the markup, not the
      pixels.)
- [ ] Virtual adapters are **marked, not hidden**: a Hyper-V/WSL vEthernet switch, a Wintun or
      Tailscale VPN, a Bluetooth PAN and a loopback each show a cube icon instead of a cable, and the
      tooltip line "Virtual or tunnel adapter" appears. A physical card must still show the cable or
      antenna icon it had before.
- [ ] The interface list is not empty on a machine that has containers or a VPN installed (compare
      against ticking "show hidden cards").
- [ ] Every tab opens: Main, Resolvers, Advanced settings, Query log, Domain block log, Address block
      log, Domain blacklist, Address blacklist, Cloak & forward, About.
- [ ] **A window appears on launch, in both startup modes.** .NET user settings are XML, under a folder
      whose name is a per-install hash — find it before editing anything:

      ```powershell
      Get-ChildItem "$env:LOCALAPPDATA\SimpleDnsCrypt" -Recurse -Filter user.config
      ```

      On a machine that has run the 0.8.2 generation that is
      `%LOCALAPPDATA%\SimpleDnsCrypt\SimpleDnsCrypt_Url_<hash>\0.8.2.0\user.config`; close the app, set
      `TrayMode` / `StartInTray` there, and relaunch each way (or simply toggle both in the Settings tab,
      which writes the same file). Tray mode must put the tray icon on screen and the main window behind
      it; normal mode must show the main window. The nullable pass added guards around exactly this code
      path in `LoaderViewModel` — they cannot fire while the DI container behaves, which is precisely why
      they need looking at rather than compiling.
- [ ] Tray icon: right-click and left-click. **The tray menu item added in the localization pass is
      now `{lex:Loc Key=tray_menu_show_window}`. It must read "Show Window", not blank.** A blank
      here means a key that does not resolve, and `OutputMissingKeys="False"` renders that as nothing.
- [ ] Resolvers tab: the eight grid column headers are visible ("Enabled", "Name", "Protocol",
      "DnsSec", "NoLog", "NoFilter", "Description" and the two more in the relay/route grids). Same
      blank-label warning as above.
- [ ] With no resolver list loaded (first launch, before sources refresh) the empty state reads
      "No resolvers have been loaded yet".
- [ ] Advanced settings: the section header reads "Proxy Logging" and its label reads
      "Log to dnscrypt-proxy.log (log_level: 0)".
- [ ] Add custom resolver dialog: header "Add custom resolver", field labels "Name" and "DNS stamp".
- [ ] Click "Block" on a query-log row and "Unblock"/"Add to whitelist" on a domain-block-log row. Each
      dialog's message line must read **"Rule:"**, and its buttons "Add"/"Cancel". Before this change
      the message line was a hard-coded literal; it now comes from `dialog_message_rule`.
- [ ] Drag-drop ordering, all three surfaces: resolver list, domain blacklist rules, address blacklist
      rules. Drop an item and confirm the order survives Apply settings and a restart of the app.
- [ ] Language switch: pick German (or any non-English entry) and confirm the interface translates
      *and* that the strings listed above still show English text rather than nothing. New keys live
      only in the neutral `Translation.resx` and reach every culture through invariant fallback; a
      blank is a bug, English is not.
- [ ] Update dialog: with `settings_check_for_updates` on, confirm it reports "up to date" for the
      installed version and never offers a `-rc` build as a stable update.
- [ ] Close the app from the tray, then confirm the process is gone (`Get-Process SimpleDnsCryptPlus`).

Only files written during R0 are `dnscrypt-proxy/dnscrypt-proxy.toml` and
`dnscrypt-proxy/configVersion.txt` inside the app folder. Confirm nothing else changed:

```powershell
Get-DnsClientServerAddress | Format-Table -AutoSize   # must be identical to before you launched
Get-Service dnscrypt-proxy -ErrorAction SilentlyContinue   # must not exist
```

---

## R2 — real service, real DNS rewrite (Windows Sandbox or a checkpointed VM)

Sandbox setup: `SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS=1` in the environment before launching the app, or
the interface list is empty and every test below is meaningless (see dns-safety.md).

Do R2 in **two** elevated PowerShell windows: one you only ever paste recovery commands into, and one
for the probes. Keep both open for the whole run.

### 2.1 Baseline — record it before touching anything

```powershell
$if = Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -First 1
$if | Format-List Name, InterfaceDescription, InterfaceIndex, MacAddress
Get-DnsClientServerAddress | Format-Table -AutoSize
ipconfig /all > pre.txt
```

Write down `$if.Name` (the `InterfaceAlias` you will need in the recovery line). Do not test on the
adapter you would hate to lose; a secondary one is better.

### 2.2 Install and start through the app

Click **Install service**, then **Start service**. Then:

```powershell
Get-Service dnscrypt-proxy | Format-List Name, Status, StartType
Get-NetUDPEndpoint  -LocalPort 53 -OwningProcess (Get-Process dnscrypt-proxy).Id -ErrorAction SilentlyContinue |
    Select-Object LocalAddress, LocalPort
Get-NetTCPConnection -LocalPort 53 -State Listen -OwningProcess (Get-Process dnscrypt-proxy).Id -ErrorAction SilentlyContinue |
    Select-Object LocalAddress, LocalPort
Get-DnsClientServerAddress -InterfaceAlias $if.Name
```

**Pass condition:** the service is `Running`/`Automatic`; the proxy owns `127.0.0.1:53` and
`[::1]:53`; and `Get-DnsClientServerAddress` for that interface now lists `127.0.0.1` (and `::1`)
first. That last line is the whole point of the application — the OS sends lookups to the local proxy.

### 2.3 Proof that no query leaves the box in clear text on UDP/53

The decisive offline test is to **block** outbound UDP 53 and confirm resolution keeps working while
the proxy is up. Still working means the traffic was never on 53 to begin with.

```powershell
$block = New-NetFirewallRule -DisplayName 'R2 block outbound UDP 53' -Direction Outbound `
    -Protocol UDP -RemotePort 53 -Action Block -Enabled True
Resolve-DnsName www.example.com            # must still succeed, and quickly
Get-NetFirewallRule -DisplayName 'R2 block outbound UDP 53' |
    Get-NetFirewallSecurityFilter            # confirm the rule is really in the stack
```

Now watch where the proxy's own traffic goes — the resolver's address, on 443 (or 5353 for plain
DNSCrypt-over-TCP), never on the interface's own UDP 53:

```powershell
$pid2 = (Get-Process dnscrypt-proxy).Id
Get-NetTCPConnection    -OwningProcess $pid2 -State Established -ErrorAction SilentlyContinue |
    Select-Object RemoteAddress, RemotePort
Get-NetUDPEndpoint      -OwningProcess $pid2 -ErrorAction SilentlyContinue |
    Select-Object RemoteAddress, RemotePort
```

**Pass condition:** with the block still active, `Resolve-DnsName` succeeds; the proxy's established
connections are to resolver addresses on `443`/`5353`; and no `RemotePort 53` connection belongs to
the proxy while the block is on (it cannot get one).

Restore before continuing:

```powershell
Remove-NetFirewallRule -DisplayName 'R2 block outbound UDP 53'
```

### 2.4 External leak check (needs internet; do it last, not first)

Local routing proves the GUI did its job. Whether the *upstream resolver* sees your address is a
property of the resolver you picked, so check it once with a real service:

- open `https://dnsleaktest.com` → Extended test → record the ISP/AS lines, and
- open `https://ipleak.net` → note the DNS Address block.

**Pass condition:** every listed DNS server belongs to the resolver's network (check the IP against
`public-resolvers.md` for the resolver you selected), not to your ISP's. Record the AS names in the
table below — a result that names your ISP is a leak, and it is a configuration question
(`forced_tcp`, `fallback_resolvers`, which resolver is chosen), not automatically a GUI bug.

### 2.5 Fallback — stop the service and confirm the machine still resolves

```powershell
Stop-Service dnscrypt-proxy -Force
Resolve-DnsName www.example.com -ErrorAction SilentlyContinue   # with 127.0.0.1 still on the card this FAILS, and that is expected
Get-DnsClientServerAddress -InterfaceAlias $if.Name
```

Then either start it again, or remove the local server and confirm recovery:

```powershell
Set-DnsClientServerAddress -InterfaceAlias $if.Name -ServerAddresses ('8.8.8.8','9.9.9.9')
Clear-DnsClientCache
Resolve-DnsName www.example.com          # must succeed within a second or two
```

**Pass condition:** you understand and observed both states — a stopped proxy with the card still
pointing at `127.0.0.1` means **no** resolution (that is upstream #533 and the reason the recovery
line exists), and resetting the servers restores it. Do not call this a bug; do record how long the
recovery took and whether the "no internet" flap appeared.

### 2.6 Uninstall paths — the two channels are different, test both

Portable zip: **Uninstall service** in the app, then run `Uninstall.exe` from the folder.
MSI: `Apps & features` → Uninstall (or `msiexec /x <path>.msi /qn` for the silent path).

```powershell
Get-Service dnscrypt-proxy -ErrorAction SilentlyContinue     # gone after either uninstall
Get-DnsClientServerAddress -InterfaceAlias $if.Name          # back to the DHCP/offered servers
netsh interface ipv4 show dnsglobals
```

**Pass conditions, and the known difference:** the MSI removes the service *and* restores DNS because
its `ServiceControl` stops and removes the service and its deferred custom action runs
`Uninstall.exe`. The portable `Uninstall.exe` alone only restores DNS — it never touches the service
(see dns-safety.md "What Uninstall.exe does and does not do").

The check that currently **cannot pass** and is the reason Poin 3 of this plan exists: before
uninstalling, note a secondary adapter that is present but **disconnected** (unplug a cable, or disable
a card) and had been given static DNS by the app. After `Uninstall.exe`, that adapter still holds its
static servers, because the tool only enumerates operational interfaces. Record it:

```powershell
Get-NetAdapter | Select-Object Name, InterfaceDescription, Status
Get-DnsClientServerAddress | Where-Object ServerAddresses
```

### 2.7 MSI install/upgrade surface

```powershell
msiexec /i SimpleDNSCryptPlus-x64-<version>.msi /qn
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' |
    Where-Object DisplayName -like '*Simple DNSCrypt*' |
    Select-Object DisplayName, DisplayVersion, UpgradeCode, InstallLocation
```

**Pass conditions:** it installs unattended; the Apps & features entry reads "Simple DNSCrypt Plus"
with the release version and **not** instant.sc's `UpgradeCode`; the app starts from the installed
location; installing a *lower* version over it is refused (no `AllowDowngrades`); and 2.6's uninstall
then leaves no service and no static DNS behind.

---

## Result table

Copy this out and fill it in. A row marked `not run` is fine and honest; a row marked pass without
the command output behind it is not.

| # | Check | Command / action | Result | Evidence (paste or file) |
|---|---|---|---|---|
| R0-1 | Section rhythm and alignment | launch, Main tab | | |
| R0-2 | Virtual adapters marked, physical unchanged | launch, Main tab | | |
| R0-3 | Ten tabs open | click each | | |
| R0-4 | Tray menu "Show Window" not blank | tray right-click | | |
| R0-5 | Grid headers not blank | Resolvers tab | | |
| R0-6 | "Proxy Logging" + description | Advanced settings | | |
| R0-7 | "Rule:" in both dialogs | log tabs | | |
| R0-8 | Drag-drop order survives Apply | 3 surfaces | | |
| R0-9 | Language switch keeps new strings visible | Settings | | |
| R0-10 | No DNS/service side effects | 2.1 commands | | |
| R2-1 | Baseline captured | 2.1 | | |
| R2-2 | Service + 53 listen + card DNS = 127.0.0.1 | 2.2 | | |
| R2-3 | UDP/53 egress blocked, resolution works | 2.3 | | |
| R2-4 | Proxy peers on 443/5353 only | 2.3 | | |
| R2-5 | External leak test AS names | 2.4 | | |
| R2-6 | Stopped-proxy behaviour + recovery time | 2.5 | | |
| R2-7 | Portable uninstall restores DNS | 2.6 | | |
| R2-8 | MSI uninstall removes service **and** DNS | 2.6 | | |
| R2-9 | Disconnected adapter left behind (**expected fail**) | 2.6 | | |
| R2-10 | Silent MSI install, identity, version | 2.7 | | |

When the R2 rows are filled, the deliberate caveat in the release notes — "no human has installed or
uninstalled this MSI on a real machine yet" — can be removed. Until then it stays.

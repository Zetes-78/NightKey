# NightKey

Free two-step sign-in codes for Windows, by [Night Tech Solutions](https://nighttech.solutions/).

**[Download NightKey for Windows](https://github.com/Zetes-78/NightKey/releases/latest/download/NightKey-Setup.exe)** · Requires Windows 10 (version 2004 or newer) or Windows 11, 64-bit. Questions? [nighttech.solutions](https://nighttech.solutions/) or call 864-381-8510.

NightKey shows the 6-digit codes sites ask for when two-step verification is on, the same codes Google Authenticator or Microsoft Authenticator show on a phone. Each account's countdown is drawn as a moon phase: full when a new code appears, waning toward a new moon as it runs out, and ember-colored in the last five seconds.

## Features

- Scan setup QR codes straight off the screen (all monitors), from a copied image, or from an image file
- Paste a setup link or type the secret key by hand
- Import Google Authenticator's "Transfer accounts" QR codes
- Password-protected backup and restore (`.nkbackup`) for moving to a new PC
- Optional Windows Hello lock (PIN, fingerprint or face), which re-locks when you lock the PC
- Copied codes stay out of clipboard history and cloud sync, and clear after 30 seconds
- NightKey's windows are hidden from screenshots, screen recordings and screen sharing
- Search: type a name and press Enter to copy that code

## Build the installer

On a Windows PC with the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Inno Setup 6.3+ (`winget install JRSoftware.InnoSetup`):

```powershell
.\build.ps1
```

That runs the core tests, publishes a self-contained `NightKey.exe` (no .NET install needed on customers' PCs), and builds `dist\NightKey-Setup-<version>.exe` plus a `.sha256.txt` checksum.

The installer installs per-user without an admin prompt, adds a Start menu shortcut and an uninstaller in Apps & features, asks people to close NightKey before upgrading, and never deletes saved accounts on uninstall unless the person says yes.

To develop, open `NightKey.sln` in Visual Studio 2022 or run `dotnet run --project src\NightKey`.

## Releasing

1. Bump `<Version>` in `src/NightKey/NightKey.csproj`.
2. Run `.\build.ps1 -SignTool '<your signtool command>'` (see "Code signing").
3. Upload `NightKey-Setup-<version>.exe` and its checksum. GitHub Releases is a good free host; link the site's download button to it.
4. Test the download on a clean PC or Windows Sandbox before announcing it.

Never change `AppId` in `installer/NightKey.iss` after the first public release; Windows uses it to match upgrades.

GitHub builds both packages on every push (Actions > Build NightKey); publishing a GitHub release attaches the
installer as `NightKey-Setup.exe`, which the website's download button always points to.

## Microsoft Store

`.\build-store.ps1` makes `dist\NightKey-<version>.msix` for the Microsoft Store (needs the Windows SDK). The Store
signs it, so it needs no certificate. See [store/STORE-LISTING.md](store/STORE-LISTING.md) for the Partner Center steps,
the listing text and how to take screenshots.

## Code signing

Unsigned downloads trigger SmartScreen's "Windows protected your PC" warning, which stops most people. Sign releases with a code-signing certificate, for example Azure Trusted Signing. `build.ps1 -SignTool` takes the full `signtool sign ...` command, appends each file path, and signs `NightKey.exe`, the installer and the uninstaller. New certificates still build SmartScreen reputation over the first downloads, so early users may see a milder prompt.

## How secrets are protected

- **On disk:** accounts live in `%AppData%\Night Tech Solutions\NightKey\vault.dat`, encrypted with Windows DPAPI for the signed-in Windows user. Only that user on that PC can decrypt it. The previous version is kept as `vault.dat.bak`.
- **Backups:** AES-256-GCM, with a key derived from the backup password using PBKDF2-SHA256 (600,000 rounds). Tampering is detected.
- **Windows Hello:** this is a gate in front of the app, not a separate encryption layer. Anyone with full access to the same Windows account can still reach DPAPI-protected data. Hello is skipped when Windows reports it unavailable, for example over Remote Desktop.
- **Same-device caveat:** codes on the PC you sign in from are convenient, but a stolen or compromised PC holds both your password and your codes. For the most important accounts, a phone authenticator or hardware key is stronger.
- Nothing is sent over the network. NightKey has no accounts, telemetry or ads.

## Project layout

```
src/NightKey/Core        Portable logic: TOTP, Base32, otpauth parsing, migration import, backups
src/NightKey/Services    Windows: vault (DPAPI), Windows Hello, QR scanning, clipboard, window hardening
src/NightKey/ViewModels  Account rows and the moon-phase geometry
tests/NightKey.CoreTests RFC 6238 / RFC 4226 test vectors, parser, migration and backup tests (any OS)
installer/               Inno Setup script and installer artwork
```

## Roadmap ideas

- Auto-update (Velopack or an update check against nighttech.solutions)
- Microsoft Store listing (Store signing removes SmartScreen friction)
- Hardware-bound vault key through Windows Hello key credentials
- Tray icon and global hotkey

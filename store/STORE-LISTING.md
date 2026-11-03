# NightKey in the Microsoft Store

Everything needed to submit NightKey in Partner Center (https://partner.microsoft.com/dashboard). Registration is free.

## 1. One-time setup

1. Sign up as a developer (Individual, or Company to show "Night Tech Solutions" as a verified business; Company needs
   business verification, easiest with a free D-U-N-S number).
2. **Apps and games > New product > MSIX or PWA app**, and reserve the name **NightKey** (if it's taken, try
   "NightKey Authenticator").
3. Open **Product management > Product identity** and copy three values into the GitHub repository
   (Settings > Secrets and variables > Actions > Variables):
   - `STORE_IDENTITY_NAME` = Package/Identity/Name (looks like `12345NightTechSolutions.NightKey`)
   - `STORE_PUBLISHER` = Package/Identity/Publisher (looks like `CN=1A2B3C4D-...`)
   - `STORE_PUBLISHER_NAME` = Package/Properties/PublisherDisplayName
4. Run the build again (Actions > Build NightKey > Run workflow). Download the **NightKey-Microsoft-Store** artifact:
   that `.msix` is the file to upload.

## 2. Submission

**Pricing and availability:** Free. All markets.

**Properties**
- Category: Security (subcategory: none) or Productivity > Utilities & tools
- Privacy policy URL: https://nighttech.solutions/privacy/#apps
- Website: https://nighttech.solutions/security-apps/
- Support contact: https://nighttech.solutions/contact/
- System requirements: none beyond Windows 10 2004 or later

**Age ratings:** answer the questionnaire: no user-generated content, no sharing of location, no purchases, no
communication between users. Expected rating: 3+ / Everyone.

**Packages:** upload `NightKey-<version>.msix`.

**Store listing (English, United States)**

*Description*

> NightKey shows the 6-digit codes that sites ask for when two-step verification is on, the same codes a phone
> authenticator app shows, right on your Windows PC.
>
> Scan a site's setup QR code straight off your screen, then copy codes with one click. Each account's countdown is
> drawn as a moon: full when a new code appears, waning as it runs out.
>
> Private by design: your secret keys are encrypted on your PC with Windows' own protection and never leave it.
> NightKey has no accounts, no ads and no tracking, and it hides itself from screenshots and screen sharing.
>
> Made by Night Tech Solutions, a family-owned computer repair and IT support business in the Upstate of South Carolina.

*What's new in this version:* Night Tech links and a new About window.

*Product features* (one per line)
- Works with any site that offers an authenticator app (Google, Microsoft, Amazon, banks and more)
- Scan setup QR codes off the screen, from a copied image or from a file
- Import from Google Authenticator's "Transfer accounts" QR codes
- Password-protected backup and restore for moving to a new PC
- Optional Windows Hello lock (PIN, fingerprint or face)
- Copied codes stay out of clipboard history and clear after 30 seconds
- Hidden from screenshots, screen recordings and screen sharing
- No accounts, ads or tracking

*Short description:* Free two-step sign-in codes for Windows. Encrypted on your PC, never in the cloud.

*Search terms:* authenticator, 2FA, two-factor, TOTP, MFA, verification code, one-time password

*Screenshots* (at least one, 1366x768 or larger): NightKey hides itself from screenshots, so start it in screenshot
mode with made-up accounts only:

1. Win+R, then: `"%LOCALAPPDATA%\Programs\NightKey\NightKey.exe" --screenshot-mode` (installer version) or
   `explorer.exe shell:AppsFolder\` and find NightKey (Store version, run from a shortcut with `--screenshot-mode`).
2. Add two or three fake accounts by typing a made-up key, for example `JBSWY3DPEHPK3PXP`, named "Example Bank",
   "Email", "Work".
3. Take screenshots (Win+Shift+S), then remove the fake accounts and close NightKey. Never take screenshots of real accounts.

**Submission options > Notes for certification**

> NightKey is a WPF desktop authenticator (TOTP, RFC 6238). It needs runFullTrust because it's a Win32 desktop app.
> No sign-in is required: choose Add account and type the test key JBSWY3DPEHPK3PXP to see a code.

**Restricted capability justification (runFullTrust):** NightKey is a .NET WPF desktop application packaged with MSIX.

## 3. Notes

- The Store signs the package, so no code-signing certificate is needed, and Store downloads don't get SmartScreen
  warnings.
- The Store and website versions keep accounts separately (Windows gives Store apps their own storage). To move,
  use NightKey's Back up accounts and Restore from backup.
- Bump `<Version>` in `src/NightKey/NightKey.csproj` for every Store update; the package version is that plus `.0`.

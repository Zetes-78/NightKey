using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using NightKey.Core;
using NightKey.Services;

namespace NightKey;

public partial class AddAccountWindow : Window
{
    public List<Account> Accounts { get; } = new();

    public AddAccountWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ScanButton.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.Harden(this); // also keeps this window out of our own screen scan
    }

    private async void ScanScreen_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Looking for a QR code on your screens…");
        try
        {
            var texts = await Task.Run(() => QrScanner.ScanAllScreens());
            HandleCodes(texts, "on your screens");
        }
        catch (Exception ex)
        {
            ShowError("NightKey couldn't read the screen: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                if (OtpAuthUri.IsSetupUri(text) || MigrationImport.IsMigrationUri(text))
                {
                    HandleCodes(new[] { text }, "in what you pasted");
                }
                else
                {
                    SecretBox.Text = text;
                    ShowInfo("Pasted the secret key. Add the service name, then choose Add account.");
                    IssuerBox.Focus();
                }
            }
            else if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            {
                var (pixels, w, h) = QrScanner.ToPixels(image);
                SetBusy(true, "Reading the copied image…");
                var texts = await Task.Run(() => QrScanner.Decode(pixels, w, h));
                HandleCodes(texts, "in the copied image");
            }
            else
            {
                ShowError("The clipboard is empty. Copy a QR code image, a setup link or a secret key, then choose Paste.");
            }
        }
        catch (Exception ex)
        {
            ShowError("NightKey couldn't read the clipboard: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog
        {
            Title = "Open a QR code image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
        };
        if (open.ShowDialog(this) != true) return;

        SetBusy(true, "Reading the image…");
        try
        {
            string path = open.FileName;
            var texts = await Task.Run(() => QrScanner.DecodeFile(path));
            HandleCodes(texts, "in that image");
        }
        catch (Exception ex)
        {
            ShowError("NightKey couldn't open that image: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void HandleCodes(IReadOnlyList<string> texts, string where)
    {
        var found = new List<Account>();
        var errors = new List<string>();
        int skipped = 0;

        foreach (string text in texts)
        {
            try
            {
                if (MigrationImport.IsMigrationUri(text))
                {
                    var result = MigrationImport.Parse(text);
                    found.AddRange(result.Accounts);
                    skipped += result.Skipped;
                }
                else if (OtpAuthUri.IsSetupUri(text))
                {
                    found.Add(OtpAuthUri.Parse(text));
                }
            }
            catch (NotSupportedException) { skipped++; }
            catch (FormatException ex) { errors.Add(ex.Message); }
        }

        string skippedNote = skipped switch
        {
            0 => "",
            1 => " One counter-based account was skipped because NightKey doesn't support that type.",
            _ => $" {skipped} counter-based accounts were skipped because NightKey doesn't support that type.",
        };

        if (found.Count == 0)
        {
            if (errors.Count > 0) ShowError(errors[0]);
            else if (skipped > 0) ShowError(skippedNote.Trim());
            else if (texts.Count == 0) ShowError($"No QR code found {where}. Make sure the whole code is visible and not covered by another window.");
            else ShowError($"Found a QR code {where}, but it isn't for two-step sign-in.");
            return;
        }

        if (found.Count == 1)
        {
            Fill(found[0]);
            ShowInfo($"Found {found[0].DisplayTitle}. Check the details, then choose Add account.{skippedNote}");
            return;
        }

        var answer = MessageBox.Show(this, $"Found {found.Count} accounts.{skippedNote}\n\nAdd them all?",
            "Add accounts", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            Accounts.AddRange(found);
            DialogResult = true;
        }
    }

    private void Fill(Account a)
    {
        IssuerBox.Text = a.Issuer;
        NameBox.Text = a.Name;
        SecretBox.Text = a.Secret;
        Sha1Radio.IsChecked = a.Algorithm == OtpAlgorithm.Sha1;
        Sha256Radio.IsChecked = a.Algorithm == OtpAlgorithm.Sha256;
        Sha512Radio.IsChecked = a.Algorithm == OtpAlgorithm.Sha512;
        DigitsBox.Text = a.Digits.ToString();
        PeriodBox.Text = a.Period.ToString();
        AdvancedExpander.IsExpanded = a.Algorithm != OtpAlgorithm.Sha1 || a.Digits != 6 || a.Period != 30;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var account = new Account
            {
                Issuer = IssuerBox.Text.Trim(),
                Name = NameBox.Text.Trim(),
                Secret = Base32.Normalize(SecretBox.Text),
                Algorithm = Sha256Radio.IsChecked == true ? OtpAlgorithm.Sha256
                          : Sha512Radio.IsChecked == true ? OtpAlgorithm.Sha512
                          : OtpAlgorithm.Sha1,
                Digits = int.TryParse(DigitsBox.Text.Trim(), out var d) ? d
                    : throw new FormatException("Digits must be a number: 6, 7 or 8."),
                Period = int.TryParse(PeriodBox.Text.Trim(), out var p) ? p
                    : throw new FormatException("The period must be a whole number of seconds."),
            };
            if (account.Issuer.Length == 0 && account.Name.Length == 0)
                throw new FormatException("Give the account a name, like the site it's for, so you can tell it apart.");
            account.Validate();

            Accounts.Add(account);
            DialogResult = true;
        }
        catch (FormatException ex)
        {
            ShowError(ex.Message);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        ScanButton.IsEnabled = PasteButton.IsEnabled = OpenButton.IsEnabled = !busy;
        if (busy && message is not null) ShowInfo(message);
    }

    private void ShowInfo(string message)
    {
        MessageText.Foreground = (System.Windows.Media.Brush)FindResource("TealBrush");
        MessageText.Text = message;
        MessageText.Visibility = Visibility.Visible;
    }

    private void ShowError(string message)
    {
        MessageText.Foreground = (System.Windows.Media.Brush)FindResource("EmberBrush");
        MessageText.Text = message;
        MessageText.Visibility = Visibility.Visible;
    }
}

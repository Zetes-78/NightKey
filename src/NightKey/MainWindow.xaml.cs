using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using NightKey.Core;
using NightKey.Services;
using NightKey.ViewModels;

namespace NightKey;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<AccountRow> _rows = new();
    private readonly ICollectionView _view;
    private readonly DispatcherTimer _ticker;
    private readonly DispatcherTimer _statusTimer;
    private readonly AppSettings _settings;
    private bool _vaultReadOnly;
    private bool _locked;
    private bool _unlocking;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        HelloMenuItem.IsChecked = _settings.RequireHello;
        if (Links.StoreBuild) UpdatesMenuItem.Visibility = Visibility.Collapsed;   // the Store keeps it up to date

        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.SortDescriptions.Add(new SortDescription(nameof(AccountRow.Title), ListSortDirection.Ascending));
        _view.Filter = FilterRow;
        AccountsList.ItemsSource = _view;

        LoadVault();

        _ticker = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(200) };
        _ticker.Tick += (_, _) => TickAll();
        _ticker.Start();
        TickAll();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); StatusBar.Visibility = Visibility.Collapsed; };

        SystemEvents.SessionSwitch += OnSessionSwitch;
        Closed += (_, _) =>
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            ClipboardGuard.ClearNow();
        };
        Loaded += OnLoaded;
        UpdateEmptyState();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.Harden(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_settings.IsNew)
        {
            // Turn on Windows Hello by default, but only where it's set up.
            _settings.RequireHello = await HelloGate.IsAvailableAsync();
            TrySaveSettings();
            HelloMenuItem.IsChecked = _settings.RequireHello;
        }

        if (_settings.RequireHello)
        {
            Lock();
            await TryUnlockAsync();
        }
        else
        {
            SearchBox.Focus();
        }
    }

    // ---------- Vault ----------

    private void LoadVault()
    {
        try
        {
            foreach (var account in Vault.Load()) _rows.Add(new AccountRow(account));
        }
        catch (Exception ex)
        {
            _vaultReadOnly = true;
            MessageBox.Show(
                "NightKey couldn't open your saved accounts, so it won't save changes this session. That keeps the vault file from being overwritten.\n\n" +
                "This happens when the vault was copied from another PC or Windows account. Use Restore from backup to bring your accounts back.\n\n" +
                "Details: " + ex.Message,
                "NightKey", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveVault()
    {
        if (_vaultReadOnly)
        {
            ShowStatus("Changes aren't being saved. Restore from a backup to repair your saved accounts.");
            return;
        }
        try
        {
            Vault.Save(_rows.Select(r => r.Account));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "NightKey couldn't save your accounts: " + ex.Message, "NightKey",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TrySaveSettings()
    {
        try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ---------- Codes ----------

    private void TickAll()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var row in _rows) row.Tick(now);
    }

    private bool FilterRow(object item)
    {
        string query = SearchBox?.Text?.Trim() ?? "";
        if (query.Length == 0) return true;
        var row = (AccountRow)item;
        return row.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void CopyCode(AccountRow row)
    {
        if (_locked) return;
        ClipboardGuard.Copy(row.Code);
        ShowStatus($"Copied the {row.Title} code. The clipboard clears in 30 seconds.");
    }

    private static AccountRow? RowFrom(object sender) => (sender as FrameworkElement)?.DataContext as AccountRow;

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is { } row) CopyCode(row);
    }

    private void CopyMenu_Click(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is { } row) CopyCode(row);
    }

    private void RenameMenu_Click(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is not { } row) return;
        var dialog = new RenameDialog(row.Account.Issuer, row.Account.Name) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        row.Account.Issuer = dialog.Issuer;
        row.Account.Name = dialog.AccountName;
        row.NotifyLabels();
        _view.Refresh();
        SaveVault();
        ShowStatus($"Renamed to {row.Title}.");
    }

    private void RemoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is not { } row) return;
        var answer = MessageBox.Show(this,
            $"Remove {row.Title} from NightKey?\n\nTurn off two-step sign-in on that site first, or you could be locked out of it.",
            "Remove account", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        _rows.Remove(row);
        SaveVault();
        UpdateEmptyState();
        ShowStatus($"Removed {row.Title}.");
    }

    // ---------- Adding ----------

    private void AddButton_Click(object sender, RoutedEventArgs e) => AddAccounts();

    private void AddAccounts()
    {
        if (_locked) return;
        var dialog = new AddAccountWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var (added, duplicates, lastTitle) = Merge(dialog.Accounts);
        SaveVault();
        _view.Refresh();
        TickAll();
        UpdateEmptyState();

        string message = added switch
        {
            0 => "That account is already in NightKey.",
            1 => $"Added {lastTitle}.",
            _ => $"Added {added} accounts.",
        };
        if (added > 0 && duplicates > 0) message += $" Skipped {duplicates} already in NightKey.";
        ShowStatus(message);
    }

    private (int Added, int Duplicates, string LastTitle) Merge(IEnumerable<Account> incoming)
    {
        int added = 0, duplicates = 0;
        string lastTitle = "";
        foreach (var account in incoming)
        {
            if (_rows.Any(r => r.Account.SameSecretAs(account))) { duplicates++; continue; }
            _rows.Add(new AccountRow(account));
            lastTitle = account.DisplayTitle;
            added++;
        }
        return (added, duplicates, lastTitle);
    }

    // ---------- Backup ----------

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (_locked) return;
        if (_rows.Count == 0) { ShowStatus("There's nothing to back up yet."); return; }

        var pw = new PasswordDialog("Back up your accounts",
            "Choose a password for this backup file. You'll need it to restore, and NightKey can't recover it if it's lost.",
            confirm: true) { Owner = this };
        if (pw.ShowDialog() != true) return;

        var save = new SaveFileDialog
        {
            Title = "Save NightKey backup",
            FileName = $"NightKey backup {DateTime.Now:yyyy-MM-dd}.nkbackup",
            Filter = "NightKey backup (*.nkbackup)|*.nkbackup",
            DefaultExt = ".nkbackup",
        };
        if (save.ShowDialog(this) != true) return;

        var accounts = _rows.Select(r => r.Account).ToList();
        string password = pw.Password;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            string text = await Task.Run(() => Backup.Export(accounts, password));
            await File.WriteAllTextAsync(save.FileName, text);
            ShowStatus($"Backed up {accounts.Count} accounts. Keep the file somewhere other than this PC, like a USB drive.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The backup wasn't saved: " + ex.Message, "NightKey", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_locked) return;
        var open = new OpenFileDialog
        {
            Title = "Restore NightKey backup",
            Filter = "NightKey backup (*.nkbackup)|*.nkbackup|All files (*.*)|*.*",
        };
        if (open.ShowDialog(this) != true) return;

        string text;
        try { text = await File.ReadAllTextAsync(open.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "NightKey couldn't read that file: " + ex.Message, "NightKey", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var pw = new PasswordDialog("Restore from backup", "Enter the password you chose when you made this backup.", confirm: false) { Owner = this };
        if (pw.ShowDialog() != true) return;
        string password = pw.Password;

        List<Account> accounts;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            accounts = await Task.Run(() => Backup.Import(text, password));
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(this, ex.Message, "NightKey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        var (added, duplicates, _) = Merge(accounts);
        _vaultReadOnly = false; // a successful restore replaces an unreadable vault; the old file is kept as vault.dat.bak
        SaveVault();
        _view.Refresh();
        TickAll();
        UpdateEmptyState();
        ShowStatus(duplicates > 0
            ? $"Restored {added} accounts. {duplicates} were already in NightKey."
            : $"Restored {added} accounts.");
    }

    // ---------- Lock ----------

    private async void HelloMenu_Click(object sender, RoutedEventArgs e)
    {
        bool want = HelloMenuItem.IsChecked;
        if (want && !await HelloGate.IsAvailableAsync())
        {
            HelloMenuItem.IsChecked = false;
            MessageBox.Show(this,
                "Windows Hello isn't set up on this PC. Set up a PIN, fingerprint or face sign-in in Settings > Accounts > Sign-in options, then turn this on.",
                "NightKey", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _settings.RequireHello = want;
        TrySaveSettings();
        ShowStatus(want
            ? "NightKey will ask for Windows Hello when it opens and after you lock your PC."
            : "Windows Hello is off. Anyone signed in to this Windows account can open NightKey.");
    }

    private void LockMenu_Click(object sender, RoutedEventArgs e) => Lock();

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock && _settings.RequireHello)
            Dispatcher.BeginInvoke(new Action(Lock));
    }

    private void Lock()
    {
        _locked = true;
        ClipboardGuard.ClearNow();
        MainContent.Visibility = Visibility.Hidden;
        LockOverlay.Visibility = Visibility.Visible;
        LockMessage.Text = "Your codes stay hidden until Windows confirms it's you.";
        UnlockButton.Focus();
    }

    private void Unlock()
    {
        _locked = false;
        LockOverlay.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;
        SearchBox.Focus();
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e) => await TryUnlockAsync();

    private async Task TryUnlockAsync()
    {
        if (_unlocking) return;
        _unlocking = true;
        UnlockButton.IsEnabled = false;
        LockMessage.Text = "Waiting for Windows Hello…";
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            switch (await HelloGate.VerifyAsync(hwnd, "Unlock NightKey to see your codes"))
            {
                case HelloResult.Verified:
                    Unlock();
                    break;
                case HelloResult.Unavailable:
                    Unlock();
                    ShowStatus("Windows Hello isn't available right now (it doesn't work over Remote Desktop), so NightKey opened without it.");
                    break;
                default:
                    LockMessage.Text = "Not verified. Choose Unlock to try again.";
                    break;
            }
        }
        finally
        {
            _unlocking = false;
            UnlockButton.IsEnabled = true;
        }
    }

    // ---------- Search, keys, misc ----------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => _view.Refresh();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _view.Cast<AccountRow>().FirstOrDefault() is { } first)
        {
            CopyCode(first);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_locked || Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.N: AddAccounts(); e.Handled = true; break;
            case Key.F: SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; break;
            case Key.L: Lock(); e.Handled = true; break;
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = MenuButton.ContextMenu;
        menu.PlacementTarget = MenuButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void Website_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Website);
    private void Help_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Help);
    private void FreeApps_Click(object sender, RoutedEventArgs e) => Links.Open(Links.FreeApps);
    private void Phone_Click(object sender, RoutedEventArgs e) => Links.Open(Links.PhoneLink);
    private void Updates_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Releases);

    private void UpdateEmptyState()
    {
        bool empty = _rows.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        AccountsScroller.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        SearchBox.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusBar.Visibility = Visibility.Visible;
        _statusTimer.Stop();
        _statusTimer.Start();
    }
}

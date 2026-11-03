using System;
using System.Windows;
using NightKey.Services;

namespace NightKey;

public partial class PasswordDialog : Window
{
    private readonly bool _confirm;

    public string Password { get; private set; } = "";

    public PasswordDialog(string heading, string message, bool confirm)
    {
        InitializeComponent();
        _confirm = confirm;
        Title = heading;
        HeadingText.Text = heading;
        MessageText.Text = message;
        OkButton.Content = confirm ? "Save backup" : "Restore";
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => PasswordBox1.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.Harden(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string pw = PasswordBox1.Password;
        string? error = null;
        if (pw.Length == 0) error = "Enter the backup password.";
        else if (_confirm && pw.Length < 10) error = "Use at least 10 characters. A short phrase of a few words works well.";
        else if (_confirm && pw != PasswordBox2.Password) error = "The passwords don't match.";

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Password = pw;
        DialogResult = true;
    }
}

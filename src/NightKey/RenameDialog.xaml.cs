using System;
using System.Windows;
using NightKey.Services;

namespace NightKey;

public partial class RenameDialog : Window
{
    public string Issuer => IssuerBox.Text.Trim();
    public string AccountName => NameBox.Text.Trim();

    public RenameDialog(string issuer, string name)
    {
        InitializeComponent();
        IssuerBox.Text = issuer;
        NameBox.Text = name;
        Loaded += (_, _) => { IssuerBox.Focus(); IssuerBox.SelectAll(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.Harden(this);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Issuer.Length == 0 && AccountName.Length == 0)
        {
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}

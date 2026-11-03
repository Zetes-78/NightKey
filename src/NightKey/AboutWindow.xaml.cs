using System;
using System.Reflection;
using System.Windows;
using NightKey.Services;

namespace NightKey;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"Version {version}{(Links.StoreBuild ? " (Microsoft Store)" : "")} · by {Links.Company}";
        PhoneRun.Text = Links.Phone;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.Harden(this);
    }

    private void Website_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Website);
    private void Phone_Click(object sender, RoutedEventArgs e) => Links.Open(Links.PhoneLink);
    private void Help_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Help);
    private void FreeApps_Click(object sender, RoutedEventArgs e) => Links.Open(Links.FreeApps);
    private void Privacy_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Privacy);
    private void Source_Click(object sender, RoutedEventArgs e) => Links.Open(Links.Source);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

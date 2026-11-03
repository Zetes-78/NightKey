using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace NightKey.Services;

/// <summary>
/// Copies a code, keeps it out of Windows clipboard history and cloud sync,
/// and clears it after 30 seconds if it's still there.
/// </summary>
public static class ClipboardGuard
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private static readonly DispatcherTimer Timer = CreateTimer();
    private static string? _lastCopied;

    private static DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer { Interval = Lifetime };
        t.Tick += (_, _) => { t.Stop(); ClearNow(); };
        return t;
    }

    public static void Copy(string text)
    {
        var data = new DataObject();
        data.SetText(text);
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
        Retry(() => Clipboard.SetDataObject(data, true));

        _lastCopied = text;
        Timer.Stop();
        Timer.Start();
    }

    public static void ClearNow()
    {
        if (_lastCopied is null) return;
        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText() == _lastCopied)
                Retry(Clipboard.Clear);
        }
        catch (COMException) { }
        catch (ExternalException) { }
        _lastCopied = null;
    }

    // Another app can briefly hold the clipboard open; try a few times.
    private static void Retry(Action action)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { action(); return; }
            catch (COMException) when (attempt < 4) { Thread.Sleep(60); }
        }
    }
}

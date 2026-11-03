using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

namespace NightKey.Services;

/// <summary>Finds QR codes on screen, in image files, or in a copied image.</summary>
public static class QrScanner
{
    /// <summary>Captures every monitor. NightKey's own windows are excluded from capture, so they never cover a code.</summary>
    public static IReadOnlyList<string> ScanAllScreens()
    {
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return Decode(bitmap);
    }

    public static IReadOnlyList<string> DecodeFile(string path)
    {
        using var loaded = new Bitmap(path);
        using var bitmap = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White); // transparent PNGs: put the code on white
            g.DrawImage(loaded, 0, 0, loaded.Width, loaded.Height);
        }
        return Decode(bitmap);
    }

    /// <summary>Converts a WPF image (e.g. from the clipboard) to opaque BGRA pixels. Call on the UI thread.</summary>
    public static (byte[] Pixels, int Width, int Height) ToPixels(System.Windows.Media.Imaging.BitmapSource source)
    {
        var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
            source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight;
        var px = new byte[w * h * 4];
        converted.CopyPixels(px, w * 4, 0);

        // Clipboard images often arrive with alpha = 0 everywhere; treat those as opaque.
        bool anyAlpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { anyAlpha = true; break; }
        if (anyAlpha)
        {
            for (int i = 0; i < px.Length; i += 4)
            {
                int a = px[i + 3];
                if (a == 255) continue;
                for (int c = 0; c < 3; c++) px[i + c] = (byte)((px[i + c] * a + 255 * (255 - a)) / 255);
                px[i + 3] = 255;
            }
        }
        return (px, w, h);
    }

    public static IReadOnlyList<string> Decode(byte[] bgra, int width, int height)
    {
        var source = new RGBLuminanceSource(bgra, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            },
        };

        var results = reader.DecodeMultiple(source);
        if (results is { Length: > 0 })
            return results.Select(r => r.Text).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();

        var single = reader.Decode(source);
        return single is null ? Array.Empty<string>() : new[] { single.Text };
    }

    private static IReadOnlyList<string> Decode(Bitmap bitmap)
    {
        int w = bitmap.Width, h = bitmap.Height;
        var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new byte[w * h * 4];
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), px, y * w * 4, w * 4);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return Decode(px, w, h);
    }
}

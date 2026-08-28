namespace AIQuotaBar.App.Tray;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using AIQuotaBar.App.Health;

public static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateIcon(QuotaHealthLevel healthLevel)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Dark circular background
            using var bgBrush = new SolidBrush(Color.FromArgb(24, 24, 27)); // #18181B
            using var borderPen = new Pen(Color.FromArgb(63, 63, 70), 1.5f); // #3F3F46
            
            var rect = new Rectangle(1, 1, 29, 29);
            g.FillEllipse(bgBrush, rect);
            g.DrawEllipse(borderPen, rect);

            // Prominent quota health accent bars
            var accentColor = QuotaHealthHelper.GetDrawingColor(healthLevel);
            using var barBrush = new SolidBrush(accentColor);

            g.FillRoundedRectangle(barBrush, new Rectangle(6, 8, 19, 3), new Size(1, 1));
            g.FillRoundedRectangle(barBrush, new Rectangle(6, 14, 15, 3), new Size(1, 1));
            g.FillRoundedRectangle(barBrush, new Rectangle(6, 20, 11, 3), new Size(1, 1));
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}

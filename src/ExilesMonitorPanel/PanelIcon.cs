using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ExilesMonitorPanel
{
    // The 48x48 icon EDDiscovery shows in the panel selector; drawn at run time so the DLL needs no resources.
    internal static class PanelIcon
    {
        public static Image Create()
        {
            var bmp = new Bitmap(48, 48);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                using (var fill = new SolidBrush(Color.FromArgb(0xF0, 0x7B, 0x05)))
                    g.FillEllipse(fill, 2, 2, 44, 44);
                using (var font = new Font("Segoe UI", 21f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var ink = new SolidBrush(Color.FromArgb(0x14, 0x14, 0x14)))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("EX", font, ink, new RectangleF(0, 1, 48, 48), fmt);
            }
            return bmp;
        }
    }
}

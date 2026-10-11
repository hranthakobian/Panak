using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Panak.Services {
    public static class IconGenerator {
        public static void GenerateFolderIcon(string folderPath, string shape = "Squircle", string hexColor = ShellHelper.DefaultPastelColorHex, double opacity = 1.0) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                // Ensure folder has ReadOnly attribute immediately
                try {
                    var initialAttrs = File.GetAttributes(folderPath);
                    if (!initialAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(folderPath, initialAttrs | FileAttributes.ReadOnly);
                    }
                } catch { }

                // Gather up to first 4 non-hidden user items
                var directoryInfo = new DirectoryInfo(folderPath);
                var items = directoryInfo.GetFileSystemInfos()
                    .Where(f => !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.Equals("panak.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                                !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .OrderBy(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                    .ThenBy(f => f.Name)
                    .Take(4)
                    .ToList();

                List<Bitmap> miniIcons = new List<Bitmap>();
                foreach (var item in items) {
                    Bitmap? bmp = ShellHelper.GetFileIconBitmap(item.FullName, isLarge: true);
                    if (bmp != null) {
                        miniIcons.Add(bmp);
                    }
                }

                Color baseColor = ColorTranslator.FromHtml(hexColor);

                // If default opacity passed, check if panak.json specifies custom BackgroundOpacity
                if (Math.Abs(opacity - 1.0) < 0.001) {
                    try {
                        string panakJson = Path.Combine(folderPath, "panak.json");
                        if (File.Exists(panakJson)) {
                            var data = Models.PanakFolderData.Load(folderPath);
                            if (data.BackgroundOpacity > 0 && data.BackgroundOpacity <= 1.0) {
                                opacity = data.BackgroundOpacity;
                            }
                        }
                    } catch { }
                }

                // Render at 256x256, 48x48, 32x32, 16x16
                using Bitmap bmp256 = RenderPlateWithGrid(256, shape, baseColor, miniIcons, opacity);
                using Bitmap bmp48 = new Bitmap(bmp256, new Size(48, 48));
                using Bitmap bmp32 = new Bitmap(bmp256, new Size(32, 32));
                using Bitmap bmp16 = new Bitmap(bmp256, new Size(16, 16));

                // Dynamic sequential icon rotation: changes the icon filename on every generation
                // (e.g. panak_icon_0.ico, panak_icon_1.ico, panak_icon_2.ico ...) to completely bypass
                // Windows Explorer's persistent in-memory icon cache
                string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
                int currentSeq = -1;
                if (File.Exists(desktopIniPath)) {
                    try {
                        string existingIni = File.ReadAllText(desktopIniPath);
                        var match = System.Text.RegularExpressions.Regex.Match(existingIni, @"panak_icon_(\d+)\.ico", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed)) {
                            currentSeq = parsed;
                        }
                    } catch { }
                }

                if (currentSeq < 0) {
                    try {
                        foreach (var f in directoryInfo.GetFiles("panak_icon*.ico")) {
                            var m = System.Text.RegularExpressions.Regex.Match(f.Name, @"panak_icon_(\d+)\.ico", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (m.Success && int.TryParse(m.Groups[1].Value, out int val) && val > currentSeq) {
                                currentSeq = val;
                            }
                        }
                    } catch { }
                }

                int nextSeq = currentSeq < 0 ? 0 : ((currentSeq + 1) % 10000);
                string targetSlot = $"panak_icon_{nextSeq}.ico";
                string targetIcoPath = Path.Combine(folderPath, targetSlot);

                // Save multi-resolution ICO bytes directly to target slot without stripping attributes
                byte[] icoBytes = BuildMultiResIcoBytes(new[] { bmp256, bmp48, bmp32, bmp16 });
                WriteProtectedSystemFile(targetIcoPath, icoBytes);

                // Prepare desktop.ini with relative path to remain stable if folder is renamed or moved
                // Include UTF-16LE Byte Order Mark (BOM) so Windows Explorer and standard tools decode it accurately
                string iniContent = $"[.ShellClassInfo]\r\nIconResource={targetSlot},0\r\nIconFile={targetSlot}\r\nIconIndex=0\r\n[ViewState]\r\nMode=\r\nVid=\r\nFolderType=Generic\r\n";
                byte[] preamble = System.Text.Encoding.Unicode.GetPreamble();
                byte[] contentBytes = System.Text.Encoding.Unicode.GetBytes(iniContent);
                byte[] iniBytes = new byte[preamble.Length + contentBytes.Length];
                Buffer.BlockCopy(preamble, 0, iniBytes, 0, preamble.Length);
                Buffer.BlockCopy(contentBytes, 0, iniBytes, preamble.Length, contentBytes.Length);
                WriteProtectedSystemFile(desktopIniPath, iniBytes);

                // Enforce ReadOnly on folder so Explorer processes desktop.ini
                try {
                    var folderAttrs = File.GetAttributes(folderPath);
                    if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);
                    }
                } catch { }

                // Clean up extracted bitmaps
                foreach (var m in miniIcons) {
                    m.Dispose();
                }

                // Clean up any older panak_icon_*.ico (other than the newly generated slot) and leftover .tmp files
                try {
                    foreach (var oldIco in directoryInfo.GetFiles("panak_icon*.ico")) {
                        if (!oldIco.Name.Equals(targetSlot, StringComparison.OrdinalIgnoreCase)) {
                            try {
                                File.SetAttributes(oldIco.FullName, FileAttributes.Normal);
                                oldIco.Delete();
                            } catch { }
                        }
                    }
                    foreach (var tmp in directoryInfo.GetFiles("*.tmp")) {
                        if (tmp.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) ||
                            tmp.Name.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)) {
                            try {
                                File.SetAttributes(tmp.FullName, FileAttributes.Normal);
                                tmp.Delete();
                            } catch { }
                        }
                    }
                } catch { }

                // Notify Windows Explorer for this folder, its icon file, and parent directory
                ShellHelper.NotifyFolderIconChanged(folderPath, targetIcoPath);
            } catch {
                // Ignore transient icon generation errors
            }
        }

        public static void WriteProtectedSystemFile(string filePath, byte[] content) {
            for (int attempt = 0; attempt < 5; attempt++) {
                try {
                    if (File.Exists(filePath)) {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                        fs.SetLength(0);
                        fs.Write(content, 0, content.Length);
                        fs.Flush();
                    } else {
                        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        fs.Write(content, 0, content.Length);
                        fs.Flush();
                    }

                    try {
                        var attrs = File.GetAttributes(filePath);
                        if (!attrs.HasFlag(FileAttributes.Hidden) || !attrs.HasFlag(FileAttributes.System)) {
                            File.SetAttributes(filePath, attrs | FileAttributes.Hidden | FileAttributes.System);
                        }
                    } catch { }
                    return;
                } catch (IOException) {
                    System.Threading.Thread.Sleep(50);
                } catch (UnauthorizedAccessException) {
                    try {
                        var attrs = File.GetAttributes(filePath);
                        File.SetAttributes(filePath, (attrs & ~FileAttributes.ReadOnly) | FileAttributes.Hidden | FileAttributes.System);
                    } catch { }
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public static Bitmap RenderPlateWithGrid(int size, string shape, Color baseColor, List<Bitmap> miniIcons, double opacity = 1.0) {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float padding = size * 0.06f;
                float plateSize = size - (padding * 2);
                RectangleF plateRect = new RectangleF(padding, padding, plateSize, plateSize);

                using GraphicsPath path = CreateShapePath(plateRect, shape);

                opacity = Math.Clamp(opacity, 0.20, 1.0);
                int alpha = (int)Math.Round(opacity * 255.0);
                Color plateColor = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);

                // Draw drop shadow
                RectangleF shadowRect = new RectangleF(padding, padding + size * 0.03f, plateSize, plateSize);
                using (GraphicsPath shadowPath = CreateShapePath(shadowRect, shape)) {
                    using (PathGradientBrush shadowBrush = new PathGradientBrush(shadowPath)) {
                        shadowBrush.CenterColor = Color.FromArgb(70, 0, 0, 0);
                        shadowBrush.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };

                        if (alpha < 255) {
                            // Exclude plate interior from shadow so dark shadow does NOT muddy the translucent plate
                            g.SetClip(path, CombineMode.Exclude);
                            g.FillPath(shadowBrush, shadowPath);
                            g.ResetClip();
                        } else {
                            g.FillPath(shadowBrush, shadowPath);
                        }
                    }
                }

                // Fill plate with opacity (solid or translucent)
                using (SolidBrush bgBrush = new SolidBrush(plateColor)) {
                    g.FillPath(bgBrush, path);
                }

                // Inner content area calculation for mini icons (optimized padding for larger legible icons)
                float innerPadding = plateSize * 0.09f;
                RectangleF innerRect = new RectangleF(padding + innerPadding, padding + innerPadding, plateSize - (innerPadding * 2), plateSize - (innerPadding * 2));

                // Draw mini 2x2 icons or empty folder badge
                if (miniIcons == null || miniIcons.Count == 0) {
                    // Draw modern Android folder silhouette with smooth rounded corners
                    float iconW = innerRect.Width * 0.52f;
                    float iconH = innerRect.Height * 0.42f;
                    float iconX = innerRect.X + (innerRect.Width - iconW) / 2f;
                    float iconY = innerRect.Y + (innerRect.Height - iconH) / 2f;
                    float r = iconW * 0.12f;

                    float luminance = (0.299f * baseColor.R + 0.587f * baseColor.G + 0.114f * baseColor.B) / 255f;
                    Color tabColor;
                    Color bodyColor;
                    if (luminance > 0.6f) {
                        // Light pastel background: use darker tone of baseColor (Material You On-Container)
                        int darkR = (int)(baseColor.R * 0.28f);
                        int darkG = (int)(baseColor.G * 0.28f);
                        int darkB = (int)(baseColor.B * 0.28f);
                        tabColor = Color.FromArgb(190, darkR, darkG, darkB);
                        bodyColor = Color.FromArgb(230, darkR, darkG, darkB);
                    } else {
                        // Dark background: use white silhouette
                        tabColor = Color.FromArgb(210, 255, 255, 255);
                        bodyColor = Color.FromArgb(240, 255, 255, 255);
                    }

                    // Tab
                    RectangleF tabRect = new RectangleF(iconX, iconY, iconW * 0.45f, iconH * 0.45f);
                    using (GraphicsPath tabPath = new GraphicsPath()) {
                        AddRoundedRectangle(tabPath, tabRect, r);
                        using (SolidBrush tabBrush = new SolidBrush(tabColor)) {
                            g.FillPath(tabBrush, tabPath);
                        }
                    }

                    // Body
                    RectangleF bodyRect = new RectangleF(iconX, iconY + iconH * 0.22f, iconW, iconH * 0.78f);
                    using (GraphicsPath bodyPath = new GraphicsPath()) {
                        AddRoundedRectangle(bodyPath, bodyRect, r);
                        using (SolidBrush bodyBrush = new SolidBrush(bodyColor)) {
                            g.FillPath(bodyBrush, bodyPath);
                        }
                    }
                } else {
                    var (iconDim, centers) = CalculateIconLayout(innerRect, miniIcons.Count);
                    int drawCount = Math.Min(miniIcons.Count, centers.Length);
                    for (int i = 0; i < drawCount; i++) {
                        PointF c = centers[i];
                        RectangleF dst = new RectangleF(c.X - iconDim / 2f, c.Y - iconDim / 2f, iconDim, iconDim);
                        g.DrawImage(miniIcons[i], dst);
                    }
                }
            }
            return bmp;
        }

        public static (float IconDim, PointF[] Centers) CalculateIconLayout(RectangleF innerRect, int count) {
            float w = innerRect.Width;
            float h = innerRect.Height;

            if (count == 1) {
                float iconDim = Math.Min(w, h) * 0.58f + 5f;
                PointF[] centers = new PointF[] {
                    new PointF(innerRect.X + w / 2f, innerRect.Y + h / 2f)
                };
                return (iconDim, centers);
            }

            if (count == 2) {
                // 2 items: larger icons (0.48), centered horizontally and vertically
                float iconDim = Math.Min(w, h) * 0.48f + 5f;
                float gap = w * 0.05f;
                float totalW = (2 * iconDim) + gap;
                float startX = innerRect.X + (w - totalW) / 2f;
                float cy = innerRect.Y + h / 2f;

                PointF[] centers = new PointF[] {
                    new PointF(startX + iconDim / 2f, cy),
                    new PointF(startX + iconDim + gap + iconDim / 2f, cy)
                };
                return (iconDim, centers);
            }

            if (count == 3) {
                // 3 items: slightly smaller than 2-item case (0.43), triangular layout with apex pointing up
                float iconDim = Math.Min(w, h) * 0.43f + 5f;
                float rowGap = h * 0.04f;
                float colGap = w * 0.06f;

                float totalH = (2 * iconDim) + rowGap;
                float startY = innerRect.Y + (h - totalH) / 2f;
                float cyTop = startY + iconDim / 2f;
                float cyBottom = startY + iconDim + rowGap + iconDim / 2f;

                // Apex at top centered horizontally
                float cxTop = innerRect.X + w / 2f;

                // Base at bottom centered horizontally
                float bottomW = (2 * iconDim) + colGap;
                float startXBottom = innerRect.X + (w - bottomW) / 2f;
                float cxBottomLeft = startXBottom + iconDim / 2f;
                float cxBottomRight = startXBottom + iconDim + colGap + iconDim / 2f;

                PointF[] centers = new PointF[] {
                    new PointF(cxTop, cyTop),
                    new PointF(cxBottomLeft, cyBottom),
                    new PointF(cxBottomRight, cyBottom)
                };
                return (iconDim, centers);
            }

            // 4 or more items: standard 2x2 grid (enlarged for improved visibility)
            float cellW = w / 2f;
            float cellH = h / 2f;
            float iconDim4 = Math.Min(cellW, cellH) * 0.85f + 5f;

            PointF[] centers4 = new PointF[] {
                new PointF(innerRect.X + cellW * 0.5f, innerRect.Y + cellH * 0.5f),
                new PointF(innerRect.X + cellW * 1.5f, innerRect.Y + cellH * 0.5f),
                new PointF(innerRect.X + cellW * 0.5f, innerRect.Y + cellH * 1.5f),
                new PointF(innerRect.X + cellW * 1.5f, innerRect.Y + cellH * 1.5f)
            };
            return (iconDim4, centers4);
        }

        private static GraphicsPath CreateShapePath(RectangleF rect, string shape) {
            GraphicsPath path = new GraphicsPath();
            switch (shape.ToLowerInvariant()) {
                case "circle": {
                    path.AddEllipse(rect);
                    break;
                }
                case "squircle": {
                    float r = rect.Width * 0.28f;
                    AddRoundedRectangle(path, rect, r);
                    break;
                }
                case "clover": {
                    // 4-lobed clover with concave inward dips on top, bottom, left, and right
                    float dip = rect.Width * 0.06f;
                    float r = rect.Width * 0.26f;
                    float x0 = rect.Left, x1 = rect.Right;
                    float y0 = rect.Top, y1 = rect.Bottom;
                    float cx = rect.Left + rect.Width / 2f;
                    float cy = rect.Top + rect.Height / 2f;

                    // Top-Left corner arc
                    path.AddArc(x0, y0, r * 2, r * 2, 180, 90);
                    // Top edge dip
                    path.AddBezier(x0 + r, y0, cx - r * 0.5f, y0 + dip, cx + r * 0.5f, y0 + dip, x1 - r, y0);
                    // Top-Right corner arc
                    path.AddArc(x1 - r * 2, y0, r * 2, r * 2, 270, 90);
                    // Right edge dip
                    path.AddBezier(x1, y0 + r, x1 - dip, cy - r * 0.5f, x1 - dip, cy + r * 0.5f, x1, y1 - r);
                    // Bottom-Right corner arc
                    path.AddArc(x1 - r * 2, y1 - r * 2, r * 2, r * 2, 0, 90);
                    // Bottom edge dip
                    path.AddBezier(x1 - r, y1, cx + r * 0.5f, y1 - dip, cx - r * 0.5f, y1 - dip, x0 + r, y1);
                    // Bottom-Left corner arc
                    path.AddArc(x0, y1 - r * 2, r * 2, r * 2, 90, 90);
                    // Left edge dip
                    path.AddBezier(x0, y1 - r, x0 + dip, cy + r * 0.5f, x0 + dip, cy - r * 0.5f, x0, y0 + r);
                    path.CloseFigure();
                    break;
                }
                case "scallop": {
                    // 12-lobed flower/starburst wavy circle
                    float cx = rect.X + rect.Width / 2f;
                    float cy = rect.Y + rect.Height / 2f;
                    float w = rect.Width;
                    float h = rect.Height;

                    int nPoints = 120;
                    PointF[] pts = new PointF[nPoints];
                    for (int i = 0; i < nPoints; i++) {
                        double angle = i * 2 * Math.PI / nPoints;
                        double rVal = 0.49 * (1.0 + 0.035 * Math.Cos(12.0 * angle));
                        float px = cx + (float)(rVal * w * Math.Cos(angle));
                        float py = cy + (float)(rVal * h * Math.Sin(angle));
                        pts[i] = new PointF(px, py);
                    }
                    path.AddClosedCurve(pts, 0.4f);
                    break;
                }
                case "arch": {
                    // Semi-circular arch top, vertical sides, and rounded bottom corners
                    float rTop = rect.Width / 2f;
                    float rBottom = rect.Width * 0.16f;

                    path.AddArc(rect.X, rect.Y, rect.Width, rTop * 2, 180, 180);
                    path.AddLine(rect.Right, rect.Y + rTop, rect.Right, rect.Bottom - rBottom);
                    path.AddArc(rect.Right - rBottom * 2, rect.Bottom - rBottom * 2, rBottom * 2, rBottom * 2, 0, 90);
                    path.AddLine(rect.Right - rBottom, rect.Bottom, rect.X + rBottom, rect.Bottom);
                    path.AddArc(rect.X, rect.Bottom - rBottom * 2, rBottom * 2, rBottom * 2, 90, 90);
                    path.AddLine(rect.X, rect.Bottom - rBottom, rect.X, rect.Y + rTop);
                    path.CloseFigure();
                    break;
                }
                case "roundedrect": {
                    float r = rect.Width * 0.22f;
                    AddRoundedRectangle(path, rect, r);
                    break;
                }
                case "teardrop": {
                    float r = rect.Width * 0.35f;
                    path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
                    path.AddLine(rect.X + r, rect.Y, rect.Right, rect.Y);
                    path.AddLine(rect.Right, rect.Y, rect.Right, rect.Bottom - r);
                    path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                    path.AddLine(rect.Right - r, rect.Bottom, rect.X + r, rect.Bottom);
                    path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    break;
                }
                default: {
                    float r = rect.Width * 0.28f;
                    AddRoundedRectangle(path, rect, r);
                    break;
                }
            }
            return path;
        }

        private static void AddRoundedRectangle(GraphicsPath path, RectangleF rect, float radius) {
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
        }

        public static byte[] BuildMultiResIcoBytes(Bitmap[] images) {
            using MemoryStream msIco = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(msIco);

            // ICO Header: 2 bytes reserved (0), 2 bytes type (1=icon), 2 bytes image count
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)images.Length);

            List<byte[]> frameBuffers = new List<byte[]>();
            foreach (var img in images) {
                if (img.Width >= 256) {
                    using MemoryStream ms = new MemoryStream();
                    img.Save(ms, ImageFormat.Png);
                    frameBuffers.Add(ms.ToArray());
                } else {
                    frameBuffers.Add(CreateDibIconFrame(img));
                }
            }

            int offset = 6 + (16 * images.Length);

            // Directory entries
            for (int i = 0; i < images.Length; i++) {
                Bitmap img = images[i];
                byte[] data = frameBuffers[i];

                bw.Write((byte)(img.Width >= 256 ? 0 : img.Width));
                bw.Write((byte)(img.Height >= 256 ? 0 : img.Height));
                bw.Write((byte)0); // Colors
                bw.Write((byte)0); // Reserved
                bw.Write((ushort)1); // Color planes
                bw.Write((ushort)32); // Bits per pixel
                bw.Write((uint)data.Length); // Image size in bytes
                bw.Write((uint)offset); // Offset of image data

                offset += data.Length;
            }

            // Write image data buffers
            foreach (var data in frameBuffers) {
                bw.Write(data);
            }
            bw.Flush();
            return msIco.ToArray();
        }

        private static byte[] CreateDibIconFrame(Bitmap img) {
            int width = img.Width;
            int height = img.Height;
            int maskRowWidth = (width + 31) / 32 * 4;
            int maskSize = maskRowWidth * height;
            int pixelDataSize = width * height * 4;
            int dibSize = 40 + pixelDataSize + maskSize;

            byte[] dib = new byte[dibSize];
            using (MemoryStream ms = new MemoryStream(dib))
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                // BITMAPINFOHEADER
                bw.Write((uint)40); // biSize
                bw.Write(width); // biWidth
                bw.Write(height * 2); // biHeight (doubled for XOR + AND mask)
                bw.Write((ushort)1); // biPlanes
                bw.Write((ushort)32); // biBitCount
                bw.Write((uint)0); // biCompression = BI_RGB
                bw.Write((uint)(pixelDataSize + maskSize)); // biSizeImage
                bw.Write(0); // biXPelsPerMeter
                bw.Write(0); // biYPelsPerMeter
                bw.Write((uint)0); // biClrUsed
                bw.Write((uint)0); // biClrImportant

                // Pixels (bottom-up BGRA)
                BitmapData data = img.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try {
                    byte[] row = new byte[width * 4];
                    for (int y = height - 1; y >= 0; y--) {
                        IntPtr rowPtr = IntPtr.Add(data.Scan0, y * data.Stride);
                        Marshal.Copy(rowPtr, row, 0, row.Length);
                        bw.Write(row);
                    }
                } finally {
                    img.UnlockBits(data);
                }

                // AND mask (all zeros for 32bpp ARGB because alpha channel defines transparency)
                byte[] mask = new byte[maskSize];
                bw.Write(mask);
            }
            return dib;
        }
    }
}

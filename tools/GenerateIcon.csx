// tools/GenerateIcon.csx
// 用 dotnet-script 运行: dotnet script tools/GenerateIcon.csx
// 或直接用 csc / .NET 8 编译后运行
// 生成 src/VoiceSync/tray.ico（16/32/48 三个分辨率）

#r "nuget: System.Drawing.Common, 8.0.0"

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

static Bitmap DrawFrame(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);

    float s = size;

    // ── 背景圆（深蓝）──────────────────────────────
    using var bgBrush = new SolidBrush(Color.FromArgb(255, 30, 80, 180));
    g.FillEllipse(bgBrush, 0, 0, s - 1, s - 1);

    // ── 麦克风主体（白色圆角矩形）──────────────────
    float micW = s * 0.28f, micH = s * 0.38f;
    float micX = s * 0.5f - micW / 2, micY = s * 0.12f;
    float r = micW * 0.45f;
    using var micBrush = new SolidBrush(Color.White);
    using var micPath = new GraphicsPath();
    micPath.AddRoundedRectangle(new RectangleF(micX, micY, micW, micH), r);
    g.FillPath(micBrush, micPath);

    // ── 麦克风支架弧线 ─────────────────────────────
    using var pen = new Pen(Color.White, s * 0.07f) { EndCap = LineCap.Round, StartCap = LineCap.Round };
    float arcX = s * 0.25f, arcY = s * 0.28f;
    float arcW = s * 0.50f, arcH = s * 0.30f;
    g.DrawArc(pen, arcX, arcY, arcW, arcH, 0, 180);

    // 竖线
    float lineX = s * 0.5f;
    g.DrawLine(pen, lineX, arcY + arcH, lineX, s * 0.72f);
    // 横线底座
    g.DrawLine(pen, lineX - s * 0.15f, s * 0.72f, lineX + s * 0.15f, s * 0.72f);

    // ── 右侧无线信号波（3段弧，由小到大）─────────────
    float cx = s * 0.72f, cy = s * 0.42f;
    using var wPen1 = new Pen(Color.FromArgb(220, 255, 255, 255), s * 0.055f) { EndCap = LineCap.Round, StartCap = LineCap.Round };
    using var wPen2 = new Pen(Color.FromArgb(160, 255, 255, 255), s * 0.055f) { EndCap = LineCap.Round, StartCap = LineCap.Round };
    using var wPen3 = new Pen(Color.FromArgb(90,  255, 255, 255), s * 0.055f) { EndCap = LineCap.Round, StartCap = LineCap.Round };

    float r1 = s * 0.07f, r2 = s * 0.13f, r3 = s * 0.19f;
    // 弧度: -60° ~ +60°（右侧朝外）
    g.DrawArc(wPen1, cx - r1, cy - r1, r1 * 2, r1 * 2, -60, 120);
    g.DrawArc(wPen2, cx - r2, cy - r2, r2 * 2, r2 * 2, -60, 120);
    g.DrawArc(wPen3, cx - r3, cy - r3, r3 * 2, r3 * 2, -60, 120);

    return bmp;
}

// GraphicsPath 扩展：圆角矩形
static class GpExt
{
    public static void AddRoundedRectangle(this GraphicsPath path, RectangleF rect, float radius)
    {
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
    }
}

// ── 写出多分辨率 .ico ──────────────────────────────
static void WriteIco(string path, int[] sizes)
{
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);

    // ICO header
    bw.Write((ushort)0);      // reserved
    bw.Write((ushort)1);      // type: ICO
    bw.Write((ushort)sizes.Length);

    // 每帧的 PNG 数据
    var pngs = new byte[sizes.Length][];
    for (int i = 0; i < sizes.Length; i++)
    {
        using var bmp = DrawFrame(sizes[i]);
        using var pngMs = new MemoryStream();
        bmp.Save(pngMs, ImageFormat.Png);
        pngs[i] = pngMs.ToArray();
    }

    // Directory entries (16 bytes each)
    int offset = 6 + sizes.Length * 16;
    for (int i = 0; i < sizes.Length; i++)
    {
        int sz = sizes[i];
        bw.Write((byte)(sz >= 256 ? 0 : sz));   // width
        bw.Write((byte)(sz >= 256 ? 0 : sz));   // height
        bw.Write((byte)0);   // color count
        bw.Write((byte)0);   // reserved
        bw.Write((ushort)1); // color planes
        bw.Write((ushort)32); // bits per pixel
        bw.Write((uint)pngs[i].Length);
        bw.Write((uint)offset);
        offset += pngs[i].Length;
    }

    // PNG payloads
    foreach (var png in pngs)
        bw.Write(png);

    File.WriteAllBytes(path, ms.ToArray());
    Console.WriteLine($"Written: {path}  ({ms.Length} bytes)");
}

var outPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "src", "VoiceSync", "tray.ico");
outPath = Path.GetFullPath(outPath);
WriteIco(outPath, [16, 32, 48]);

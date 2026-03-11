// tools/GenerateIcon/Program.cs
// Run: dotnet run --project tools/GenerateIcon
// Outputs: src/VoiceSync/tray.ico (16/32/48 px, voice+wifi theme)

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var outPath  = Path.Combine(repoRoot, "src", "VoiceSync", "tray.ico");

WriteIco(outPath, [16, 32, 48]);
Console.WriteLine($"Generated: {outPath}");

// ── Draw one frame ──────────────────────────────────────────────────────────
static Bitmap DrawFrame(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode      = SmoothingMode.AntiAlias;
    g.CompositingQuality = CompositingQuality.HighQuality;
    g.Clear(Color.Transparent);

    float s = size;

    // Background circle — deep blue
    using (var bg = new SolidBrush(Color.FromArgb(255, 28, 78, 188)))
        g.FillEllipse(bg, 1, 1, s - 2, s - 2);

    // Microphone body — white rounded rect
    float mw = s * 0.26f, mh = s * 0.36f;
    float mx = s * 0.38f, my = s * 0.11f;
    float rad = mw * 0.48f;
    using (var wb = new SolidBrush(Color.White))
    using (var mp = RoundedRect(mx, my, mw, mh, rad))
        g.FillPath(wb, mp);

    // Microphone stand arc + vertical line + base
    float pw = Math.Max(1f, s * 0.07f);
    using var pen = new Pen(Color.White, pw) { EndCap = LineCap.Round, StartCap = LineCap.Round };
    float ax = s * 0.22f, ay = s * 0.26f, aw = s * 0.46f, ah = s * 0.28f;
    g.DrawArc(pen, ax, ay, aw, ah, 0, 180);
    float lx = s * 0.50f;
    g.DrawLine(pen, lx, ay + ah, lx, s * 0.74f);
    g.DrawLine(pen, lx - s * 0.14f, s * 0.74f, lx + s * 0.14f, s * 0.74f);

    // Wifi arcs on the right — 3 layers, fading opacity
    float wcx = s * 0.74f, wcy = s * 0.42f;
    float arcW = Math.Max(1f, s * 0.055f);
    int[] alphas = [220, 155, 80];
    float[] radii = [s * 0.07f, s * 0.13f, s * 0.20f];
    for (int i = 0; i < 3; i++)
    {
        using var wp = new Pen(Color.FromArgb(alphas[i], 255, 255, 255), arcW)
            { EndCap = LineCap.Round, StartCap = LineCap.Round };
        float r = radii[i];
        g.DrawArc(wp, wcx - r, wcy - r, r * 2, r * 2, -55, 110);
    }

    return bmp;
}

static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
{
    float d = r * 2;
    var p = new GraphicsPath();
    p.AddArc(x,         y,         d, d, 180, 90);
    p.AddArc(x + w - d, y,         d, d, 270, 90);
    p.AddArc(x + w - d, y + h - d, d, d,   0, 90);
    p.AddArc(x,         y + h - d, d, d,  90, 90);
    p.CloseFigure();
    return p;
}

// ── Write multi-resolution .ico using PNG payloads ─────────────────────────
static void WriteIco(string path, int[] sizes)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    var pngs = sizes.Select(sz =>
    {
        using var bmp = DrawFrame(sz);
        using var ms2 = new MemoryStream();
        bmp.Save(ms2, ImageFormat.Png);
        return ms2.ToArray();
    }).ToArray();

    using var ms  = new MemoryStream();
    using var bw  = new BinaryWriter(ms);

    // ICO header
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)sizes.Length);

    // Directory entries
    int offset = 6 + sizes.Length * 16;
    for (int i = 0; i < sizes.Length; i++)
    {
        int sz = sizes[i];
        bw.Write((byte)(sz >= 256 ? 0 : sz));
        bw.Write((byte)(sz >= 256 ? 0 : sz));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write((uint)pngs[i].Length);
        bw.Write((uint)offset);
        offset += pngs[i].Length;
    }

    foreach (var png in pngs)
        bw.Write(png);

    File.WriteAllBytes(path, ms.ToArray());
}

using System; using System.IO; using Chunha.FX;
static class Program
{
    static int Main(string[] args)
    {
        var dir = args.Length > 0 ? args[0] : "out"; Directory.CreateDirectory(dir);
        int fail = 0;
        foreach (Sfx k in Enum.GetValues(typeof(Sfx))) fail += Check(dir, k.ToString(), SoundKit.DebugSynth(k), 0.03, 4);
        fail += Check(dir, "BGM", SoundKit.DebugBgm(), 25, 40);
        Console.WriteLine(fail == 0 ? "AUDIO OK" : $"AUDIO FAIL {fail}");
        return fail;
    }
    static int Check(string dir, string name, float[] d, double minSec, double maxSec)
    {
        double peak = 0, rms = 0; bool nan = false;
        foreach (var v in d) { if (float.IsNaN(v) || float.IsInfinity(v)) nan = true; peak = Math.Max(peak, Math.Abs(v)); rms += v * v; }
        rms = Math.Sqrt(rms / d.Length); double sec = d.Length / 22050.0;
        bool ok = !nan && peak > 0.05 && peak <= 1.0 && sec >= minSec && sec <= maxSec;
        Console.WriteLine($"{(ok ? "✓" : "✗")} {name,-8} {sec,5:F2}s peak {peak:F2} rms {rms:F3}{(nan ? " NaN!" : "")}");
        Wav(Path.Combine(dir, name + ".wav"), d);
        return ok ? 0 : 1;
    }
    static void Wav(string path, float[] d)
    {
        using var w = new BinaryWriter(File.Create(path));
        int n = d.Length; w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVEfmt ".ToCharArray());
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(22050); w.Write(22050 * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data".ToCharArray()); w.Write(n * 2);
        foreach (var v in d) w.Write((short)Math.Max(-32767, Math.Min(32767, v * 32767)));
    }
}

// PigmentCore 를 Unity 없이 검증한다 (Unity Assets 에 넣지 않는다).
//   mcs -out:run.exe ../PigmentCore/*.cs RunTests.cs && mono run.exe fixtures/
// fixtures/ 는 make_fixtures.py 로 만든다. 파이썬 기준 출력과 비교해 PASS/FAIL 을 찍는다.
using System;
using System.Collections.Generic;
using System.IO;
using Pigment;

static class RunTests
{
    static int fails = 0;
    static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + "  " + detail);
        if (!ok) fails++;
    }

    static double Num(string json, string key)
    {
        int k = json.IndexOf("\"" + key + "\"");
        if (k < 0) throw new Exception("missing " + key);
        int c = json.IndexOf(':', k) + 1;
        int e = c;
        while (e < json.Length && "-+.0123456789eE ".IndexOf(json[e]) >= 0) e++;
        return double.Parse(json.Substring(c, e - c).Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "fixtures";
        string exp = File.ReadAllText(Path.Combine(dir, "expected.json"));
        int frames = (int)Num(exp, "frames"), pixels = (int)Num(exp, "pixels");

        // ---- LoopFinder
        byte[] raw = File.ReadAllBytes(Path.Combine(dir, "gray.f32"));
        var gray = new List<float[]>();
        for (int f = 0; f < frames; f++)
        {
            var g = new float[pixels];
            Buffer.BlockCopy(raw, f * pixels * 4, g, 0, pixels * 4);
            gray.Add(g);
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float[] d = LoopFinder.DiffMatrix(gray);
        LoopResult r = LoopFinder.Find(d, frames, 30f, 1f, 3f, 8);
        sw.Stop();
        int es = (int)Num(exp, "start"), ee = (int)Num(exp, "end");
        double escore = Num(exp, "score");
        Check("loop range", r.Start == es && r.End == ee, string.Format("C# ({0},{1})  py ({2},{3})", r.Start, r.End, es, ee));
        Check("loop score", Math.Abs(r.Score - escore) < 1e-3, string.Format("C# {0:F4}  py {1:F4}  ({2} ms)", r.Score, escore, sw.ElapsedMilliseconds));

        // ---- PeriodicTile
        int n = (int)Num(exp, "n");
        byte[] rgba = File.ReadAllBytes(Path.Combine(dir, "tile.rgba"));
        float before = PeriodicTile.SeamError(rgba, n);
        sw.Restart();
        PeriodicTile.MakePeriodicRGBA(rgba, n);
        sw.Stop();
        float after = PeriodicTile.SeamError(rgba, n);
        long sum = 0; for (int i = 0; i < n * n; i++) sum += rgba[i * 4] + rgba[i * 4 + 1] + rgba[i * 4 + 2];
        long esum = (long)Num(exp, "checksum");
        Check("seam before", Math.Abs(before - Num(exp, "seamBefore")) < 1e-3, string.Format("C# {0:F4}  py {1:F4}", before, Num(exp, "seamBefore")));
        Check("seam after", Math.Abs(after - Num(exp, "seamAfter")) < 0.02, string.Format("C# {0:F4}  py {1:F4}  ({2} ms)", after, Num(exp, "seamAfter"), sw.ElapsedMilliseconds));
        // 반올림 경계에서 ±1 차이가 몇 픽셀 생길 수 있어 합계는 0.01% 안에서 비교
        Check("pixels match", Math.Abs(sum - esum) <= esum * 1e-4, string.Format("C# {0}  py {1}", sum, esum));

        // ---- FlipbookLayout
        int[] picked;
        var lay = FlipbookLayout.For(90, 64, 256, 30f, out picked);
        int px, py;
        lay.PixelRectUnity(0, out px, out py);
        Check("layout", lay.Cols == 8 && lay.Rows == 8 && Math.Abs(lay.Fps - 30f * 64 / 90) < 1e-4 && px == 0 && py == 7 * 256 && picked[63] == 88,
              string.Format("{0}x{1} {2:F2}fps frame0@({3},{4}) last={5}", lay.Cols, lay.Rows, lay.Fps, px, py, picked[63]));

        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails == 0 ? 0 : 1;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MoodPicker.Catch
{
    [Serializable]
    public class PaintLoop
    {
        public string frames;
        public int frameCount;
        public int fps;
        public int cols;
    }

    [Serializable]
    public class PaintRecord
    {
        public int version = 1;
        public string id;
        public int number;
        public string capturedAt;
        public string mode;
        public string label;
        public float gazeSeconds;
        public float intensity;
        public string lightTone;
        public string midTone;
        public string shadowTone;
        public float[] lightDir;
        public float[] lightDirView;
        public int size;
        public string texture;
        public string tile;
        public string build;
        public PaintLoop loop;

        public bool HasLoop => loop != null && loop.frameCount > 0;
    }

    public class PaintDraft
    {
        public CatchMode mode;
        public DateTime capturedAt;
        public float gazeSeconds;
        public CelResult cel;
        public Vector3 lightDirWorld;
        public int size;
        public byte[] stillPng;
        public byte[] tilePng;
        public string build = "";
    }

    public struct SaveResult
    {
        public bool ok;
        public string error;
        public PaintRecord record;
    }

    public interface IPaintFiles
    {
        void WriteAllBytes(string path, byte[] data);
    }

    public class DiskPaintFiles : IPaintFiles
    {
        public void WriteAllBytes(string path, byte[] data) => File.WriteAllBytes(path, data);
    }

    public class PaintStore
    {
        public const string RecordFile = "paint.json";
        public const string StillFile = "still.png";
        public const string TileFile = "tile.png";
        const string StagingPrefix = ".staging-";
        static readonly Regex EmptyLoop = new Regex(@",""loop"":\{[^{}]*\}");

        readonly string _root;
        readonly IPaintFiles _files;

        public string Root => _root;

        public PaintStore(string root, IPaintFiles files = null)
        {
            _root = root;
            _files = files ?? new DiskPaintFiles();
        }

        public static string DefaultRoot => Path.Combine(Application.persistentDataPath, "paints");

        public List<PaintRecord> LoadAll()
        {
            var list = new List<PaintRecord>();
            if (!Directory.Exists(_root)) return list;
            foreach (string dir in Directory.GetDirectories(_root))
            {
                if (Path.GetFileName(dir).StartsWith(StagingPrefix, StringComparison.Ordinal)) continue;
                string p = Path.Combine(dir, RecordFile);
                if (!File.Exists(p)) continue;
                try
                {
                    var r = FromJson(File.ReadAllText(p));
                    if (r != null && !string.IsNullOrEmpty(r.id)) list.Add(r);
                }
                catch (Exception) { }
            }
            return list.OrderBy(r => r.number).ToList();
        }

        public string PathOf(PaintRecord r, string file) => Path.Combine(_root, r.id, file);

        public int NextNumber() => LoadAll().Select(r => r.number).DefaultIfEmpty(0).Max() + 1;

        public SaveResult Save(PaintDraft d)
        {
            if (d.stillPng == null || d.stillPng.Length == 0) return Fail("still image missing");
            if (d.tilePng == null || d.tilePng.Length == 0) return Fail("tile image missing");
            if (d.mode == CatchMode.Loop) return Fail("loop paints are saved by LoopCapture");

            int number = NextNumber();
            string id = $"paint_{d.capturedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}_{number:000}";
            var rec = new PaintRecord
            {
                id = id,
                number = number,
                capturedAt = d.capturedAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                mode = "snap",
                label = "",
                gazeSeconds = d.gazeSeconds,
                intensity = 1f,
                lightTone = CelExtract.Hex(d.cel.Light),
                midTone = CelExtract.Hex(d.cel.Mid),
                shadowTone = CelExtract.Hex(d.cel.Shadow),
                lightDir = Arr(d.lightDirWorld),
                lightDirView = Arr(d.cel.lightDirView),
                size = d.size,
                texture = id + "/" + StillFile,
                tile = id + "/" + TileFile,
                build = d.build,
                loop = null,
            };

            string staging = Path.Combine(_root, StagingPrefix + id);
            string final = Path.Combine(_root, id);
            try
            {
                Directory.CreateDirectory(_root);
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                Directory.CreateDirectory(staging);
                _files.WriteAllBytes(Path.Combine(staging, StillFile), d.stillPng);
                _files.WriteAllBytes(Path.Combine(staging, TileFile), d.tilePng);
                _files.WriteAllBytes(Path.Combine(staging, RecordFile), System.Text.Encoding.UTF8.GetBytes(ToJson(rec)));
                if (Directory.Exists(final)) throw new IOException("paint already exists: " + id);
                Directory.Move(staging, final);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (Exception) { }
                return Fail(e.Message);
            }
            return new SaveResult { ok = true, record = rec };
        }

        public static string ToJson(PaintRecord r)
        {
            string json = JsonUtility.ToJson(r);
            return r.HasLoop ? json : EmptyLoop.Replace(json, "");
        }

        public static PaintRecord FromJson(string json)
        {
            var r = JsonUtility.FromJson<PaintRecord>(json);
            if (r != null && !json.Contains("\"loop\"")) r.loop = null;
            return r;
        }

        static float[] Arr(Vector3 v) => new[] { Round4(v.x), Round4(v.y), Round4(v.z) };
        static float Round4(float f) => (float)Math.Round(f, 4);
        static SaveResult Fail(string why) => new SaveResult { ok = false, error = why };
    }
}

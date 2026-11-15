using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MoodPicker.Ritual
{
    [Serializable]
    public sealed class DayLayer
    {
        public string category;
        public int units;
    }

    [Serializable]
    public sealed class DayRecord
    {
        public string date;
        public string dominant;
        public string sealedAtUtc;
        public bool demo;
        public List<DayLayer> layers = new List<DayLayer>();

        public int TotalUnits
        {
            get
            {
                int t = 0;
                foreach (DayLayer l in layers) t += l.units;
                return t;
            }
        }
    }

    [Serializable]
    sealed class DayFile
    {
        public List<DayRecord> days = new List<DayRecord>();
    }

    public sealed class RitualDayStore
    {
        public const string DateFormat = "yyyy-MM-dd";
        static readonly string[] Ids = { "sleep", "move", "work", "drift", "play" };

        static readonly (string id, int units)[][] Curated =
        {
            new[] { ("sleep", 30), ("move", 11), ("work", 7), ("drift", 4) },
            new[] { ("drift", 22), ("work", 15), ("play", 10), ("move", 6) },
        };

        readonly string _path;
        readonly List<DayRecord> _days = new List<DayRecord>();

        public IReadOnlyList<DayRecord> Days => _days;
        public string Path => _path;

        public RitualDayStore(string path)
        {
            _path = path;
            Load();
        }

        public void Load()
        {
            _days.Clear();
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
            try
            {
                var file = JsonUtility.FromJson<DayFile>(File.ReadAllText(_path));
                if (file != null && file.days != null)
                    foreach (DayRecord d in file.days)
                        if (d != null && !d.demo && !string.IsNullOrEmpty(d.date)) _days.Add(d);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MP] ritual days unreadable: {e.Message}");
            }
        }

        public void Save(DayRecord record)
        {
            if (record == null || record.demo) return;
            _days.RemoveAll(d => d.date == record.date);
            _days.Add(record);
            _days.Sort((a, b) => string.CompareOrdinal(a.date, b.date));
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonUtility.ToJson(new DayFile { days = new List<DayRecord>(_days) }, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MP] ritual days not saved: {e.Message}");
            }
        }

        public static string Key(DateTime day) => day.ToString(DateFormat, CultureInfo.InvariantCulture);

        public List<DayRecord> Before(DateTime today, int count)
        {
            string key = Key(today);
            var list = new List<DayRecord>();
            for (int i = _days.Count - 1; i >= 0 && list.Count < count; i--)
                if (string.CompareOrdinal(_days[i].date, key) < 0) list.Add(_days[i]);
            list.Reverse();
            return list;
        }

        public DayRecord Find(DateTime day)
        {
            string key = Key(day);
            foreach (DayRecord d in _days)
                if (d.date == key) return d;
            return null;
        }

        public static List<DayRecord> Demo(DateTime today, int count)
        {
            var list = new List<DayRecord>();
            for (int k = count; k >= 1; k--)
            {
                DateTime day = today.AddDays(-k);
                int seed = day.Year * 400 + day.DayOfYear;
                var rec = new DayRecord { date = Key(day), demo = true };
                if (k <= Curated.Length)
                {
                    (string id, int units)[] pick = Curated[Curated.Length - k];
                    foreach (var (id, units) in pick) rec.layers.Add(new DayLayer { category = id, units = units });
                    rec.dominant = pick[0].id;
                    list.Add(rec);
                    continue;
                }
                int budget = 60 + (int)(RitualUtil.Hash(seed) * 70f);
                float[] w = new float[Ids.Length];
                float sum = 0f;
                for (int i = 0; i < Ids.Length; i++)
                {
                    float h = RitualUtil.Hash(seed * 7 + i * 13);
                    w[i] = i == 0 ? 0.6f + h : h * h * 1.4f;
                    sum += w[i];
                }
                int best = 0;
                for (int pass = 0; pass < Ids.Length; pass++)
                {
                    int i = (pass + seed) % Ids.Length;
                    int units = Mathf.RoundToInt(budget * w[i] / sum);
                    if (units <= 0) continue;
                    rec.layers.Add(new DayLayer { category = Ids[i], units = units });
                    if (units > best)
                    {
                        best = units;
                        rec.dominant = Ids[i];
                    }
                }
                list.Add(rec);
            }
            return list;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using MoodPicker.Catch;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class PaintStoreTests
    {
        string _root;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "mp-paints-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        static PaintDraft Draft(int seed)
        {
            var rnd = new System.Random(seed);
            byte[] Png() { var b = new byte[64 + seed]; rnd.NextBytes(b); return b; }
            return new PaintDraft
            {
                mode = CatchMode.Still,
                capturedAt = new DateTime(2026, 11, 5, 17, 24, seed % 60, DateTimeKind.Local),
                gazeSeconds = 3f,
                cel = new CelResult { tones = new[] { new Color(0.1f, 0.2f, 0.3f), new Color(0.5f, 0.5f, 0.5f), new Color(0.9f, 0.8f, 0.7f) }, lightDirView = new Vector3(0.1f, 0.7f, -0.7f) },
                lightDirWorld = new Vector3(0.2f, 0.6f, -0.77f),
                size = 480,
                stillPng = Png(),
                tilePng = Png(),
                build = "test",
            };
        }

        class FullDisk : IPaintFiles
        {
            int _left;
            public FullDisk(int okWrites) { _left = okWrites; }
            public void WriteAllBytes(string path, byte[] data)
            {
                if (_left-- <= 0) throw new IOException("No space left on device");
                File.WriteAllBytes(path, data);
            }
        }

        [Test]
        public void Store_SaveLoadRoundTrip()
        {
            var store = new PaintStore(_root);
            var d = Draft(1);
            var res = store.Save(d);
            Assert.That(res.ok, res.error);
            var all = store.LoadAll();
            Assert.That(all.Count, Is.EqualTo(1));
            var r = all[0];
            Assert.That(PaintStore.ToJson(r), Is.EqualTo(PaintStore.ToJson(res.record)));
            Assert.That(r.mode, Is.EqualTo("snap"));
            Assert.That(r.number, Is.EqualTo(1));
            Assert.That(r.lightTone, Is.EqualTo("#E5CCB2"));
            Assert.That(r.HasLoop, Is.False);
            Assert.That(File.ReadAllText(store.PathOf(r, PaintStore.RecordFile)).Contains("\"loop\""), Is.False, "snap 에는 loop 칸이 없다");
            Assert.That(File.ReadAllBytes(store.PathOf(r, PaintStore.StillFile)), Is.EqualTo(d.stillPng));
            Assert.That(File.ReadAllBytes(store.PathOf(r, PaintStore.TileFile)), Is.EqualTo(d.tilePng));
        }

        [Test]
        public void Store_RestartKeepsChips()
        {
            var a = new PaintStore(_root);
            a.Save(Draft(1)); a.Save(Draft(2)); a.Save(Draft(3));
            var before = a.LoadAll().Select(PaintStore.ToJson).ToArray();
            var b = new PaintStore(_root);
            var after = b.LoadAll();
            Assert.That(after.Select(PaintStore.ToJson).ToArray(), Is.EqualTo(before));
            Assert.That(after.Select(r => r.number).ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Store_NumbersAreNotReusedAfterDeleteOfOlder()
        {
            var s = new PaintStore(_root);
            s.Save(Draft(1)); var two = s.Save(Draft(2)).record;
            Directory.Delete(Path.Combine(_root, s.LoadAll()[0].id), true);
            Assert.That(s.Save(Draft(3)).record.number, Is.EqualTo(two.number + 1));
        }

        [Test]
        public void Store_DiskFullFailsAndKeepsExistingChips()
        {
            var ok = new PaintStore(_root);
            ok.Save(Draft(1)); ok.Save(Draft(2));
            var snapshot = ok.LoadAll().Select(r => (PaintStore.ToJson(r), File.ReadAllBytes(ok.PathOf(r, PaintStore.StillFile)))).ToArray();

            foreach (int writes in new[] { 0, 1, 2 })
            {
                var full = new PaintStore(_root, new FullDisk(writes));
                var res = full.Save(Draft(10 + writes));
                Assert.That(res.ok, Is.False);
                Assert.That(res.error, Does.Contain("No space"));
            }

            Assert.That(Directory.GetDirectories(_root).Any(d => Path.GetFileName(d).StartsWith(".staging-")), Is.False, "반쪽 저장 폴더가 남으면 안 됨");
            var after = new PaintStore(_root).LoadAll();
            Assert.That(after.Count, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(PaintStore.ToJson(after[i]), Is.EqualTo(snapshot[i].Item1));
                Assert.That(File.ReadAllBytes(ok.PathOf(after[i], PaintStore.StillFile)), Is.EqualTo(snapshot[i].Item2));
            }
            Assert.That(ok.NextNumber(), Is.EqualTo(3));
        }

        [Test]
        public void Store_LoopModeIsNotSavedAsSnap()
        {
            var d = Draft(1);
            d.mode = CatchMode.Loop;
            var res = new PaintStore(_root).Save(d);
            Assert.That(res.ok, Is.False);
        }
    }
}

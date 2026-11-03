using System.IO;
using System.Linq;
using NUnit.Framework;
using Reality404.BuildGate;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class BuildGateProjectTests
    {
        static string Root => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void ProjectPassesBuildGateFileChecks()
        {
            var fails = GateFileChecks.RunAll(Root).Where(r => r.Level == GateLevel.Fail).Select(r => r.ToString()).ToList();
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
        }

        [Test]
        public void DeskKeyboardScene_PassesBuildGateFileChecks()
        {
            var fails = GateFileChecks.RunAll(Root, new[] { "Assets/Scenes/DeskKeyboard.unity" })
                .Where(r => r.Level == GateLevel.Fail).Select(r => r.ToString()).ToList();
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
        }

        [Test]
        public void GateConfigUsesMoodPickerLogTag()
        {
            Assert.That(GateConfig.Load(Root).LogTag, Is.EqualTo("MP"));
        }
    }
}

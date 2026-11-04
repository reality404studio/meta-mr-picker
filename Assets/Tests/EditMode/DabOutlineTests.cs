using System.IO;
using System.Linq;
using NUnit.Framework;
using Reality404.BuildGate;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class DabOutlineTests
    {
        const string OutlinePath = "Assets/Shaders/Cel/CelOutline.shader";
        static string Root => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void Outline_IsKleinBlueAndRendersBackFacesOnly()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(OutlinePath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported);
            var m = new Material(shader);
            Assert.That(ColorUtility.ToHtmlStringRGB(m.GetColor("_OutlineColor")), Is.EqualTo("15317C"));
            Assert.That(m.GetFloat("_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Front), "_Cull 1 = 앞면을 버리고 뒷면만 (Render Face: Back)");
            Assert.That(m.FindPass("Outline"), Is.GreaterThanOrEqualTo(0));
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Gate_RejectsOutlineShaderThatDrawsFrontFaces()
        {
            Assert.That(GateFileChecks.OutlineCullViolations(new[] { "Pass { Cull Back }" }), Is.Not.Empty);
            Assert.That(GateFileChecks.OutlineCullViolations(new[] { "Pass { Cull Off }" }), Is.Not.Empty);
            Assert.That(GateFileChecks.OutlineCullViolations(new[] { "Pass { ZWrite On }" }), Is.Not.Empty, "Cull 없으면 기본 Back");
            Assert.That(GateFileChecks.OutlineCullViolations(new[] { "[Enum(UnityEngine.Rendering.CullMode)] _Cull (\"Cull\", Float) = 2", "Cull [_Cull]" }), Is.Not.Empty);
            Assert.That(GateFileChecks.OutlineCullViolations(new[] { "Pass { Cull Front }" }), Is.Empty);
            Assert.That(GateFileChecks.OutlineCullViolations(File.ReadAllLines(Path.Combine(Root, OutlinePath))), Is.Empty);
        }

        [Test]
        public void Gate_ProjectOutlineShadersPass()
        {
            var fails = GateFileChecks.RunAll(Root, new string[0])
                .Where(r => r.Level == GateLevel.Fail && (r.ToString().Contains("outline-shader") || r.ToString().Contains("outline-cull")))
                .Select(r => r.ToString()).ToList();
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MoodPicker.Catch;
using MoodPicker.Dab;
using NUnit.Framework;
using Reality404.BuildGate;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MoodPicker.Tests
{
    public class OrionLookTests
    {
        Material material;

        [SetUp]
        public void SetUp() { material = OrionLookRender.NewPaintMaterial(); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(material); }

        [Test]
        public void PropertiesAndDefaultsKeepContinuousTextureAndOptionalOutline()
        {
            foreach (string name in new[] { "_BaseMap", "_LightTone", "_MidTone", "_ShadowTone", "_LightDir", "_ToneMix", "_ToneSteps", "_ToneSoftness", "_Outline", "_OutlineColor", "_OutlineWidth", "_Threshold", "_Softness", "_PaintMask" })
                Assert.That(material.HasProperty(name), Is.True, name);
            Assert.That(material.GetFloat(CelPaint.ToneSteps), Is.Zero);
            Assert.That(material.GetFloat(CelPaint.Outline), Is.Zero);
            Assert.That(material.GetFloat(CelPaint.ToneMix), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(material.GetFloat("_ToneSoftness"), Is.EqualTo(1f));
            Assert.That(material.GetColor("_OutlineColor"), Is.EqualTo(CelShaderRender.Ink).Using(new UnityEngine.TestTools.Utils.ColorEqualityComparer(0.002f)));
            Assert.That(material.FindPass("Outline"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void LegacyApplyPreservesLookControlsAndNullTexture()
        {
            material.SetFloat(CelPaint.ToneSteps, 4);
            material.SetFloat(CelPaint.Outline, 1);
            material.SetFloat(CelPaint.ToneMix, 0.65f);
            material.SetColor(CelPaint.MidTone, Color.cyan);
            material.SetTexture(CelPaint.BaseMap, Texture2D.blackTexture);
            CelPaint.Apply(material, new PaintRecord { lightTone = "#FFFFFF", shadowTone = "#000000", lightDir = new[] { 2f, 0f, 0f } }, null);
            Assert.That(material.GetFloat(CelPaint.ToneSteps), Is.EqualTo(4));
            Assert.That(material.GetFloat(CelPaint.Outline), Is.EqualTo(1));
            Assert.That(material.GetFloat(CelPaint.ToneMix), Is.EqualTo(0.65f));
            Assert.That(material.GetColor(CelPaint.MidTone), Is.EqualTo(Color.gray));
            Assert.That(material.GetTexture(CelPaint.BaseMap), Is.SameAs(Texture2D.blackTexture));
            Assert.That(material.GetColor(CelPaint.LightTone), Is.EqualTo(Color.white));
            Assert.That(material.GetColor(CelPaint.ShadowTone), Is.EqualTo(Color.black));
            Assert.That(material.GetVector(CelPaint.LightDir), Is.EqualTo(new Vector4(1, 0, 0, 0)));
            Assert.DoesNotThrow(() => CelPaint.Apply(null, new PaintRecord(), null));
            Assert.DoesNotThrow(() => CelPaint.Apply(material, null, null));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(4, true)]
        [TestCase(-1, false)]
        public void ApplyOverloadSetsControlsAndCapturedMidTone(int steps, bool outline)
        {
            var texture = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Clamp };
            try
            {
                CelPaint.Apply(material, new PaintRecord { midTone = "#00FF00" }, texture, steps, outline);
                Assert.That(material.GetFloat(CelPaint.ToneSteps), Is.EqualTo(Mathf.Max(0, steps)));
                Assert.That(material.GetFloat(CelPaint.Outline), Is.EqualTo(outline ? 1 : 0));
                Assert.That(material.GetColor(CelPaint.MidTone), Is.EqualTo(Color.green));
                Assert.That(material.GetTexture(CelPaint.BaseMap), Is.SameAs(texture));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ApplyOverloadMissingMidToneUsesEndpoints()
        {
            CelPaint.Apply(material, new PaintRecord { lightTone = "#FFFFFF", shadowTone = "#000000" }, null, 0, false);
            Assert.That(material.GetColor(CelPaint.MidTone), Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1)));
            material.SetFloat(CelPaint.ToneSteps, 3);
            CelPaint.Apply(material, null, null, 0, false);
            Assert.That(material.GetFloat(CelPaint.ToneSteps), Is.EqualTo(3));
            Assert.DoesNotThrow(() => CelPaint.Apply(null, new PaintRecord(), null, 0, false));
        }

        [Test]
        public void ClayReferencesShaderGuidAndHasNoTextureOrOutline()
        {
            string guid = AssetDatabase.AssetPathToGUID(CelShaderRender.ShaderPath);
            string yaml = File.ReadAllText(OrionLookRender.ClayPath);
            StringAssert.Contains("m_Shader: {fileID: 4800000, guid: " + guid + ", type: 3}", yaml);
            var clay = AssetDatabase.LoadAssetAtPath<Material>(OrionLookRender.ClayPath);
            Assert.That(clay, Is.Not.Null);
            Assert.That(clay.shader, Is.SameAs(material.shader));
            Assert.That(clay.GetFloat(CelPaint.ToneSteps), Is.Zero);
            Assert.That(clay.GetFloat(CelPaint.Outline), Is.Zero);
            Assert.That(Regex.IsMatch(yaml, @"m_Texture: \{fileID: [^0]"), Is.False);
            foreach (int tone in new[] { CelPaint.LightTone, CelPaint.MidTone, CelPaint.ShadowTone })
            {
                Color color = clay.GetColor(tone);
                Assert.That(color.r * 255, Is.InRange(223f, 239f));
                Assert.That(color.g, Is.EqualTo(color.r));
                Assert.That(color.b, Is.EqualTo(color.r));
            }
        }

        [Test]
        public void ShaderPassesKeepAlphaStereoAndBackFaceOutline()
        {
            string source = File.ReadAllText(CelShaderRender.ShaderPath);
            Assert.That(GateFileChecks.ColorMaskViolations(source.Split('\n'), out _), Is.Empty);
            int start = source.IndexOf("Name \"Outline\"");
            int end = source.IndexOf("ENDHLSL", start);
            string outline = source.Substring(start, end - start);
            Assert.That(GateFileChecks.OutlineCullViolations(outline.Split('\n')), Is.Empty);
            StringAssert.Contains("clip(_Outline - 0.5)", outline);
            StringAssert.Contains("half4(_OutlineColor.rgb, 1.0h)", outline);
            Assert.That(Regex.Matches(source, "#pragma multi_compile_instancing").Count, Is.EqualTo(3));
            Assert.That(Regex.Matches(source, "UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO").Count, Is.EqualTo(3));
            Assert.That(GateFileChecks.OutlineCullViolations(File.ReadAllLines(CelShaderRender.OutlineShaderPath)), Is.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        public void RenderedRampHasContinuousOrExactIntegerToneCount(int steps)
        {
            material.SetFloat(CelPaint.ToneMix, 1);
            material.SetFloat(CelPaint.ToneSteps, steps);
            material.SetVector(CelPaint.LightDir, Vector3.right);
            material.SetColor(CelPaint.ShadowTone, Color.black);
            material.SetColor(CelPaint.MidTone, Color.gray);
            material.SetColor(CelPaint.LightTone, Color.white);
            Texture2D image = OrionLookRender.RenderMaterials(new[] { material });
            try
            {
                Color32[] pixels = image.GetPixels32();
                Assert.That(pixels.Any(p => p.a == 255), Is.True);
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                var colors = new HashSet<int>(pixels.Where(p => p.a == 255).Select(p => p.r << 16 | p.g << 8 | p.b));
                if (steps == 0) Assert.That(colors.Count, Is.GreaterThan(64));
                else Assert.That(colors.Count, Is.EqualTo(steps));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            }
            finally { Object.DestroyImmediate(image); }
        }

        [Test]
        public void OutlineToggleAddsOnlyOpaqueKleinBorder()
        {
            var clay = new Material(AssetDatabase.LoadAssetAtPath<Material>(OrionLookRender.ClayPath));
            Texture2D without = null, with = null;
            try
            {
                without = OrionLookRender.RenderMaterials(new[] { clay });
                clay.SetFloat(CelPaint.Outline, 1);
                with = OrionLookRender.RenderMaterials(new[] { clay });
                Color32[] a = without.GetPixels32(), b = with.GetPixels32();
                int added = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i].a == 255) Assert.That(b[i], Is.EqualTo(a[i]), "outline must not cover surface");
                    else if (b[i].a != 0)
                    {
                        added++;
                        Assert.That(b[i].a, Is.EqualTo(255));
                        Assert.That((int)b[i].r, Is.InRange(0x15 - 2, 0x15 + 2));
                        Assert.That((int)b[i].g, Is.InRange(0x31 - 2, 0x31 + 2));
                        Assert.That((int)b[i].b, Is.InRange(0x7C - 2, 0x7C + 2));
                    }
                }
                Assert.That(added, Is.GreaterThan(100));
            }
            finally
            {
                Object.DestroyImmediate(clay);
                Object.DestroyImmediate(without);
                Object.DestroyImmediate(with);
            }
        }
    }
}

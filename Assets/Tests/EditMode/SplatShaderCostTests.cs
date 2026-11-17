using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class SplatShaderCostTests
    {
        const string SplatShaderPath = "Assets/Shaders/Splatter/LetterSplatURP.shader";

        [Test]
        public void SplatPixelStageDoesNotRecomputePerSplatValues()
        {
            string src = File.ReadAllText(SplatShaderPath);
            Match body = Regex.Match(src, @"half4 EvaluateSplatShade\(Varyings i\)(.*?)ENDHLSL", RegexOptions.Singleline);
            Assert.That(body.Success, "EvaluateSplatShade 를 찾지 못함");
            string frag = body.Groups[1].Value;
            Assert.That(frag, Does.Not.Contain("ApplyFront("), "번짐 앞머리는 정점 단계에서 계산한다");
            Assert.That(frag, Does.Not.Contain("distance(i.centerOS"), "번짐 거리는 정점 단계에서 계산한다");
            Assert.That(frag, Does.Not.Contain("modeOutlineSplashRadius.x"), "모드는 균일 값 _Mode 로 고른다");
            Assert.That(Regex.IsMatch(frag, @"UNITY_BRANCH\s+if \(detached\)"), "떨어진 점만 결정 법선을 계산한다");
            Assert.That(Regex.IsMatch(frag, @"UNITY_BRANCH\s+if \(_GlintParams\.x > 0\.001\)"), "반짝임 모듈이 꺼지면 건너뛴다");
        }

        [Test]
        public void SplatVertexStageSkipsGlintAndTheApplyWaveWhenTheyAreOff()
        {
            string src = File.ReadAllText(SplatShaderPath);
            Match body = Regex.Match(src, @"Varyings EvaluateSplatVertex\(Attributes v, float passKind\)(.*?)half4 EvaluateSplatShade", RegexOptions.Singleline);
            Assert.That(body.Success, "EvaluateSplatVertex 를 찾지 못함");
            string vert = body.Groups[1].Value;
            Assert.That(Regex.IsMatch(vert, @"UNITY_BRANCH\s+if \(_GlintParams\.x > 0\.001\)\s*\{\s*glint = SplatGlint\("), "반짝임이 꺼진 조각은 정점마다 반짝임을 계산하지 않는다 (성능 측정 11-16, build-2c40)");
            Assert.That(Regex.IsMatch(vert, @"\?\s*SplatGlint\("), Is.False, "삼항 연산은 양쪽을 다 계산한다");
            Assert.That(Regex.IsMatch(vert, @"UNITY_BRANCH\s+if \(_Slot2\.y > 0\.5\)\s*\{\s*float applyFront = ApplyFront\("), "이전 재질이 없으면 번짐 물결을 계산하지 않는다");
        }

        [Test]
        public void TheRitualVariantLeavesOutOutlineFurAndTheModesTheRitualNeverDraws()
        {
            string[] lines = File.ReadAllLines(SplatShaderPath);
            Assert.That(string.Join("\n", lines), Does.Contain("#pragma shader_feature_local __ SPLAT_RITUAL"));
            foreach (string guarded in new[] { "if (isOutline)", "fluff = _FurParams.x * (1.0 - detach);", "else if (mode == 1)", "if (mode == 2)" })
            {
                int at = System.Array.FindIndex(lines, l => l.Trim() == guarded);
                Assert.That(at, Is.GreaterThan(0), guarded + " 줄을 찾지 못함");
                Assert.That(lines[at - 1].Trim(), Is.EqualTo("#ifndef SPLAT_RITUAL"), guarded + " 는 의식 변형에서 컴파일하지 않는다");
            }
            System.Type setup = System.Type.GetType("TimeRitualSceneSetup, Assembly-CSharp-Editor");
            Assert.That(setup, Is.Not.Null);
            var mat = (UnityEngine.Material)setup.GetMethod("EnsureRitualSplatMaterial").Invoke(null, null);
            Assert.That(mat.shader.name, Is.EqualTo("MoodPicker/LetterSplatURP"));
            Assert.That(mat.IsKeywordEnabled(MoodPicker.Ritual.TimeRitualLook.LeanKeyword), Is.True, "의식의 스플랫 재질은 의식 변형을 쓴다");
        }
    }
}

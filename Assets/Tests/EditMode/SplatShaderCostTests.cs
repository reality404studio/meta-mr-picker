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
    }
}

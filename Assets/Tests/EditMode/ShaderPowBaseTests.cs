using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class ShaderPowBaseTests
    {
        const string ShaderRoot = "Assets/Shaders";

        static readonly Regex Pow = new Regex(@"\bpow\(\s*([^,]*)", RegexOptions.Compiled);
        static readonly Regex ClampedBase = new Regex(@"^(saturate|max|abs)\(|^1\.0h?\s*-\s*saturate\(", RegexOptions.Compiled);

        sealed class Verified
        {
            public string file;
            public string call;
            public (string file, string line)[] proof;
            public string why;
        }

        static readonly Verified[] Unclamped =
        {
            new Verified
            {
                file = "Splatter/LetterSplatURP.shader",
                call = "pow(h, 4.0)",
                proof = new[]
                {
                    ("Splatter/LetterSplatURP.shader", "float h = PigmentLuma(texLinear);"),
                    ("Splatter/LetterSplatURP.shader", "return pow(max(srgb, 0.0),"),
                    ("Pigment/PigmentCommon.hlsl", "float PigmentLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }"),
                },
                why = "h 는 max(srgb, 0) 의 거듭제곱에 양수 가중치를 곱한 밝기"
            },
            new Verified
            {
                file = "Splatter/LetterSplatURP.shader",
                call = "pow(lerp(lift, float3(1.0, 1.0, 1.0), 0.35 + 0.65 * whit), 2.2)",
                proof = new[]
                {
                    ("Splatter/LetterSplatURP.shader", "float3 lift = texSRGB / max(max(texSRGB.r, max(texSRGB.g, texSRGB.b)), 1e-3);"),
                },
                why = "lift 는 텍스처 색을 최대 채널로 나눈 값이고, 텍스처 샘플과 삼면 가중치는 0 이상"
            },
            new Verified
            {
                file = "Pigment/PigmentCommon.hlsl",
                call = "pow(hLinear, contrast)",
                proof = new[]
                {
                    ("Splatter/LetterSplatURP.shader", "float h = PigmentLuma(texLinear);"),
                    ("Splatter/LetterSplatURP.shader", "ShadeRaw(oldLinear, normalWS, L, V, PigmentLuma(oldLinear), d)"),
                },
                why = "CausticTerm 은 위와 같은 밝기 h 만 받는다"
            },
        };

        static string StripComment(string line)
        {
            int i = line.IndexOf("//", System.StringComparison.Ordinal);
            return i >= 0 ? line.Substring(0, i) : line;
        }

        static string Rel(string path) => path.Replace('\\', '/').Substring(ShaderRoot.Length + 1);

        static IEnumerable<string> ShaderFiles() =>
            Directory.GetFiles(ShaderRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".shader") || f.EndsWith(".hlsl") || f.EndsWith(".cginc"));

        [Test]
        public void PowBasesCannotGoNegativeSoFacingSplatsNeverTurnNaN()
        {
            var files = ShaderFiles().ToArray();
            Assert.That(files, Is.Not.Empty, "셰이더를 찾지 못함");
            int checkedCalls = 0;
            foreach (string file in files)
            {
                string rel = Rel(file);
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (Match m in Pow.Matches(code))
                    {
                        checkedCalls++;
                        string arg = m.Groups[1].Value.Trim();
                        if (ClampedBase.IsMatch(arg)) continue;
                        string rest = code.Substring(m.Index);
                        bool verified = Unclamped.Any(v => v.file == rel && rest.StartsWith(v.call));
                        Assert.That(verified,
                            $"{rel}:{i + 1} pow 의 밑이 음수가 될 수 있다 (GPU pow 는 exp2(y*log2(x)) 라 음수면 NaN, 스플랫 하나가 통째로 흰 점이 됨). " +
                            $"saturate/max/abs 로 자르거나, 음수가 될 수 없음을 확인하고 근거 줄과 함께 목록에 넣는다: {code.Trim()}");
                    }
                }
            }
            Assert.That(checkedCalls, Is.GreaterThan(0), "pow 호출을 하나도 못 찾음 (정규식 확인)");
        }

        [Test]
        public void EveryUnclampedPowStillRestsOnTheLinesThatProvedItSafe()
        {
            foreach (Verified v in Unclamped)
            {
                string src = File.ReadAllText(Path.Combine(ShaderRoot, v.file));
                Assert.That(src, Does.Contain(v.call), $"{v.file}: 확인했던 '{v.call}' 이 없어졌다. 목록에서 빼거나 다시 확인");
                foreach (var (file, line) in v.proof)
                {
                    string proofSrc = File.ReadAllText(Path.Combine(ShaderRoot, file));
                    Assert.That(proofSrc, Does.Contain(line),
                        $"{v.call} 이 음수가 아니라는 근거가 바뀌었다 ({file}: '{line}'). 근거: {v.why}. 다시 확인하고 목록을 고친다");
                }
            }
        }

        [Test]
        public void CausticTermOnlyEverReceivesTheLumaH()
        {
            foreach (string file in ShaderFiles())
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"CausticTerm\(\s*([^,]+),"))
                {
                    string arg = m.Groups[1].Value.Trim();
                    if (arg.StartsWith("float ")) continue;
                    Assert.That(arg, Is.EqualTo("h"), $"{Rel(file)}: CausticTerm 은 밝기 h 만 받는다고 확인했다. 다른 값이면 pow(hLinear, …) 를 다시 확인");
                }
            }
        }
    }
}

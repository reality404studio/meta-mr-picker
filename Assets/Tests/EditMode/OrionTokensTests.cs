using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MoodPicker.HandStates;
using MoodPicker.PullOut;
using MoodPicker.Tokens;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class OrionTokensTests
    {
        static string ProjectRoot()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "design/tokens/orion.tokens.json")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("design/tokens/orion.tokens.json relative to project root");
        }

        static Dictionary<string, string> ReadTokens()
        {
            string json = File.ReadAllText(Path.Combine(ProjectRoot(), "design/tokens/orion.tokens.json"));
            var tokens = Regex.Matches(json, "\"(?:\\\\.|[^\"\\\\])*\"|-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null|[{}\\[\\]:,]")
                .Cast<Match>().Select(match => match.Value).ToArray();
            var values = new Dictionary<string, string>();
            int cursor = 0;
            ReadValue(tokens, ref cursor, "", values);
            Assert.That(cursor, Is.EqualTo(tokens.Length));
            return values;
        }

        static string Unquote(string token) => Regex.Unescape(token.Substring(1, token.Length - 2));

        static void Expect(string[] tokens, ref int cursor, string expected)
        {
            Assert.That(tokens[cursor++], Is.EqualTo(expected));
        }

        static void ReadValue(string[] tokens, ref int cursor, string path, Dictionary<string, string> values)
        {
            if (tokens[cursor] == "{")
            {
                cursor++;
                while (tokens[cursor] != "}")
                {
                    string key = Unquote(tokens[cursor++]);
                    Expect(tokens, ref cursor, ":");
                    ReadValue(tokens, ref cursor, path.Length == 0 ? key : path + "." + key, values);
                    if (tokens[cursor] != ",") break;
                    cursor++;
                }
                Expect(tokens, ref cursor, "}");
            }
            else if (tokens[cursor] == "[")
            {
                cursor++;
                int index = 0;
                while (tokens[cursor] != "]")
                {
                    ReadValue(tokens, ref cursor, path + "[" + index++ + "]", values);
                    if (tokens[cursor] != ",") break;
                    cursor++;
                }
                Expect(tokens, ref cursor, "]");
            }
            else values.Add(path, tokens[cursor++]);
        }

        static string Member(string path) => string.Concat(path.Split('.').Select(part =>
            char.ToUpperInvariant(part[0]) + part.Substring(1))).Replace("[", "").Replace("]", "");

        static object Constant(string name)
        {
            var field = typeof(OrionTokens).GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            Assert.That(field.IsLiteral, Is.True, name);
            return field.GetRawConstantValue();
        }

        [Test]
        public void EveryJsonLeafMatchesGeneratedConstantIncludingColorsTimesAndPalmHysteresis()
        {
            var tokens = ReadTokens();
            Assert.That(tokens.Count, Is.GreaterThan(70));
            foreach (var pair in tokens)
            {
                string member = Member(pair.Key);
                string raw = pair.Value;
                if (raw.StartsWith("\"", StringComparison.Ordinal))
                {
                    string value = Unquote(raw);
                    bool color = Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$");
                    Assert.That(Constant(member + (color ? "Hex" : "")), Is.EqualTo(value), pair.Key);
                    if (color)
                        for (int channel = 0; channel < 3; channel++)
                            Assert.That(Constant(member + "RGB"[channel]), Is.EqualTo(
                                int.Parse(value.Substring(1 + channel * 2, 2), NumberStyles.HexNumber) / 255f), pair.Key);
                }
                else if (raw == "true" || raw == "false")
                    Assert.That(Constant(member), Is.EqualTo(bool.Parse(raw)), pair.Key);
                else
                {
                    float value = float.Parse(raw, CultureInfo.InvariantCulture);
                    Assert.That(Constant(member), Is.EqualTo(value), pair.Key);
                    var unit = Regex.Match(pair.Key, @"(Mm|Cm)(\[\d+\])?$");
                    if (unit.Success)
                    {
                        string meters = Member(pair.Key.Substring(0, unit.Index) + "Meters" + unit.Groups[2].Value);
                        Assert.That((float)Constant(meters), Is.EqualTo(value / (unit.Groups[1].Value == "Mm" ? 1000f : 100f))
                            .Within(0.0000001f), pair.Key);
                    }
                }
            }
        }

        [Test]
        public void PullOutAndHandDefaultsFollowTokens()
        {
            Assert.That(PullOutSettings.LetterDelaySeconds, Is.EqualTo(OrionTokens.MotionPulloutStaggerStaggerSeconds));
            Assert.That(new PullOutSettings().DurationSeconds, Is.EqualTo(OrionTokens.MotionPulloutStaggerSeconds));
            Assert.That(PullOutSettings.Ease, Is.EqualTo(OrionTokens.MotionPulloutStaggerEase));
            Assert.That(PullOutSettings.ThicknessFrom, Is.EqualTo(OrionTokens.MotionPulloutStaggerThicknessFrom));
            Assert.That(PullOutSettings.ThicknessTo, Is.EqualTo(OrionTokens.MotionPulloutStaggerThicknessTo));
            var hand = new HandStyleSettings();
            Assert.That(new[] { hand.Bare.R, hand.Bare.G, hand.Bare.B }, Is.EqualTo(new[] {
                OrionTokens.ColorPaperFaceR, OrionTokens.ColorPaperFaceG, OrionTokens.ColorPaperFaceB }));
            Assert.That(new[] { hand.Stamp.R, hand.Stamp.G, hand.Stamp.B }, Is.EqualTo(new[] {
                OrionTokens.ColorPrintCobaltR, OrionTokens.ColorPrintCobaltG, OrionTokens.ColorPrintCobaltB }));
            Assert.That(HandStyleSettings.RimPower, Is.EqualTo(OrionTokens.StrokeHandRimPower));
            Assert.That(HandStyleSettings.InflateMm, Is.EqualTo(OrionTokens.StrokeHandInflateMm));
            Assert.That(HandStyleSettings.InflateMeters, Is.EqualTo(OrionTokens.StrokeHandInflateMeters));
            Assert.That(HandStyleSettings.PassthroughWorkspace, Is.EqualTo(OrionTokens.EnvPassthroughWorkspace));
            Assert.That(HandStyleSettings.PassthroughHero, Is.EqualTo(OrionTokens.EnvPassthroughHero));
            Assert.That(HandStyleSettings.PassthroughTransitionSeconds, Is.EqualTo(OrionTokens.EnvPassthroughTransitionSeconds));
        }
    }
}

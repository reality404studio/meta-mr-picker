using System;
using MoodPicker.MacAnchoring;
using UnityEngine;

namespace MoodPicker.PullOut
{
    public enum PullOutLetter { O1, R, I, O2, N }

    public readonly struct LetterStart
    {
        public PullOutLetter Letter { get; }
        public float Time { get; }
        public LetterStart(PullOutLetter letter, float time) { Letter = letter; Time = time; }
    }

    public readonly struct LetterPose
    {
        public Vector3 Position { get; }
        public float ThicknessScale { get; }
        public LetterPose(Vector3 position, float thicknessScale)
        {
            Position = position;
            ThicknessScale = thicknessScale;
        }
    }

    public sealed class PullOutSequence
    {
        readonly MacScreenRectangle _screen;
        readonly PullOutSettings _settings;
        readonly ScreenTapDetector _tap;
        int _nextLetter;

        public bool HasStarted { get; private set; }
        public float TapTime { get; private set; }
        public float EndTime => HasStarted ? StartTime(PullOutLetter.N) + _settings.DurationSeconds : float.PositiveInfinity;
        public event Action<float> Tapped;
        public event Action<LetterStart> LetterStarted;

        public PullOutSequence(MacScreenRectangle screen, PullOutSettings settings = null)
        {
            _screen = screen;
            _settings = (settings ?? new PullOutSettings()).Snapshot();
            _tap = new ScreenTapDetector(screen, _settings);
            _tap.Tapped += Begin;
        }

        public void UpdateTip(float time, Vector3 indexTip, bool tracked, bool isPointing, bool isBareHand)
        {
            if (!HasStarted) _tap.Update(time, indexTip, tracked, isPointing, isBareHand);
        }

        public void CancelContact() => _tap.Reset();

        public float StartTime(PullOutLetter letter)
        {
            int index = Index(letter);
            return HasStarted ? TapTime + PullOutSettings.LetterDelaySeconds * index : float.PositiveInfinity;
        }

        public Vector3 ScreenPosition(PullOutLetter letter) => _screen.Plane.ToWorld(new Vector3(
            (Index(letter) - 2) * _screen.widthMeters * _settings.ScreenWidthFraction / 4f,
            0f, _screen.heightMeters * _settings.ScreenBaselineFraction));

        public Vector3 HoverPosition(PullOutLetter letter) => _screen.Plane.ToWorld(new Vector3(
            (Index(letter) - 2) * _settings.HoverLetterSpacingMeters,
            _settings.HoverNormalOffsetMeters, _settings.HoverHeightMeters));

        public LetterPose Sample(PullOutLetter letter, float time)
        {
            if (!PullOutSettings.Finite(time)) throw new ArgumentOutOfRangeException(nameof(time));
            float progress = HasStarted ? Mathf.Clamp01((time - StartTime(letter)) / _settings.DurationSeconds) : 0f;
            float eased = progress * progress * (3f - 2f * progress);
            return new LetterPose(Vector3.LerpUnclamped(ScreenPosition(letter), HoverPosition(letter), eased), eased);
        }

        public void Advance(float time)
        {
            if (!HasStarted || !PullOutSettings.Finite(time)) return;
            while (_nextLetter < PullOutSettings.LetterCount && time >= StartTime((PullOutLetter)_nextLetter))
            {
                var letter = (PullOutLetter)_nextLetter++;
                LetterStarted?.Invoke(new LetterStart(letter, StartTime(letter)));
            }
        }

        void Begin(float time)
        {
            if (HasStarted) return;
            TapTime = time;
            HasStarted = true;
            Tapped?.Invoke(time);
            Advance(time);
        }

        static int Index(PullOutLetter letter)
        {
            int index = (int)letter;
            if (index < 0 || index >= PullOutSettings.LetterCount) throw new ArgumentOutOfRangeException(nameof(letter));
            return index;
        }
    }
}

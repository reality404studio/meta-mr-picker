using System;
using MoodPicker.MacAnchoring;
using UnityEngine;

namespace MoodPicker.PullOut
{
    public sealed class PullOutView : MonoBehaviour
    {
        [SerializeField] GameObject orionPrefab;
        [SerializeField] Transform anchorSpace;
        [SerializeField] PullOutSettings settings = new PullOutSettings();

        readonly Transform[] _letters = new Transform[PullOutSettings.LetterCount];
        readonly Vector3[] _scales = new Vector3[PullOutSettings.LetterCount];
        GameObject _instance;

        public PullOutSequence Sequence { get; private set; }

        void Start()
        {
            if (Sequence != null || orionPrefab == null) return;
            var anchor = new MacAnchor();
            if (anchor.IsAvailable) Initialize(anchor.Rectangle, orionPrefab, anchorSpace, settings);
        }

        public void Initialize(MacScreenRectangle screen, GameObject importedOrionPrefab,
            Transform coordinateSpace = null, PullOutSettings configuration = null)
        {
            if (Sequence != null) throw new InvalidOperationException("PullOutView is already initialized.");
            if (importedOrionPrefab == null) throw new ArgumentNullException(nameof(importedOrionPrefab));
            var values = configuration ?? settings;
            var sequence = new PullOutSequence(screen, values);
            if (importedOrionPrefab.transform.childCount != PullOutSettings.LetterCount)
                throw new ArgumentException("ORION must contain exactly five letter children.");
            for (int i = 0; i < _letters.Length; i++)
                if (importedOrionPrefab.transform.Find(((PullOutLetter)i).ToString()) == null)
                    throw new ArgumentException("ORION is missing letter " + (PullOutLetter)i);

            _instance = Instantiate(importedOrionPrefab, coordinateSpace, false);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _instance.transform.localScale = Vector3.one;
            Quaternion rotation = Quaternion.LookRotation(screen.normal, screen.up);
            for (int i = 0; i < _letters.Length; i++)
            {
                var letter = _instance.transform.Find(((PullOutLetter)i).ToString());
                _letters[i] = letter;
                _scales[i] = letter.localScale * values.ModelScale;
                letter.localRotation = rotation * letter.localRotation;
            }
            Sequence = sequence;
            RenderAt(0f);
        }

        public void SubmitTip(float time, Vector3 indexTipInAnchorSpace, bool tracked, bool isPointing, bool isBareHand)
        {
            if (isActiveAndEnabled)
                Sequence?.UpdateTip(time, indexTipInAnchorSpace, tracked, isPointing, isBareHand);
        }

        public void RenderAt(float time)
        {
            if (Sequence == null) return;
            Sequence.Advance(time);
            for (int i = 0; i < _letters.Length; i++)
            {
                var pose = Sequence.Sample((PullOutLetter)i, time);
                _letters[i].localPosition = pose.Position;
                Vector3 scale = _scales[i];
                scale.z *= pose.ThicknessScale;
                _letters[i].localScale = scale;
            }
        }

        void Update() => RenderAt(Time.time);
        void OnEnable() { if (_instance != null) _instance.SetActive(true); }
        void OnDisable()
        {
            Sequence?.CancelContact();
            if (_instance != null) _instance.SetActive(false);
        }
        void OnDestroy() { if (_instance != null) Destroy(_instance); }
    }
}

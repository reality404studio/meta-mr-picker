using System;
using MoodPicker.Hands;
using MoodPicker.Splatter;
using UnityEngine;

namespace MoodPicker.Splatter.Patisserie
{
    public sealed class PipingBagInteractor : MonoBehaviour
    {
        [SerializeField] IcingCanvasController canvas;
        [SerializeField] SplatPigmentSlotAsset[] paletteSlots;
        [SerializeField] Transform nozzleTransform;
        [SerializeField] float jetSpeed = 0.05f;

        IcingNozzleProfile _profile = IcingNozzleProfile.Star6;
        ParallelFrame _currentFrame;
        bool _hasFrame;
        Vector3 _lastEmitPos;
        bool _isPiping;
        float _currentSqueeze;

        IHandInput _handInput;
        int _selectedSlotIndex = 0;
        int _selectedNozzleIndex = 0;

        readonly IcingNozzleProfile[] _nozzlePresets = new[]
        {
            IcingNozzleProfile.Star6,
            IcingNozzleProfile.FrenchStar8,
            IcingNozzleProfile.Round,
            IcingNozzleProfile.PetalRibbon
        };

        public IcingNozzleProfile CurrentNozzle => _profile;
        public bool IsPiping => _isPiping;

        void Start()
        {
            if (canvas == null) canvas = FindFirstObjectByType<IcingCanvasController>();
            var allMono = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < allMono.Length; i++)
            {
                if (allMono[i] is IHandInput hi)
                {
                    _handInput = hi;
                    break;
                }
            }
        }

        void Update()
        {
            HandleEditorShortcuts();

            bool handActive = TryUpdateFromHandTracking();
            if (!handActive)
            {
                UpdateFromMouseInput();
            }
        }

        void HandleEditorShortcuts()
        {
            if (paletteSlots != null && paletteSlots.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(5, paletteSlots.Length); i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        _selectedSlotIndex = i;
                        if (canvas != null && paletteSlots[i] != null)
                        {
                            canvas.PourGlaze(paletteSlots[i], Vector3.zero, 1.2f);
                        }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                _selectedNozzleIndex = (_selectedNozzleIndex + 1) % _nozzlePresets.Length;
                _profile = _nozzlePresets[_selectedNozzleIndex];
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                if (canvas != null) canvas.ClearAllIcing();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                if (canvas != null && paletteSlots != null && paletteSlots.Length > 0)
                {
                    int nextSlot = (_selectedSlotIndex + 1) % paletteSlots.Length;
                    canvas.PourGlaze(paletteSlots[nextSlot], Vector3.up * 0.08f, 1.5f);
                    _selectedSlotIndex = nextSlot;
                }
            }

            if (Input.GetMouseButton(1) && canvas != null)
            {
                float mouseX = Input.GetAxis("Mouse X");
                canvas.RotateTurntable(-mouseX * 90f * Time.deltaTime);
            }
        }

        bool TryUpdateFromHandTracking()
        {
            if (_handInput == null) return false;
            if (!_handInput.TryGetFrame(out HandFrame frame)) return false;

            HandPose right = frame.right;
            if (!right.tracked) return false;

            float pinchDist = right.PinchDistance;
            Vector3 pinchPoint = right.PinchPoint;

            if (pinchDist < 0.045f)
            {
                float squeeze = Mathf.Clamp01((0.045f - pinchDist) / 0.035f);
                Vector3 jetDir = right.palmNormal.sqrMagnitude > 1e-4f ? -right.palmNormal : Vector3.down;
                ProcessPipingStep(pinchPoint, jetDir, squeeze);
                return true;
            }
            else if (_isPiping)
            {
                EndStroke();
            }

            return true;
        }

        void UpdateFromMouseInput()
        {
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main != null ? Camera.main.ScreenPointToRay(Input.mousePosition) : new Ray(new Vector3(0, 0.5f, -0.3f), Vector3.forward);
                Plane cakePlane = new Plane(Vector3.up, canvas != null ? canvas.transform.position + Vector3.up * 0.08f : Vector3.zero);

                if (cakePlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    ProcessPipingStep(hitPoint, Vector3.down, 1.0f);
                }
            }
            else if (_isPiping)
            {
                EndStroke();
            }
        }

        void ProcessPipingStep(Vector3 tipPosWS, Vector3 jetDirWS, float squeeze)
        {
            if (canvas == null) return;

            Transform cakeRoot = canvas.CakeRoot != null ? canvas.CakeRoot : canvas.transform;
            Vector3 tipPosOS = cakeRoot.InverseTransformPoint(tipPosWS);
            Vector3 jetDirOS = cakeRoot.InverseTransformDirection(jetDirWS).normalized;

            if (!_isPiping)
            {
                _isPiping = true;
                _currentSqueeze = squeeze;
                _lastEmitPos = tipPosOS;
                Vector3 initialForward = Vector3.forward;
                _currentFrame = ParallelFrame.Initialize(tipPosOS, tipPosOS + initialForward * 0.01f, Vector3.up);
                _hasFrame = true;

                EmitRingAt(tipPosOS, jetDirWS, squeeze, cakeRoot);
                return;
            }

            float dist = Vector3.Distance(tipPosOS, _lastEmitPos);
            float stepSpacing = Mathf.Max(0.001f, _profile.advanceSpacing);

            if (dist >= stepSpacing && _hasFrame)
            {
                _currentFrame = _currentFrame.Advance(tipPosOS);
                _currentSqueeze = Mathf.Lerp(_currentSqueeze, squeeze, 0.5f);
                _lastEmitPos = tipPosOS;

                EmitRingAt(tipPosOS, jetDirWS, _currentSqueeze, cakeRoot);
            }
        }

        void EmitRingAt(Vector3 centerOS, Vector3 jetDirWS, float squeeze, Transform cakeRoot)
        {
            SplatStrokeChunk chunk = canvas.GetOrCreateActiveChunk(_profile.splatsPerRing);
            if (chunk == null) return;

            SplatSample[] ring = IcingTubeSampler.SampleRing(_currentFrame, _profile, squeeze, Vector3.up);
            var homeBodies = new Vector3[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                homeBodies[i] = new Vector3(ring[i].CenterX, ring[i].CenterY, ring[i].CenterZ);
            }

            Vector3 nozzleTipWS = cakeRoot.TransformPoint(centerOS);
            chunk.AppendRing(ring, homeBodies, nozzleTipWS, jetDirWS, jetSpeed);
        }

        void EndStroke()
        {
            _isPiping = false;
            _hasFrame = false;
        }
    }
}

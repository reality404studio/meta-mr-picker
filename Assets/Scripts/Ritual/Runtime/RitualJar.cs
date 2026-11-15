using System;
using System.Collections.Generic;
using MoodPicker.Splatter;
using Unity.Mathematics;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualJar
    {
        const float GoldenAngle = 2.39996323f;
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int InnerId = Shader.PropertyToID("_Inner");
        static readonly int RimId = Shader.PropertyToID("_Rim");
        static readonly int SpecId = Shader.PropertyToID("_Spec");
        static readonly int BodyId = Shader.PropertyToID("_Body");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");

        public sealed class Layer
        {
            public TimeCategoryAsset category;
            public int categoryIndex;
            public int units;
            public RitualSplatCloud cloud;
            public RitualSurface surface;
            public float[] bottom;
            public float[] top;
            public float[] emit;
            public float[] flight;
            public float3[] source;
            public bool[] landed;
            public int landedCount;
            public bool settled;
            public float grainScale = 1f;
        }

        struct Point
        {
            public float3 home;
            public float3 normal;
            public float y;
        }

        public readonly Transform root;
        public event Action<Layer> PourStarted;
        public event Action<Layer> PourFinished;
        public event Action<Layer, int> GrainsLanded;

        readonly TimeRitualConfig _config;
        readonly SplatTuning _tuning;
        readonly Material _splatMaterial;
        readonly Func<TimeCategoryAsset, SplatSurfaceMaterial> _lookOf;
        readonly JarShape _shape;
        readonly List<Layer> _layers = new List<Layer>();
        readonly List<Point> _points = new List<Point>(4096);
        readonly MaterialPropertyBlock _glassBlock = new MaterialPropertyBlock();
        MeshRenderer _glass;
        Mesh _glassMesh;

        RitualSplatCloud _cap;
        RitualSurface _capSurface;
        RitualSplatCloud _floor;
        RitualSurface _floorSurface;
        float[] _capRadius;
        float[] _capAngle;
        float[] _capFrom;
        float[] _floorRadius;
        float[] _surface;
        float[] _next;

        Layer _pouring;
        TimePieceView _pourPiece;
        float _pourTime;
        float _pourSeconds;
        float _pourTail;
        int _totalUnits;
        int _totalGrains;
        int _seed = 17;
        float _level = 1f;
        float _quality = 1f;
        Color _innerGlow = Color.clear;
        float _pulse;
        bool _glassDirty = true;

        public IReadOnlyList<Layer> Layers => _layers;
        public bool Pouring => _pouring != null;
        public int TotalUnits => _totalUnits;
        public int TotalGrains => _totalGrains;
        public JarShape Shape => _shape;
        public float RimY => _shape.height;
        public Vector3 MouthCenter => root.TransformPoint(new Vector3(0f, _shape.height, 0f));
        public Vector3 BodyCenter => root.TransformPoint(new Vector3(0f, _shape.height * 0.45f, 0f));
        public Vector3 Spout => root.TransformPoint(new Vector3(0f, _shape.height + _config.spoutHeight, 0f));
        public float SurfaceMean => Strata.Mean(_surface);
        public int Capacity => RitualFill.Capacity(_shape, _config.hoursWhenFull);
        public RitualSplatCloud Cap => _cap;
        public RitualSplatCloud Floor => _floor;
        public Mesh GlassMesh => _glassMesh;

        public RitualJar(Transform parent, TimeRitualConfig config, SplatTuning tuning, Material splatMaterial, Material glassMaterial,
            Func<TimeCategoryAsset, SplatSurfaceMaterial> lookOf)
        {
            _config = config;
            _tuning = tuning;
            _splatMaterial = splatMaterial;
            _lookOf = lookOf;
            _shape = config.jar;
            var go = new GameObject("Jar");
            root = go.transform;
            root.SetParent(parent, false);
            _surface = Strata.Flat(_shape.floor);
            _next = Strata.Flat(_shape.floor);
            BuildGlass(glassMaterial);
            BuildCap();
            BuildFloor();
        }

        void BuildGlass(Material material)
        {
            if (material == null) return;
            int count = Mathf.Max(200, _config.glassSplats);
            const int steps = 160;
            var ys = new float[steps + 1];
            var rs = new float[steps + 1];
            var cumulative = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                ys[i] = _shape.height * i / steps;
                rs[i] = _shape.OuterRadius(ys[i]);
            }
            float baseArea = Mathf.PI * rs[0] * rs[0];
            float lipWidth = _shape.NeckOuter - _shape.mouthRadius;
            float lipArea = 2f * Mathf.PI * (_shape.mouthRadius + lipWidth * 0.5f) * lipWidth;
            cumulative[0] = 0f;
            for (int i = 1; i <= steps; i++)
            {
                float dy = ys[i] - ys[i - 1];
                float dr = rs[i] - rs[i - 1];
                float boost = ys[i] > _shape.lipBottom ? 2.2f : 1f;
                cumulative[i] = cumulative[i - 1] + Mathf.PI * (rs[i] + rs[i - 1]) * Mathf.Sqrt(dy * dy + dr * dr) * boost;
            }
            float side = cumulative[steps];
            float innerNeck = 2f * Mathf.PI * _shape.mouthRadius * (_shape.height - _shape.lipBottom) * 1.4f;
            float baseShare = baseArea * 1.6f;
            float total = side + baseShare + lipArea * 2.5f + innerNeck;
            var b = RitualStaticSplats.Builder.Create(count);
            for (int i = 0; i < count; i++)
            {
                float pick = (i + RitualUtil.Hash(i * 7 + 3)) / count * total;
                float angle = i * GoldenAngle + RitualUtil.Hash(i * 5 + 1) * 0.6f;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                float rnd = RitualUtil.Hash(i * 11 + 9);
                float size = 0.8f + 0.5f * RitualUtil.Hash(i * 3 + 2);
                Vector3 p, n;
                float weight = 1f;
                float bevel = 1f;
                float normY = 0f;
                if (pick < side)
                {
                    int lo = 0, hi = steps;
                    while (hi - lo > 1)
                    {
                        int mid = (lo + hi) / 2;
                        if (cumulative[mid] < pick) lo = mid;
                        else hi = mid;
                    }
                    float t = Mathf.InverseLerp(cumulative[lo], cumulative[hi], pick);
                    float y = Mathf.Lerp(ys[lo], ys[hi], t);
                    float r = Mathf.Lerp(rs[lo], rs[hi], t);
                    float slope = (rs[hi] - rs[lo]) / Mathf.Max(1e-6f, ys[hi] - ys[lo]);
                    Vector3 radial = new Vector3(ca, 0f, sa);
                    n = (radial - Vector3.up * slope).normalized;
                    p = radial * r + Vector3.up * y;
                    normY = y / Mathf.Max(1e-4f, _shape.height);
                    bool nearShoulder = y > _shape.bodyTop && y < _shape.lipBottom;
                    bool nearBase = y < _shape.floor * 1.4f;
                    if (y > _shape.lipBottom)
                    {
                        weight = 1.32f;
                        bevel = 1.3f;
                    }
                    else if (nearShoulder || nearBase)
                    {
                        bevel = 1.25f;
                    }
                }
                else if (pick < side + baseShare)
                {
                    float u = RitualUtil.Hash(i * 17 + 5);
                    float rimT = Mathf.Sqrt(u);
                    float r = rs[0] * rimT;
                    if (rimT > 0.74f)
                    {
                        float footT = (rimT - 0.74f) / 0.26f;
                        float y = 0.0008f + 0.0012f * Mathf.Sin(footT * Mathf.PI);
                        p = new Vector3(ca * r, y, sa * r);
                        n = new Vector3(ca * 0.78f, -0.62f, sa * 0.78f).normalized;
                        weight = 1.28f;
                        bevel = 1.35f;
                        normY = y / Mathf.Max(1e-4f, _shape.height);
                        size *= 1.15f;
                    }
                    else
                    {
                        float puntT = rimT / 0.74f;
                        float dome = 1f - puntT * puntT;
                        float y = 0.0010f + _shape.floor * 0.62f * dome;
                        p = new Vector3(ca * r, y, sa * r);
                        float radialSlope = 0.58f * puntT;
                        n = new Vector3(-ca * radialSlope, -0.82f, -sa * radialSlope).normalized;
                        weight = 1.22f;
                        bevel = 1.30f;
                        normY = y / Mathf.Max(1e-4f, _shape.height);
                        size *= 1.22f;
                    }
                }
                else if (pick < side + baseShare + lipArea * 2.5f)
                {
                    float r = _shape.mouthRadius + lipWidth * RitualUtil.Hash(i * 19 + 4);
                    p = new Vector3(ca * r, _shape.height, sa * r);
                    n = (Vector3.up + new Vector3(ca, 0f, sa) * 0.6f).normalized;
                    weight = 1.38f;
                    bevel = 1.35f;
                    normY = 1f;
                    size *= 0.85f;
                }
                else
                {
                    float y = Mathf.Lerp(_shape.lipBottom, _shape.height, RitualUtil.Hash(i * 23 + 8));
                    p = new Vector3(ca * _shape.mouthRadius, y, sa * _shape.mouthRadius);
                    n = new Vector3(-ca, 0f, -sa);
                    weight = 1.2f;
                    bevel = 1.2f;
                    normY = y / Mathf.Max(1e-4f, _shape.height);
                    size *= 0.85f;
                }
                b.Add(p, n, new Vector4(p.x, p.y, p.z, rnd), new Vector4(size, weight, bevel, normY), Vector4.zero);
            }
            _glassMesh = b.ToMesh("JarGlass", _shape.height);
            _glass = RitualStaticSplats.Spawn("Glass", root, _glassMesh, material);
        }

        void BuildCap()
        {
            int n = Mathf.Max(64, _config.capGrains);
            _cap = new RitualSplatCloud("SandTop", root, _splatMaterial, n, 0.3f);
            _capRadius = new float[n];
            _capAngle = new float[n];
            _capFrom = new float[n];
            float rMax = _shape.InnerRadius(_shape.bodyTop * 0.5f) - _config.grainRadius;
            float spacing = Mathf.Sqrt(Mathf.PI * rMax * rMax / n);
            float3 rest = new float3(0f, TimeRitualLook.RestLift, 0f);
            for (int i = 0; i < n; i++)
            {
                float r = rMax * Mathf.Sqrt((i + 0.5f) / n);
                float a = i * GoldenAngle;
                _capRadius[i] = r;
                _capAngle[i] = a;
                float3 tilt = new float3(RitualUtil.Hash(i * 3 + 1) - 0.5f, 0f, RitualUtil.Hash(i * 3 + 2) - 0.5f) * 0.5f;
                int s = _cap.Add(new float3(r * Mathf.Cos(a), _shape.floor, r * Mathf.Sin(a)), math.normalize(new float3(0f, 1f, 0f) + tilt), spacing * 0.62f);
                _cap.disp[s] = rest;
                _cap.scale[s] = 0f;
                _capFrom[i] = _shape.floor;
            }
            _cap.SetDrawCount(0);
            _capSurface = new RitualSurface { sizeMeters = _config.sandTextureSize, triplanarScale = 1f, aspectMax = _config.scatterAspectMax };
            _cap.Flush();
        }

        void BuildFloor()
        {
            int n = Mathf.Max(64, _config.capGrains);
            _floor = new RitualSplatCloud("SandBottom", root, _splatMaterial, n, 0.3f);
            _floorRadius = new float[n];
            float rMax = WallRadius(_shape.floor);
            float spacing = Mathf.Sqrt(Mathf.PI * rMax * rMax / n);
            float3 rest = new float3(0f, -TimeRitualLook.RestLift, 0f);
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Sqrt((i + 0.5f) / n);
                float r = rMax * k;
                float a = i * GoldenAngle;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                _floorRadius[i] = r;
                float y = _shape.floor + 0.0014f * k * k * k;
                float3 tilt = new float3(RitualUtil.Hash(i * 5 + 3) - 0.5f, 0f, RitualUtil.Hash(i * 5 + 4) - 0.5f) * 0.45f;
                float3 normal = math.normalize(new float3(ca * 0.28f * k, -1f, sa * 0.28f * k) + tilt);
                int s = _floor.Add(new float3(ca * r, y, sa * r), normal, spacing * 0.66f);
                _floor.disp[s] = rest;
                _floor.scale[s] = 0f;
            }
            _floor.SetDrawCount(0);
            _floorSurface = new RitualSurface { sizeMeters = _config.sandTextureSize, triplanarScale = 1f, aspectMax = _config.scatterAspectMax };
            _floor.Flush();
        }

        public bool InBody(Vector3 worldPoint)
        {
            Vector3 p = root.InverseTransformPoint(worldPoint);
            if (p.y < -0.01f || p.y > _shape.bodyTop) return false;
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            return r < _shape.bodyRadius + _config.jarGrabMargin;
        }

        public bool InGrabZone(Vector3 worldPoint, bool sealedVessel, bool secondHand = false)
        {
            if (!sealedVessel) return InBody(worldPoint);
            if (secondHand && Vector3.Distance(worldPoint, BodyCenter) <= _shape.bodyRadius + _config.jarGrabMargin + 0.14f)
                return true;
            Vector3 p = root.InverseTransformPoint(worldPoint);
            if (p.y < -0.025f || p.y > _shape.height + 0.03f) return false;
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            return r < _shape.bodyRadius + _config.jarGrabMargin + 0.022f;
        }

        public bool InPourZone(Vector3 worldPoint, float radius)
        {
            Vector3 p = root.InverseTransformPoint(worldPoint);
            if (p.y < _shape.height - 0.012f || p.y > _shape.height + _config.pourZoneHeight + radius) return false;
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            return r < _shape.mouthRadius + _config.pourZoneMargin + radius;
        }

        public bool InPourZone(TimePieceView piece)
        {
            piece.Segment(out Vector3 a, out Vector3 b);
            float r = piece.Radius;
            return InPourZone(a, r) || InPourZone(b, r) || InPourZone((a + b) * 0.5f, r);
        }

        public int Room => Math.Max(0, Capacity - _totalUnits);

        public bool Fits(int units)
        {
            return units > 0 && units <= Room;
        }

        public bool CanAccept(int units)
        {
            return _pouring == null && Fits(units);
        }

        float WallRadius(float y)
        {
            return Mathf.Max(0.002f, _shape.InnerRadius(y) - _config.grainRadius * 0.9f);
        }

        void GatherPoints(float[] bottom, float[] top)
        {
            _points.Clear();
            float spacing = Mathf.Max(0.0006f, _config.grainSpacing);
            float dy = spacing * 0.866f;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < bottom.Length; i++)
            {
                lo = Mathf.Min(lo, bottom[i]);
                hi = Mathf.Max(hi, top[i]);
            }
            int rowLo = Mathf.Max(0, Mathf.FloorToInt((lo - _shape.floor) / dy) - 1);
            int rowHi = Mathf.CeilToInt((hi - _shape.floor) / dy) + 1;
            for (int row = rowLo; row <= rowHi; row++)
            {
                float yRow = _shape.floor + (row + 0.5f) * dy;
                float rw = WallRadius(yRow);
                int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * rw / spacing));
                for (int a = 0; a < count; a++)
                {
                    int key = row * 4099 + a;
                    float theta = 2f * Mathf.PI * (a + 0.5f * (row & 1) + (RitualUtil.Hash(key) - 0.5f) * 0.9f) / count;
                    float y = yRow + (RitualUtil.Hash(key + 7919) - 0.5f) * dy * 0.95f;
                    if (y < Strata.At(bottom, theta) || y >= Strata.At(top, theta)) continue;
                    float r = rw - RitualUtil.Hash(key + 15485) * 0.0008f;
                    float ct = Mathf.Cos(theta), st = Mathf.Sin(theta);
                    float3 tilt = new float3(RitualUtil.Hash(key + 1) - 0.5f, RitualUtil.Hash(key + 2) - 0.5f, RitualUtil.Hash(key + 3) - 0.5f) * 0.7f;
                    _points.Add(new Point
                    {
                        home = new float3(ct * r, y, st * r),
                        normal = math.normalize(new float3(ct, 0f, st) + tilt),
                        y = y
                    });
                }
            }
        }

        public Layer BeginPour(TimePieceView piece)
        {
            if (!CanAccept(piece.Units)) return null;
            Layer layer = CreateLayer(piece.category, piece.categoryIndex, piece.Units, piece.data.category);
            _pouring = layer;
            _pourPiece = piece;
            _pourTime = 0f;
            _pourSeconds = Mathf.Clamp(_config.pourSecondsMin + layer.units * _config.pourSecondsPerUnit, _config.pourSecondsMin, _config.pourSecondsMax);
            _pourTail = _config.grainFlight * 1.25f + 1f / Mathf.Max(0.2f, _config.landGlowDecay);
            piece.BeginPour(Spout);
            PourStarted?.Invoke(layer);
            return layer;
        }

        public Layer FillInstant(TimeCategoryAsset category, int categoryIndex, int units)
        {
            if (_pouring != null || !Fits(units)) return null;
            Layer layer = CreateLayer(category, categoryIndex, units, category != null ? category.id : "day");
            RitualSplatCloud cloud = layer.cloud;
            float3 rest = new float3(0f, TimeRitualLook.RestLift, 0f);
            int n = cloud.Count;
            for (int i = 0; i < n; i++)
            {
                cloud.disp[i] = rest;
                cloud.vel[i] = float3.zero;
                cloud.scale[i] = 1f;
                cloud.glow[i] = 0f;
                layer.landed[i] = true;
            }
            layer.landedCount = n;
            cloud.MarkSim();
            float limit = WallRadius(Strata.Mean(layer.top)) - _config.grainRadius;
            float3 capRest = new float3(0f, TimeRitualLook.RestLift, 0f);
            for (int i = 0; i < _capRadius.Length; i++)
            {
                float3 h = _cap.home[i];
                _cap.SetHome(i, new float3(h.x, CapHeight(layer.top, i, 0.4f), h.z), _cap.normal[i], _cap.halfSize[i]);
                _cap.disp[i] = capRest;
                _cap.scale[i] = _capRadius[i] <= limit ? 1f : 0f;
            }
            _cap.SetDrawCount(Mathf.RoundToInt(_capRadius.Length * _quality));
            _cap.MarkSim();
            _capSurface.apply = new Vector4(0f, Strata.Mean(layer.top), 0f, _shape.bodyRadius * 1.1f);
            if (_layers.Count == 1)
            {
                for (int i = 0; i < _floorRadius.Length; i++) _floor.scale[i] = 1f;
                _floor.SetDrawCount(Mathf.RoundToInt(_floorRadius.Length * _quality));
                _floor.MarkSim();
            }
            layer.settled = true;
            float[] swap = _surface;
            _surface = _next;
            _next = swap;
            _totalUnits += layer.units;
            _capSurface.previous = null;
            _capSurface.MarkDirty();
            return layer;
        }

        Layer CreateLayer(TimeCategoryAsset category, int categoryIndex, int units, string label)
        {
            float before = RitualFill.HeightAfter(_shape, _config.hoursWhenFull, _totalUnits);
            float after = Mathf.Min(_shape.FillCeiling, RitualFill.HeightAfter(_shape, _config.hoursWhenFull, _totalUnits + units));
            float rise = Mathf.Max(1e-5f, after - before);
            Strata.Raise(_surface, _next, rise, _config.layerBump, _seed++);
            var layer = new Layer
            {
                category = category,
                categoryIndex = categoryIndex,
                units = units,
                bottom = (float[])_surface.Clone(),
                top = (float[])_next.Clone()
            };
            GatherPoints(layer.bottom, layer.top);
            int wanted = _points.Count;
            int budget = Mathf.Max(0, _config.maxGrains - _config.capGrains);
            int share = Mathf.FloorToInt(budget / (float)Mathf.Max(1, Capacity) * units);
            int room = Mathf.Max(0, Mathf.Min(share, budget - _totalGrains - 1));
            float keep = wanted > room ? (float)room / Mathf.Max(1, wanted) : 1f;
            layer.grainScale = 1f / Mathf.Sqrt(Mathf.Max(0.2f, keep));
            Shuffle(_points, _seed * 31 + units);
            int count = Mathf.Min(Mathf.RoundToInt(wanted * keep), Mathf.Min(room, _points.Count));
            if (count == 0)
            {
                _points.Add(new Point { home = new float3(WallRadius(before), before, 0f), normal = new float3(1f, 0f, 0f), y = before });
                count = 1;
            }

            layer.cloud = new RitualSplatCloud("Sand " + label + " " + _layers.Count, root, _splatMaterial, count, 0.4f);
            layer.emit = new float[count];
            layer.flight = new float[count];
            layer.source = new float3[count];
            layer.landed = new bool[count];
            float yLo = float.MaxValue, yHi = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                yLo = Mathf.Min(yLo, _points[i].y);
                yHi = Mathf.Max(yHi, _points[i].y);
            }
            float span = Mathf.Max(1e-5f, yHi - yLo);
            for (int i = 0; i < count; i++)
            {
                Point pt = _points[i];
                int s = layer.cloud.Add(pt.home, pt.normal, _config.grainRadius * layer.grainScale);
                layer.cloud.scale[s] = 0f;
                layer.emit[i] = 0.82f * (pt.y - yLo) / span + 0.14f * RitualUtil.Hash(i * 5 + _seed);
                layer.flight[i] = _config.grainFlight * (0.8f + 0.4f * RitualUtil.Hash(i * 9 + 1));
                float a = RitualUtil.Hash(i * 13 + 5) * 6.2831853f;
                float rr = Mathf.Sqrt(RitualUtil.Hash(i * 17 + 3));
                layer.source[i] = new float3(Mathf.Cos(a) * rr, RitualUtil.Hash(i * 21 + 7), Mathf.Sin(a) * rr);
            }
            layer.surface = new RitualSurface
            {
                material = _lookOf(category),
                look = TimeRitualLook.Sand(_tuning, category, _config.sandDetach, 1f),
                sizeMeters = _config.sandTextureSize,
                triplanarScale = 1f,
                intensity = category != null ? category.sandIntensity : 1.1f,
                aspectMax = _config.scatterAspectMax
            };
            _layers.Add(layer);
            _totalGrains += count;

            for (int i = 0; i < _capFrom.Length; i++) _capFrom[i] = _cap.home[i].y;
            SplatSurfaceMaterial previous = _capSurface.material;
            _capSurface.material = layer.surface.material;
            _capSurface.look = layer.surface.look;
            _capSurface.intensity = layer.surface.intensity;
            _capSurface.previous = previous != null && previous != _capSurface.material ? previous : null;
            _capSurface.apply = new Vector4(0f, Strata.Mean(layer.bottom), 0f, 0f);
            _capSurface.applyShape = new Vector4(0.01f, 0f, 0.3f, 0f);
            _capSurface.MarkDirty();
            if (_layers.Count == 1)
            {
                _floorSurface.material = layer.surface.material;
                _floorSurface.look = layer.surface.look;
                _floorSurface.intensity = layer.surface.intensity;
                _floorSurface.previous = null;
                _floorSurface.apply = SplatSurfaceState.NoApply;
                _floorSurface.MarkDirty();
            }

            return layer;
        }

        static void Shuffle(List<Point> list, int seed)
        {
            uint state = (uint)seed * 2654435761u + 1u;
            for (int i = list.Count - 1; i > 0; i--)
            {
                state = state * 1664525u + 1013904223u;
                int j = (int)(state % (uint)(i + 1));
                Point tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        float CapHeight(float[] boundary, int i, float fill)
        {
            float r = _capRadius[i];
            float wall = Strata.At(boundary, _capAngle[i]);
            float rMax = WallRadius(wall);
            float k = Mathf.Clamp01(r / Mathf.Max(1e-4f, rMax));
            return wall + _config.moundHeight * fill * (1f - k * k) - 0.0006f;
        }

        void StepPour(float dt)
        {
            Layer layer = _pouring;
            _pourTime += dt;
            float progress = Mathf.Clamp01(_pourTime / _pourSeconds);
            if (_pourPiece != null && _pourPiece.Alive) _pourPiece.PourStep(progress, Spout, dt);
            float meltRadius = _pourPiece != null && _pourPiece.Alive ? _pourPiece.MeltRadius : _config.barRadius;

            float3x3 rot = new float3x3((quaternion)root.rotation);
            float fraction = layer.cloud.Count > 0 ? (float)layer.landedCount / layer.cloud.Count : 1f;
            float landY = Mathf.Lerp(Strata.Mean(layer.bottom), Strata.Mean(layer.top), fraction) + _config.moundHeight;
            float3 spout = new float3(0f, _shape.height + _config.spoutHeight, 0f);
            float decay = _config.landGlowDecay * dt;
            float invDt = 1f / Mathf.Max(dt, 1e-4f);
            RitualSplatCloud cloud = layer.cloud;
            float3[] disp = cloud.disp, vel = cloud.vel, home = cloud.home;
            float[] scale = cloud.scale, glow = cloud.glow;
            int n = cloud.Count;
            int landedNow = 0;
            float3 rest = new float3(0f, TimeRitualLook.RestLift, 0f);
            for (int i = 0; i < n; i++)
            {
                if (layer.landed[i])
                {
                    if (glow[i] > 0f) glow[i] = math.max(0f, glow[i] - decay);
                    continue;
                }
                float tau = (_pourTime - layer.emit[i] * _pourSeconds) / layer.flight[i];
                if (tau <= 0f) continue;
                float3 h = home[i];
                if (tau >= 1f)
                {
                    layer.landed[i] = true;
                    layer.landedCount++;
                    landedNow++;
                    disp[i] = rest;
                    vel[i] = float3.zero;
                    scale[i] = 1f;
                    glow[i] = _config.landGlow;
                    continue;
                }
                float3 src = spout + new float3(layer.source[i].x * meltRadius, (layer.source[i].y - 0.5f) * 0.012f, layer.source[i].z * meltRadius);
                float3 land = new float3(layer.source[i].z * _config.streamRadius, landY, layer.source[i].x * _config.streamRadius);
                float3 p;
                if (tau < 0.62f)
                {
                    float s = tau / 0.62f;
                    float gather = RitualUtil.Smooth(math.min(1f, s * 1.8f));
                    p = new float3(math.lerp(src.x, land.x, gather), math.lerp(src.y, land.y, s * s), math.lerp(src.z, land.z, gather));
                }
                else
                {
                    float s = (tau - 0.62f) / 0.38f;
                    float e = 1f - (1f - s) * (1f - s);
                    p = math.lerp(land, h, e);
                    p.y += math.sin(s * math.PI) * 0.003f;
                }
                float3 d = math.mul(rot, p - h);
                vel[i] = (d - disp[i]) * invDt * (scale[i] > 0f ? 1f : 0f);
                disp[i] = d;
                scale[i] = math.lerp(0.55f, 1f, tau);
                glow[i] = 0.35f;
            }
            if (landedNow > 0) GrainsLanded?.Invoke(layer, landedNow);
            cloud.MarkSim();

            fraction = n > 0 ? (float)layer.landedCount / n : 1f;
            float capFill = RitualUtil.Smooth(fraction);
            float mound = 1f - 0.6f * RitualUtil.Smooth(Mathf.Clamp01((_pourTime - _pourSeconds) / Mathf.Max(0.1f, _pourTail)));
            int capCount = _capRadius.Length;
            float limit = WallRadius(Mathf.Lerp(Strata.Mean(layer.bottom), Strata.Mean(layer.top), capFill)) - _config.grainRadius;
            float reach = _shape.bodyRadius * 1.1f * RitualUtil.Smooth(Mathf.Clamp01(fraction * 1.15f));
            if (_layers.Count == 1) limit = Mathf.Min(limit, reach);
            float3 capRest = new float3(0f, TimeRitualLook.RestLift, 0f);
            for (int i = 0; i < capCount; i++)
            {
                float target = CapHeight(layer.top, i, mound);
                float y = Mathf.Lerp(_capFrom[i], target, capFill);
                float3 h = _cap.home[i];
                _cap.SetHome(i, new float3(h.x, y, h.z), _cap.normal[i], _cap.halfSize[i]);
                _cap.disp[i] = capRest;
                _cap.scale[i] = _capRadius[i] <= limit ? 1f : 0f;
            }
            _cap.SetDrawCount(Mathf.RoundToInt(capCount * _quality));
            _cap.MarkSim();
            float capMeanY = Mathf.Lerp(Strata.Mean(layer.bottom), Strata.Mean(layer.top), capFill);
            _capSurface.apply = new Vector4(0f, capMeanY, 0f, reach);
            if (_layers.Count == 1)
            {
                int floorCount = _floorRadius.Length;
                float floorLimit = Mathf.Min(WallRadius(_shape.floor), reach);
                for (int i = 0; i < floorCount; i++)
                {
                    _floor.scale[i] = _floorRadius[i] <= floorLimit ? 1f : 0f;
                }
                _floor.SetDrawCount(Mathf.RoundToInt(floorCount * _quality));
                _floor.MarkSim();
            }

            if (_pourTime >= _pourSeconds + _pourTail && layer.landedCount >= n)
            {
                for (int i = 0; i < n; i++) glow[i] = 0f;
                cloud.MarkSim();
                if (_layers.Count == 1)
                {
                    for (int i = 0; i < _floorRadius.Length; i++) _floor.scale[i] = 1f;
                    _floor.MarkSim();
                }
                layer.settled = true;
                float[] swap = _surface;
                _surface = _next;
                _next = swap;
                _totalUnits += layer.units;
                _capSurface.previous = null;
                _capSurface.MarkDirty();
                _pouring = null;
                _pourPiece = null;
                PourFinished?.Invoke(layer);
            }
        }

        public void SetLevel(float level)
        {
            _level = level;
        }

        public void SetQuality(float quality)
        {
            quality = Mathf.Clamp(quality, 0.25f, 1f);
            if (Mathf.Approximately(quality, _quality)) return;
            _quality = quality;
            float grain = 1f / Mathf.Sqrt(quality);
            for (int i = 0; i < _layers.Count; i++)
            {
                Layer layer = _layers[i];
                layer.cloud.SetDrawCount(Mathf.Max(1, Mathf.RoundToInt(layer.cloud.Count * quality)));
                layer.surface.look.grain = grain;
                layer.surface.MarkDirty();
            }
            if (_cap.DrawCount > 0) _cap.SetDrawCount(Mathf.RoundToInt(_capRadius.Length * quality));
            _capSurface.look.grain = grain;
            _capSurface.MarkDirty();
            if (_floor.DrawCount > 0) _floor.SetDrawCount(Mathf.RoundToInt(_floorRadius.Length * quality));
            _floorSurface.look.grain = grain;
            _floorSurface.MarkDirty();
        }

        public void SetInnerGlow(Color glow)
        {
            if (glow == _innerGlow) return;
            _innerGlow = glow;
            _glassDirty = true;
        }

        public void SetPulse(float pulse)
        {
            if (Mathf.Approximately(pulse, _pulse)) return;
            _pulse = pulse;
            _glassDirty = true;
        }

        public Color LayerTint(int index)
        {
            Layer layer = _layers[index];
            return layer.category != null ? layer.category.glowColor : Color.white;
        }

        public void Tick(float now, float dt)
        {
            if (_pouring != null) StepPour(Mathf.Min(dt, 1f / 45f));
            for (int i = 0; i < _layers.Count; i++)
            {
                Layer layer = _layers[i];
                layer.cloud.Flush();
                layer.surface.Apply(layer.cloud.renderer, layer.cloud.SimTexture, now, _level);
            }
            _cap.Flush();
            if (_capSurface.material != null) _capSurface.Apply(_cap.renderer, _cap.SimTexture, now, _level);
            _floor.Flush();
            if (_floorSurface.material != null) _floorSurface.Apply(_floor.renderer, _floor.SimTexture, now, _level);
            if (_glass != null && _glassDirty)
            {
                _glass.GetPropertyBlock(_glassBlock);
                _glassBlock.SetColor(InnerId, _innerGlow);
                _glassBlock.SetFloat(RimId, _config.glassRim);
                _glassBlock.SetFloat(SpecId, _config.glassSpec);
                _glassBlock.SetFloat(BodyId, _config.glassBody);
                _glassBlock.SetFloat(RadiusId, _config.glassSplatRadius);
                _glassBlock.SetFloat(PulseId, _pulse);
                _glass.SetPropertyBlock(_glassBlock);
                _glassDirty = false;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _layers.Count; i++) _layers[i].cloud.Dispose();
            _layers.Clear();
            _cap?.Dispose();
            _floor?.Dispose();
            RitualUtil.Kill(_glassMesh);
            if (root != null) RitualUtil.Kill(root.gameObject);
        }
    }
}

// First Light (setup-firstlight) — 기기 스크린샷 한 장에 넷이 같이 보이면 통과:
//   1) 방(패스스루)  2) 정면 ~60cm Unlit 큐브  3) PCA 카메라 텍스처 쿼드  4) 손끝 점 (컨트롤러 꺼짐)
// 가드레일 §3·§4·§5·§6: 머티리얼은 SerializeField, 권한 요청은 OVRManager 한 곳, 해상도는 CurrentResolution 을 읽는다.
using System.Collections;
using Meta.XR;
using UnityEngine;
using UnityEngine.XR.Management;

public class FirstLight : MonoBehaviour
{
    const string Tag = "[MP]";

    [SerializeField] OVRCameraRig rig;
    [SerializeField] PassthroughCameraAccess cameraAccess;
    [SerializeField] Transform cube;
    [SerializeField] Renderer cameraQuad;
    [SerializeField] OVRSkeleton leftHand;
    [SerializeField] OVRSkeleton rightHand;
    [SerializeField] Transform leftTip;
    [SerializeField] Transform rightTip;
    [SerializeField] float cubeDistance = 0.6f;
    [SerializeField] float quadWidth = 0.24f;

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    bool _textureBound;
    float _nextStatus;
    int _leftTipIndex = -1, _rightTipIndex = -1;

    IEnumerator Start()
    {
        leftTip.gameObject.SetActive(false);
        rightTip.gameObject.SetActive(false);

        yield return null; // 첫 프레임 뒤에야 머리 자세가 들어온다 (§4)
        PlaceInFront();
        LogStartup();
    }

    void PlaceInFront()
    {
        Transform head = rig.centerEyeAnchor;
        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up); // yaw-only
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        cube.position = head.position + fwd * cubeDistance - Vector3.up * 0.05f;
        cube.rotation = Quaternion.LookRotation(fwd);

        // 쿼드는 큐브 오른쪽 위. 앞면이 사용자 쪽을 보게
        Transform q = cameraQuad.transform;
        q.position = cube.position + right * 0.22f + Vector3.up * 0.12f;
        q.rotation = Quaternion.LookRotation(fwd);
        q.localScale = new Vector3(quadWidth, quadWidth * 0.75f, 1f); // 해상도 받기 전 4:3
    }

    void LogStartup()
    {
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        string loader = mgr != null && mgr.activeLoader != null ? mgr.activeLoader.name : "none";
        var cam = rig.centerEyeAnchor.GetComponent<Camera>();
        bool pt = OVRManager.instance != null && OVRManager.instance.isInsightPassthroughEnabled;
        Debug.Log($"{Tag} loader={loader} passthrough={pt} layer={FindAnyObjectByType<OVRPassthroughLayer>() != null} " +
                  $"clear={cam.clearFlags} bgAlpha={cam.backgroundColor.a:0.00} handTracking={OVRPlugin.GetHandTrackingEnabled()}");
        Debug.Log($"{Tag} pca requested={cameraAccess.RequestedResolution} enabled={cameraAccess.enabled}");
    }

    void Update()
    {
        BindCameraTexture();
        UpdateTip(leftHand, leftTip, ref _leftTipIndex);
        UpdateTip(rightHand, rightTip, ref _rightTipIndex);

        if (Time.unscaledTime >= _nextStatus)
        {
            _nextStatus = Time.unscaledTime + 5f;
            Debug.Log($"{Tag} status pca={(cameraAccess.IsPlaying ? "playing " + cameraAccess.CurrentResolution : "waiting")} " +
                      $"hands L={leftTip.gameObject.activeSelf} R={rightTip.gameObject.activeSelf} fps={1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0}");
        }
    }

    // IsPlaying 은 권한이 승인되고 카메라가 돌기 시작한 뒤에만 true. 그 전 GetTexture() 는 에러 로그 + null
    void BindCameraTexture()
    {
        if (_textureBound || !cameraAccess.IsPlaying) return;
        Texture tex = cameraAccess.GetTexture();
        if (tex == null) return;
        cameraQuad.material.SetTexture(BaseMap, tex);
        Vector2Int res = cameraAccess.CurrentResolution;
        if (res.x > 0) cameraQuad.transform.localScale = new Vector3(quadWidth, quadWidth * res.y / res.x, 1f);
        _textureBound = true;
        Debug.Log($"{Tag} pca CurrentResolution={res.x}x{res.y} texture={tex.width}x{tex.height}");
    }

    static void UpdateTip(OVRSkeleton skel, Transform dot, ref int index)
    {
        bool ok = skel != null && skel.IsInitialized && skel.IsDataValid && skel.Bones != null;
        if (ok && index < 0)
        {
            for (int i = 0; i < skel.Bones.Count; i++)
                if (skel.Bones[i].Id == OVRSkeleton.BoneId.XRHand_IndexTip) { index = i; break; }
        }
        ok = ok && index >= 0 && index < skel.Bones.Count;
        if (dot.gameObject.activeSelf != ok) dot.gameObject.SetActive(ok);
        if (ok) dot.position = skel.Bones[index].Transform.position;
    }
}

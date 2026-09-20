using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 右コントローラー(既定)の人差し指トリガーで撃つ銃。
/// このコンポーネントを空の GameObject に付けるだけで、
/// 起動時に OVRCameraRig のコントローラーアンカーの子になり、
/// 銃本体と照準の線を自動で作る。
///
/// Unity Editor でヘッドセットなしに Play したときだけ、
/// マウスの左クリックで画面の中心方向へ撃てる(動作確認用)。
/// </summary>
[DisallowMultipleComponent]
public class VRGun : MonoBehaviour
{
    [SerializeField] OVRInput.Controller controller = OVRInput.Controller.RTouch;
    [SerializeField] int damage = 1;
    [SerializeField] float range = 50f;
    [SerializeField] float fireInterval = 0.15f;
    [Tooltip("銃口の向きの微調整(度)。プラスで下向き。")]
    [SerializeField] float muzzlePitchDegrees = 0f;
    [SerializeField] LayerMask hitMask = ~0;
    [Tooltip("Editor でヘッドセットがないとき、マウスクリックで撃てるようにする。")]
    [SerializeField] bool editorMouseFallback = true;

    const float AimLineMaxLength = 20f;
    static readonly Color AimColor = new Color(1f, 0.1f, 0.1f);
    static readonly Color FireColor = new Color(1f, 0.9f, 0.2f);

    Transform aimLine;
    Renderer aimLineRenderer;
    Camera eyeCamera;
    float nextFireTime;
    float fireFlashUntil;

    void Start()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("VRGun: シーンに OVRCameraRig が見つかりません。Camera Rig の Building Block を追加してください。");
            enabled = false;
            return;
        }

        eyeCamera = rig.centerEyeAnchor.GetComponent<Camera>();

        Transform anchor = controller == OVRInput.Controller.LTouch
            ? rig.leftHandAnchor
            : rig.rightHandAnchor;

        transform.SetParent(anchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(muzzlePitchDegrees, 0f, 0f);

        BuildVisuals();
    }

    void BuildVisuals()
    {
        // 銃本体(小さな箱)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "GunBody";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.03f, 0.03f, 0.12f);
        body.transform.localPosition = new Vector3(0f, 0f, 0.06f);
        body.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.15f);

        // 照準の線(細長い箱)
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "AimLine";
        Destroy(line.GetComponent<Collider>());
        line.transform.SetParent(transform, false);
        aimLine = line.transform;
        aimLineRenderer = line.GetComponent<Renderer>();
        aimLineRenderer.material.color = AimColor;
    }

    void Update()
    {
        if (aimLine == null) return;

        Ray ray;
        bool triggerPressed;

        if (TryGetEditorMouseInput(out ray, out triggerPressed))
        {
            // Editor の動作確認中は、照準の線は出さない
            aimLine.gameObject.SetActive(false);
        }
        else
        {
            aimLine.gameObject.SetActive(true);
            ray = new Ray(transform.position, transform.forward);
            triggerPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller);
        }

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore);

        if (aimLine.gameObject.activeSelf)
        {
            UpdateAimLine(hasHit ? Mathf.Min(hit.distance, AimLineMaxLength) : AimLineMaxLength);
        }

        if (triggerPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireInterval;
            Fire(hasHit, hit);
        }
    }

    /// <summary>Editor でヘッドセットがないときだけ、マウスで撃つ入力を返す。</summary>
    bool TryGetEditorMouseInput(out Ray ray, out bool fire)
    {
        ray = default;
        fire = false;
#if UNITY_EDITOR
        if (!editorMouseFallback || OVRManager.isHmdPresent) return false;

        Mouse mouse = Mouse.current;
        if (mouse == null || eyeCamera == null) return false;

        ray = eyeCamera.ScreenPointToRay(mouse.position.ReadValue());
        fire = mouse.leftButton.wasPressedThisFrame;
        return true;
#else
        return false;
#endif
    }

    void UpdateAimLine(float length)
    {
        const float thickness = 0.004f;
        aimLine.localScale = new Vector3(thickness, thickness, length);
        aimLine.localPosition = new Vector3(0f, 0f, length * 0.5f + 0.12f);
        aimLineRenderer.material.color = Time.time < fireFlashUntil ? FireColor : AimColor;
    }

    void Fire(bool hasHit, RaycastHit hit)
    {
        fireFlashUntil = Time.time + 0.05f;
        StartCoroutine(Haptic());

        if (!hasHit) return;
        Target target = hit.collider.GetComponentInParent<Target>();
        if (target != null) target.TakeDamage(damage);
    }

    IEnumerator Haptic()
    {
        OVRInput.SetControllerVibration(1f, 0.5f, controller);
        yield return new WaitForSeconds(0.05f);
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }
}

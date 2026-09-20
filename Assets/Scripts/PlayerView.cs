using UnityEngine;

/// <summary>
/// プレイヤーの頭・手・床の高さなどを、実機と Editor での動作確認で同じように扱うための窓口。
///
/// 実機(Quest 2)では、OVRCameraRig の頭と手をそのまま使う。
/// Editor でヘッドセットなしに Play したときは、カメラの位置や向きが安定しないため、
/// 目の高さ 1.6m に固定した専用のカメラ(EditorTestCamera)を作って、それを頭として扱う。
/// </summary>
public static class PlayerView
{
    public static OVRCameraRig Rig { get; private set; }
    /// <summary>頭(目)の位置と向き。</summary>
    public static Transform Eye { get; private set; }
    public static Camera EyeCamera { get; private set; }
    public static Transform LeftHand { get; private set; }
    public static Transform RightHand { get; private set; }
    /// <summary>Editor でヘッドセットなしに動作確認しているときは true。</summary>
    public static bool IsEditorSimulation { get; private set; }

    /// <summary>足元の中心(水平位置)。的を出す基準になる。</summary>
    public static Vector3 Center { get; private set; }
    public static float FloorY { get; private set; }
    /// <summary>正面の向き(水平方向のみ)。</summary>
    public static Vector3 FlatForward { get; private set; }

    const float EditorEyeHeight = 1.6f;
    /// <summary>Editor の固定カメラを、砦の下の敵が見えるように、少し下へ向ける角度(度)。</summary>
    const float EditorPitchDegrees = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Rig = null;
        Eye = null;
        EyeCamera = null;
        LeftHand = null;
        RightHand = null;
        IsEditorSimulation = false;
    }

    /// <summary>準備できていなければ準備する。OVRCameraRig がなければ false。</summary>
    public static bool Ensure()
    {
        if (Rig != null && Eye != null) return true;

        Rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
        if (Rig == null)
        {
            Debug.LogError("PlayerView: シーンに OVRCameraRig が見つかりません。Camera Rig の Building Block を追加してください。");
            return false;
        }

        LeftHand = Rig.leftHandAnchor;
        RightHand = Rig.rightHandAnchor;
        Eye = Rig.centerEyeAnchor;
        EyeCamera = Eye.GetComponent<Camera>();

        Center = Rig.transform.position;
        FloorY = Rig.transform.position.y;
        FlatForward = Vector3.ProjectOnPlane(Rig.transform.forward, Vector3.up).normalized;
        if (FlatForward == Vector3.zero) FlatForward = Vector3.forward;

#if UNITY_EDITOR
        if (!OVRManager.isHmdPresent) SetUpEditorCamera();
#endif
        return true;
    }

#if UNITY_EDITOR
    static void SetUpEditorCamera()
    {
        IsEditorSimulation = true;

        // 元のカメラは、位置が安定しないので使わない
        if (EyeCamera != null) EyeCamera.enabled = false;

        var go = new GameObject("EditorTestCamera");
        go.transform.SetPositionAndRotation(
            Rig.transform.position + Vector3.up * EditorEyeHeight,
            Rig.transform.rotation * Quaternion.Euler(EditorPitchDegrees, 0f, 0f));

        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 75f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 400f;
        cam.depth = 100f;   // 元のカメラより手前に描く
        cam.clearFlags = CameraClearFlags.Skybox;

        Eye = go.transform;
        EyeCamera = cam;
        Debug.Log("[PlayerView] Editor 用の固定カメラ(EditorTestCamera)を使います。");
    }
#endif
}

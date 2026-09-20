using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 片手ぶんの銃。GunSystem が左右 2 丁ぶん、起動時に自動で作る。
/// このクラスは「見た目」「トリガーの状態」「狙っている先の判定」を担当し、
/// 撃つ・弾を減らす・威力を決める、といった判断は GunSystem が行う。
///
/// Unity Editor でヘッドセットなしに Play したときだけ、
/// 銃は画面の右下(右手)と左下(左手)に固定され、マウスカーソルの方向を向く。
/// マウスの左ボタンが右手のトリガー、右ボタンが左手のトリガーになる。
/// </summary>
public class VRGun : MonoBehaviour
{
    const float AimLineMaxLength = 20f;
    const float AimLineThickness = 0.004f;
    const float AimLineChargedThickness = 0.012f;
    // 銃口(先端)の位置。銃本体の長さに合わせている。
    const float MuzzleZ = 0.24f;

    // Editor の動作確認中に、銃を置くカメラからの相対位置(右・下・前)
    // 横の位置は、GunSystem.EditorHandHalfGap(ふだんは離れ、Space で中央に寄る)
    const float EditorDown = -0.2f;
    const float EditorForward = 0.5f;

    static readonly Color AimColor = new Color(1f, 0.1f, 0.1f);
    static readonly Color AimChargedColor = new Color(1f, 0.6f, 0.1f);
    static readonly Color AimComboColor = new Color(0.4f, 1f, 1f);
    static readonly Color FireColor = new Color(1f, 0.9f, 0.2f);
    static readonly Color BarStartColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color BarFullColor = new Color(0.5f, 1f, 1f);

    // グレード 1〜5 の銃身の色(灰 → 緑 → 青 → 紫 → 金)
    static readonly Color[] GradeColors =
    {
        new Color(0.35f, 0.38f, 0.42f),
        new Color(0.2f, 0.7f, 0.3f),
        new Color(0.2f, 0.45f, 0.95f),
        new Color(0.6f, 0.3f, 0.9f),
        new Color(1f, 0.8f, 0.2f),
    };

    GunSystem system;
    bool isRight;
    OVRInput.Controller controller;
    bool initialized;

    Transform aimLine;
    Renderer aimLineRenderer;
    Renderer barrelRenderer;
    Transform chargeBar;
    Renderer chargeBarRenderer;
    TextMesh label;

    bool comboGlow;
    bool lastHeld;
    float chargeStartTime;
    float cooldownUntil;
    float fireFlashUntil;

    string statusText = "";
    Color statusColor = Color.white;
    string messageText = "";
    Color messageColor = Color.white;
    float messageUntil;
    string shownText;
    Color shownColor;

    public bool IsRight => isRight;
    /// <summary>トリガーを引いているか。</summary>
    public bool Held { get; private set; }
    /// <summary>チャージ中か。</summary>
    public bool Charging { get; private set; }
    /// <summary>トリガーを離すまで、次のチャージを始められない状態か。</summary>
    public bool NeedsRelease { get; set; }
    public bool CooldownOver => Time.time >= cooldownUntil;

    /// <summary>今の狙いの先に、何かあるか。</summary>
    public bool HasHit { get; private set; }
    public RaycastHit LastHit;

    public Vector3 Origin => transform.position;
    /// <summary>銃口(先端)の位置。</summary>
    public Vector3 Muzzle => transform.TransformPoint(0f, 0f, MuzzleZ);
    public Vector3 Forward => transform.forward;

    /// <summary>チャージの量(0〜1)。</summary>
    public float Charge01
    {
        get
        {
            if (!Charging) return 0f;
            float t = (Time.time - chargeStartTime) / Mathf.Max(0.01f, system.FullChargeSeconds);
            return Mathf.Clamp01(t);
        }
    }

    public void Init(GunSystem owner, bool right, Transform anchor)
    {
        system = owner;
        isRight = right;
        controller = right ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;

        transform.SetParent(anchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(system.MuzzlePitchDegrees, 0f, 0f);

        BuildVisuals();
        initialized = true;
    }

    void OnDisable()
    {
        // チャージ中に止まっても、コントローラーが震え続けないようにする
        if (initialized && Charging) OVRInput.SetControllerVibration(0f, 0f, controller);
        Charging = false;
    }

    // ---- 見た目 ----

    void BuildVisuals()
    {
        // 銃身(グレードで色が変わる)
        var barrel = AddPart("Barrel", new Vector3(0.035f, 0.045f, MuzzleZ),
            new Vector3(0f, 0f, MuzzleZ * 0.5f), Vector3.zero, GradeColors[0]);
        barrelRenderer = barrel.GetComponent<Renderer>();

        // 持ち手(少し後ろに傾ける)
        AddPart("Grip", new Vector3(0.03f, 0.09f, 0.04f),
            new Vector3(0f, -0.06f, 0.03f), new Vector3(-15f, 0f, 0f), new Color(0.15f, 0.15f, 0.17f));

        // 先端の照準(明るい色で目立たせる)
        AddPart("FrontSight", new Vector3(0.015f, 0.025f, 0.02f),
            new Vector3(0f, 0.035f, MuzzleZ - 0.02f), Vector3.zero, new Color(0.1f, 0.9f, 1f));

        // チャージの量を示すバー(銃身の上。チャージ中だけ伸びる)
        var bar = AddPart("ChargeBar", new Vector3(0.012f, 0.01f, 1f),
            new Vector3(0f, 0.033f, 0f), Vector3.zero, BarStartColor);
        chargeBar = bar.transform;
        chargeBarRenderer = bar.GetComponent<Renderer>();
        bar.SetActive(false);

        // 照準の線(細長い箱)
        var line = AddPart("AimLine", new Vector3(AimLineThickness, AimLineThickness, 1f),
            new Vector3(0f, 0f, MuzzleZ), Vector3.zero, AimColor);
        aimLine = line.transform;
        aimLineRenderer = line.GetComponent<Renderer>();

        // 残弾とグレードを表示する文字(銃の上に浮かび、いつも顔のほうを向く)
        label = HudText.Create("GunLabel", transform, 64, 0.006f, Color.white);
        label.transform.localPosition = new Vector3(0f, 0.11f, 0.06f);
    }

    GameObject AddPart(string partName, Vector3 scale, Vector3 localPos, Vector3 localEuler, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        Destroy(part.GetComponent<Collider>());   // 狙いの判定に邪魔にならないように
        part.transform.SetParent(transform, false);
        part.transform.localScale = scale;
        part.transform.localPosition = localPos;
        part.transform.localEulerAngles = localEuler;
        part.GetComponent<Renderer>().material.color = color;
        return part;
    }

    public void SetGrade(int grade)
    {
        int index = Mathf.Clamp(grade - 1, 0, GradeColors.Length - 1);
        if (barrelRenderer != null) barrelRenderer.material.color = GradeColors[index];
    }

    public void SetComboGlow(bool on) { comboGlow = on; }

    public void SetStatus(string text, Color color)
    {
        statusText = text;
        statusColor = color;
    }

    public void ShowMessage(string text, Color color, float seconds)
    {
        messageText = text;
        messageColor = color;
        messageUntil = Time.time + seconds;
    }

    // ---- 毎フレームの処理 ----

    void Update()
    {
        if (!initialized) return;

        // 1. 狙う向きと、トリガーを引いているかどうかを決める
        Ray ray;
        bool held;
        bool mouseMode = TryGetEditorMouseInput(out ray, out held);
        if (!mouseMode)
        {
            ray = new Ray(transform.position, transform.forward);
            held = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
        }
        Held = held;
        if (!Held) NeedsRelease = false;

        // トリガーの入りと切りを記録する(動作確認用)
        if (held != lastHeld)
        {
            string hand = isRight ? "右" : "左";
            Debug.Log($"[VRGun] {hand}手のトリガー {(held ? "ON" : "OFF")}(マウスモード={mouseMode})");
            lastHeld = held;
        }

        // 2. 何に当たるか調べる
        RaycastHit hit;
        HasHit = Physics.Raycast(ray, out hit, system.Range, system.HitMask, QueryTriggerInteraction.Ignore);
        LastHit = hit;
        float rayLength = HasHit ? hit.distance : AimLineMaxLength;

        // 3. 銃の向きと照準線の長さ
        float lineLength;
        if (mouseMode)
        {
            // 銃を画面の下に置き、マウスの狙っている点へ向ける
            Vector3 aimPoint = ray.origin + ray.direction * rayLength;
            PoseGunForEditor(aimPoint);
            lineLength = Vector3.Distance(transform.TransformPoint(0f, 0f, MuzzleZ), aimPoint);
        }
        else
        {
            lineLength = rayLength - MuzzleZ;
        }

        float charge01 = Charge01;
        UpdateAimLine(Mathf.Clamp(lineLength, 0.01f, AimLineMaxLength), charge01);
        UpdateChargeBar(charge01);
        UpdateLabel();

        // チャージ中:量に応じて、弱く震える
        if (Charging)
        {
            OVRInput.SetControllerVibration(0.5f, 0.05f + 0.3f * charge01, controller);
        }
    }

    /// <summary>Editor でヘッドセットがないときだけ、マウスで撃つ入力を返す。</summary>
    bool TryGetEditorMouseInput(out Ray ray, out bool held)
    {
        ray = default;
        held = false;
#if UNITY_EDITOR
        if (!PlayerView.IsEditorSimulation || !system.EditorMouseFallback) return false;

        Mouse mouse = Mouse.current;
        Camera cam = PlayerView.EyeCamera;
        if (mouse == null || cam == null) return false;

        ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Keyboard keyboard = Keyboard.current;
        if (isRight)
        {
            held = mouse.leftButton.isPressed;
        }
        else
        {
            // 左手の銃:マウスの右ボタン、または Z キー(右クリックが効かない環境向け)
            held = mouse.rightButton.isPressed || (keyboard != null && keyboard.zKey.isPressed);
        }
        return true;
#else
        return false;
#endif
    }

    /// <summary>Editor の動作確認中、銃をカメラの下前方に置き、狙う点へ向ける。</summary>
    void PoseGunForEditor(Vector3 aimPoint)
    {
        Transform cam = PlayerView.Eye;
        float side = isRight ? system.EditorHandHalfGap : -system.EditorHandHalfGap;
        Vector3 pos = cam.TransformPoint(new Vector3(side, EditorDown, EditorForward));
        Vector3 toAim = aimPoint - pos;
        if (toAim.sqrMagnitude < 0.0001f) toAim = cam.forward;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toAim));
    }

    void UpdateAimLine(float length, float charge01)
    {
        float thickness = Mathf.Lerp(AimLineThickness, AimLineChargedThickness, charge01);
        aimLine.localScale = new Vector3(thickness, thickness, length);
        aimLine.localPosition = new Vector3(0f, 0f, MuzzleZ + length * 0.5f);

        Color color = Color.Lerp(AimColor, AimChargedColor, charge01);
        if (comboGlow) color = AimComboColor;
        if (Time.time < fireFlashUntil) color = FireColor;
        aimLineRenderer.material.color = color;
    }

    void UpdateChargeBar(float charge01)
    {
        bool show = Charging && charge01 > 0.01f;
        if (chargeBar.gameObject.activeSelf != show) chargeBar.gameObject.SetActive(show);
        if (!show) return;

        float length = MuzzleZ * charge01;
        chargeBar.localScale = new Vector3(0.012f, 0.01f, length);
        chargeBar.localPosition = new Vector3(0f, 0.033f, length * 0.5f);
        chargeBarRenderer.material.color = Color.Lerp(BarStartColor, BarFullColor, charge01 * charge01);
    }

    void UpdateLabel()
    {
        bool showMessage = Time.time < messageUntil;
        string text = showMessage ? messageText : statusText;
        Color color = showMessage ? messageColor : statusColor;

        // 変わったときだけ書き換える(文字の作り直しを減らすため)
        if (text != shownText) { label.text = text; shownText = text; }
        if (color != shownColor) { label.color = color; shownColor = color; }

        // 文字がいつも顔のほうを向くようにする
        Vector3 fromEye = label.transform.position - PlayerView.Eye.position;
        if (fromEye.sqrMagnitude > 0.0001f) label.transform.rotation = Quaternion.LookRotation(fromEye);
    }

    // ---- GunSystem から呼ばれる操作 ----

    public void BeginCharge()
    {
        Charging = true;
        chargeStartTime = Time.time;
    }

    /// <summary>チャージを終えて、その時点のチャージ量(0〜1)を返す。</summary>
    public float EndCharge()
    {
        float charge01 = Charge01;
        Charging = false;
        OVRInput.SetControllerVibration(0f, 0f, controller);
        return charge01;
    }

    /// <summary>撃ったときの演出(照準線の色、振動)と、次のチャージまでの待ち時間を設定する。</summary>
    public void PlayShotFeedback(float charge01, float cooldownSeconds)
    {
        cooldownUntil = Time.time + cooldownSeconds;
        fireFlashUntil = Time.time + 0.06f + 0.1f * charge01;
        StartCoroutine(Haptic(0.4f + 0.6f * charge01, 0.05f + 0.1f * charge01));
    }

    /// <summary>弾がないときの演出。</summary>
    public void PlayEmpty()
    {
        ShowMessage("NO AMMO", new Color(1f, 0.3f, 0.3f), 0.8f);
        StartCoroutine(Haptic(0.2f, 0.03f));
    }

    /// <summary>コントローラーを短く震わせる(砦が攻撃されたときなど)。</summary>
    public void PlayHapticPulse(float amplitude, float seconds)
    {
        StartCoroutine(Haptic(amplitude, seconds));
    }

    public void PlayPickupFeedback()
    {
        StartCoroutine(Haptic(0.6f, 0.08f));
    }

    IEnumerator Haptic(float amplitude, float seconds)
    {
        OVRInput.SetControllerVibration(1f, amplitude, controller);
        yield return new WaitForSeconds(seconds);
        // 次のチャージが始まっていたら、その震えは止めない
        if (!Charging) OVRInput.SetControllerVibration(0f, 0f, controller);
    }
}

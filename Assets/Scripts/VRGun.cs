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
    // バズーカにしたときの銃口(筒の先端)の位置と、持ち替えたときの「ぽん」と大きくなる動きの長さ
    const float BazookaMuzzleZ = 0.48f;
    const float SwitchPopSeconds = 0.25f;

    // Editor の動作確認中に、銃を置くカメラからの相対位置(右・下・前)
    // 横の位置は、GunSystem.EditorHandHalfGap(ふだんは離れ、Space で中央に寄る)
    const float EditorDown = -0.2f;
    const float EditorForward = 0.5f;

    static readonly Color AimColor = new Color(1f, 0.1f, 0.1f);
    static readonly Color AimChargedColor = new Color(1f, 0.6f, 0.1f);
    static readonly Color AimComboColor = new Color(0.4f, 1f, 1f);
    static readonly Color FireColor = new Color(1f, 0.9f, 0.2f);
    static readonly Color SightColor = new Color(0.1f, 0.9f, 1f);
    static readonly Color LinkGlow = new Color(0.3f, 0.95f, 1f);
    static readonly Color LinkSteel = new Color(0.18f, 0.2f, 0.27f);
    static readonly Color GoldTrim = new Color(1f, 0.78f, 0.3f);
    static readonly Color GunSteel = new Color(0.17f, 0.18f, 0.23f);
    static readonly Color LeatherColor = new Color(0.28f, 0.16f, 0.10f);
    static readonly Color RuneGlow = new Color(0.3f, 1.2f, 2.2f);
    static readonly Color BazookaSteel = new Color(0.2f, 0.21f, 0.26f);
    static readonly Color BazookaHead = new Color(0.92f, 0.2f, 0.1f);
    static readonly Color BazookaAim = new Color(1f, 0.55f, 0.1f);
    static readonly Color BazookaAimCooling = new Color(0.45f, 0.25f, 0.12f);
    static readonly Color BazookaCold = new Color(0.35f, 0.05f, 0.02f);
    static readonly Color BazookaReady = new Color(3f, 1.8f, 0.5f);
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
    Renderer frontSightRenderer;
    Transform chargeBar;
    Renderer chargeBarRenderer;
    Renderer runeGemRenderer;
    Color gemShown;
    TextMesh label;

    // 武器(銃 / バズーカ)。バズーカのときは、銃の見た目を隠して、バズーカの見た目を出す
    Transform gunRoot;
    GameObject bazookaRoot;
    Renderer bazookaGem;
    Renderer bazookaBand;
    Color bazookaGemShown;
    float muzzleZ = MuzzleZ;
    float switchPopUntil;
    float lastCooldownSeconds = 1f;

    bool comboGlow;
    bool linked;
    bool sightPulsing;

    // 揃っているあいだの、かっこいい姿(羽根、レール、回る枠、回るコア、銃身の発光)
    float linkBlend;                  // 0 = 通常の姿、1 = 揃った姿(なめらかに変わる)
    bool lookDrawn;
    Color gradeColor = new Color(0.35f, 0.38f, 0.42f);
    Transform linkRoot;
    Transform linkSpin;
    Transform linkCore;
    Renderer linkCoreRenderer;
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
    /// <summary>今、バズーカを持っているか(false のときは、ふつうの銃)。</summary>
    public bool IsBazooka { get; private set; }

    /// <summary>今の狙いの先に、何かあるか。</summary>
    public bool HasHit { get; private set; }
    public RaycastHit LastHit;

    public Vector3 Origin => transform.position;
    /// <summary>銃口(先端)の位置。</summary>
    public Vector3 Muzzle => transform.TransformPoint(0f, 0f, muzzleZ);
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
        var sight = AddPart("FrontSight", new Vector3(0.015f, 0.025f, 0.02f),
            new Vector3(0f, 0.035f, MuzzleZ - 0.02f), Vector3.zero, SightColor);
        frontSightRenderer = sight.GetComponent<Renderer>();

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

        // 飾り:後ろの鉄の受け部、金の輪 2 つ、銃口の飾り、持ち手の革巻き、光るルーンの宝石(チャージで色が変わる)
        AddPart("Receiver", new Vector3(0.05f, 0.062f, 0.085f), new Vector3(0f, 0.004f, 0.02f), Vector3.zero, GunSteel);
        AddPart("RingBack", new Vector3(0.041f, 0.051f, 0.012f), new Vector3(0f, 0f, 0.1f), Vector3.zero, GoldTrim);
        AddPart("RingFront", new Vector3(0.041f, 0.051f, 0.012f), new Vector3(0f, 0f, 0.185f), Vector3.zero, GoldTrim);
        AddPart("MuzzleBrake", new Vector3(0.043f, 0.053f, 0.022f), new Vector3(0f, 0f, MuzzleZ - 0.012f), Vector3.zero, GunSteel);
        AddPart("GripWrapUpper", new Vector3(0.034f, 0.014f, 0.044f), new Vector3(0f, -0.042f, 0.032f), new Vector3(-15f, 0f, 0f), LeatherColor);
        AddPart("GripWrapLower", new Vector3(0.034f, 0.014f, 0.044f), new Vector3(0f, -0.078f, 0.024f), new Vector3(-15f, 0f, 0f), LeatherColor);
        var rune = AddPart("RuneGem", Vector3.one * 0.018f, new Vector3(0f, 0.042f, 0.045f), new Vector3(45f, 45f, 0f), RuneGlow);
        runeGemRenderer = rune.GetComponent<Renderer>();
        gemShown = RuneGlow;

        // 銃の見た目の部品を 1 つにまとめる(バズーカに持ち替えたとき、まとめて隠せるように)。
        // 照準の線と、文字は、どちらの武器でも使うので、まとめない。
        gunRoot = new GameObject("GunParts").transform;
        gunRoot.SetParent(transform, false);
        var toGroup = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child != aimLine && child != label.transform && child != gunRoot) toGroup.Add(child);
        }
        foreach (var child in toGroup) child.SetParent(gunRoot, false);
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
        Paint.Apply(part.GetComponent<Renderer>(), color);
        return part;
    }

    public void SetGrade(int grade)
    {
        int index = Mathf.Clamp(grade - 1, 0, GradeColors.Length - 1);
        gradeColor = GradeColors[index];
        if (barrelRenderer != null) barrelRenderer.material.color = gradeColor;
        if (bazookaBand != null) bazookaBand.material.color = gradeColor;
    }

    public void SetComboGlow(bool on) { comboGlow = on; }

    /// <summary>左右の銃が揃っている(手がくっついている)あいだ true。照準線と、先端の照準の見た目が変わる。</summary>
    public void SetLinked(bool on) { linked = on; }

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
            lineLength = Vector3.Distance(transform.TransformPoint(0f, 0f, muzzleZ), aimPoint);
        }
        else
        {
            lineLength = rayLength - muzzleZ;
        }

        float charge01 = Charge01;
        UpdateAimLine(Mathf.Clamp(lineLength, 0.01f, AimLineMaxLength), charge01);
        UpdateChargeBar(charge01);
        UpdateGem(charge01);
        UpdateBazookaGem();
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

    void LateUpdate()
    {
        if (!initialized) return;
        UpdateLinkLook();
        UpdateSwitchPop();
    }

    /// <summary>
    /// 揃っているあいだ、銃が「合体モード」の姿になる。ぽんと出て、少し行き過ぎて、落ち着く動きで変わる。
    ///   ・外側に開いた羽根(暗い金属)  ・銃身の両脇の水色のレール  ・銃口の前で回る四角い枠
    ///   ・銃の上で回る水色のコア  ・銃身が水色に光る(グレードの色と混ざる)
    /// </summary>
    void UpdateLinkLook()
    {
        linkBlend = Mathf.MoveTowards(linkBlend, linked ? 1f : 0f, Time.deltaTime / 0.18f);

        if (linkBlend <= 0f)
        {
            if (lookDrawn)
            {
                // 通常の姿に戻す
                lookDrawn = false;
                if (linkRoot != null) linkRoot.gameObject.SetActive(false);
                if (barrelRenderer != null) barrelRenderer.material.color = gradeColor;
            }
            return;
        }

        if (linkRoot == null) BuildLinkParts();
        lookDrawn = true;
        if (!linkRoot.gameObject.activeSelf) linkRoot.gameObject.SetActive(true);

        float u = linkBlend - 1f;
        float pop = 1f + 2.7f * u * u * u + 1.7f * u * u;   // ぽんと出て、少し大きくなってから、落ち着く
        linkRoot.localScale = Vector3.one * Mathf.Max(0.0001f, pop);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
        linkSpin.localRotation = Quaternion.Euler(0f, 0f, Time.time * 200f);
        linkCore.localRotation = Quaternion.Euler(Time.time * 90f, Time.time * 130f, Time.time * 70f);
        linkCoreRenderer.material.color = Color.Lerp(LinkGlow, Color.white, pulse);
        barrelRenderer.material.color = Color.Lerp(gradeColor, LinkGlow, (0.45f + 0.15f * pulse) * linkBlend);
    }

    void BuildLinkParts()
    {
        var root = new GameObject("LinkParts");
        root.transform.SetParent(transform, false);
        linkRoot = root.transform;

        // 相手の銃と反対側(外側)。左の銃は左、右の銃は右
        float side = isRight ? 1f : -1f;

        // 外側に少し開いた羽根
        MakeLinkPart(linkRoot, "Fin", new Vector3(0.006f, 0.06f, 0.16f),
            new Vector3(side * 0.045f, 0.005f, 0.11f), new Vector3(0f, side * 12f, 0f), LinkSteel);
        // 銃身の両脇の、水色のレール
        MakeLinkPart(linkRoot, "RailOuter", new Vector3(0.004f, 0.008f, 0.2f),
            new Vector3(side * 0.024f, 0f, 0.13f), Vector3.zero, LinkGlow);
        MakeLinkPart(linkRoot, "RailInner", new Vector3(0.004f, 0.008f, 0.2f),
            new Vector3(-side * 0.024f, 0f, 0.13f), Vector3.zero, LinkGlow);

        // 銃口の前で回る四角い枠(真ん中を照準の線が通る)
        var spin = new GameObject("MuzzleFrame");
        spin.transform.SetParent(linkRoot, false);
        spin.transform.localPosition = new Vector3(0f, 0f, MuzzleZ + 0.03f);
        linkSpin = spin.transform;
        const float half = 0.045f;
        MakeLinkPart(linkSpin, "Top", new Vector3(0.09f, 0.008f, 0.012f), new Vector3(0f, half, 0f), Vector3.zero, LinkGlow);
        MakeLinkPart(linkSpin, "Bottom", new Vector3(0.09f, 0.008f, 0.012f), new Vector3(0f, -half, 0f), Vector3.zero, LinkGlow);
        MakeLinkPart(linkSpin, "Left", new Vector3(0.008f, 0.09f, 0.012f), new Vector3(-half, 0f, 0f), Vector3.zero, LinkGlow);
        MakeLinkPart(linkSpin, "Right", new Vector3(0.008f, 0.09f, 0.012f), new Vector3(half, 0f, 0f), Vector3.zero, LinkGlow);

        // 銃の上で回る、光るコア
        var core = new GameObject("Core");
        core.transform.SetParent(linkRoot, false);
        core.transform.localPosition = new Vector3(0f, 0.062f, 0.1f);
        linkCore = core.transform;
        var gem = MakeLinkPart(linkCore, "Gem", Vector3.one * 0.024f, Vector3.zero, Vector3.zero, LinkGlow);
        linkCoreRenderer = gem.GetComponent<Renderer>();

        root.SetActive(false);
    }

    GameObject MakeLinkPart(Transform parent, string partName, Vector3 scale, Vector3 localPos, Vector3 localEuler, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localScale = scale;
        part.transform.localPosition = localPos;
        part.transform.localEulerAngles = localEuler;
        Paint.Apply(part.GetComponent<Renderer>(), color);
        return part;
    }

    void UpdateAimLine(float length, float charge01)
    {
        float thickness = Mathf.Lerp(AimLineThickness, AimLineChargedThickness, charge01);
        if (linked) thickness = Mathf.Max(thickness, 0.008f);
        aimLine.localScale = new Vector3(thickness, thickness, length);
        aimLine.localPosition = new Vector3(0f, 0f, muzzleZ + length * 0.5f);

        Color color = Color.Lerp(AimColor, AimChargedColor, charge01);
        if (comboGlow || linked) color = AimComboColor;
        if (IsBazooka) color = CooldownOver ? BazookaAim : BazookaAimCooling;
        if (Time.time < fireFlashUntil) color = FireColor;
        aimLineRenderer.material.color = color;

        // 揃っているあいだは、先端の照準が、水色と白で脈打つ
        if (frontSightRenderer != null && (linked || sightPulsing))
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            frontSightRenderer.material.color = linked ? Color.Lerp(SightColor, Color.white, pulse) : SightColor;
            sightPulsing = linked;
        }
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

    /// <summary>ルーンの宝石:ふだんは水色。チャージすると金色に燃え、撃った瞬間に白く光り、揃っているあいだは脈打つ。</summary>
    void UpdateGem(float charge01)
    {
        if (runeGemRenderer == null) return;

        Color c = RuneGlow;
        if (Charging) c = Color.Lerp(RuneGlow, new Color(3f, 2f, 0.6f), charge01);
        if (Time.time < fireFlashUntil) c = new Color(3f, 3f, 2.4f);
        if (linked) c = Color.Lerp(RuneGlow, new Color(2f, 2f, 2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));

        if (c != gemShown)
        {
            runeGemRenderer.material.color = c;
            gemShown = c;
        }
    }

    void UpdateLabel()
    {
        bool showMessage = Time.time < messageUntil;
        string text = showMessage ? messageText : statusText;
        Color color = showMessage ? messageColor : statusColor;

        // 変わったときだけ書き換える(文字の作り直しを減らすため)
        if (text != shownText) { HudText.SetText(label, text); shownText = text; }
        if (color != shownColor) { label.color = color; shownColor = color; }

        // 文字がいつも顔のほうを向くようにする
        Vector3 fromEye = label.transform.position - PlayerView.Eye.position;
        if (fromEye.sqrMagnitude > 0.0001f) label.transform.rotation = Quaternion.LookRotation(fromEye);
    }

    // ---- 武器の切り替え(銃 ⇄ バズーカ) ----

    /// <summary>銃とバズーカを切り替える。見た目と銃口の位置が変わり、「ぽん」と大きくなる動きが出る。</summary>
    public void SetWeapon(bool bazooka)
    {
        if (bazooka == IsBazooka) return;

        IsBazooka = bazooka;
        muzzleZ = bazooka ? BazookaMuzzleZ : MuzzleZ;
        if (bazooka && bazookaRoot == null) BuildBazooka();

        gunRoot.gameObject.SetActive(!bazooka);
        bazookaRoot.SetActive(bazooka);
        switchPopUntil = Time.time + SwitchPopSeconds;

        // 持ち替えた瞬間に、すぐ撃ってしまわないように、少しだけ待つ
        cooldownUntil = Mathf.Max(cooldownUntil, Time.time + 0.2f);
        lastCooldownSeconds = 0.2f;
    }

    /// <summary>持ち替えた直後、武器が少し大きくなってから、元の大きさに落ち着く。</summary>
    void UpdateSwitchPop()
    {
        Transform visible = IsBazooka ? (bazookaRoot != null ? bazookaRoot.transform : null) : gunRoot;
        if (visible == null) return;

        float k = Mathf.Clamp01((switchPopUntil - Time.time) / SwitchPopSeconds);
        visible.localScale = Vector3.one * (1f + 0.5f * k * k);
    }

    /// <summary>バズーカの見た目を作る(最初に持ち替えたときに 1 回だけ)。</summary>
    void BuildBazooka()
    {
        bazookaRoot = new GameObject("BazookaParts");
        bazookaRoot.transform.SetParent(transform, false);
        Transform root = bazookaRoot.transform;

        // 太い筒(暗い鉄)と、後ろの広がった口
        MakeLinkPart(root, "Tube", new Vector3(0.07f, 0.07f, 0.55f), new Vector3(0f, 0f, 0.2f), Vector3.zero, BazookaSteel);
        MakeLinkPart(root, "RearFlare", new Vector3(0.105f, 0.105f, 0.05f), new Vector3(0f, 0f, -0.075f), Vector3.zero, BazookaSteel);
        MakeLinkPart(root, "RearRing", new Vector3(0.115f, 0.115f, 0.012f), new Vector3(0f, 0f, -0.1f), Vector3.zero, GoldTrim);

        // 金の帯 3 本(まん中の 1 本は、グレードの色になる)
        MakeLinkPart(root, "BandRear", new Vector3(0.08f, 0.08f, 0.02f), new Vector3(0f, 0f, 0.05f), Vector3.zero, GoldTrim);
        var band = MakeLinkPart(root, "BandGrade", new Vector3(0.082f, 0.082f, 0.03f), new Vector3(0f, 0f, 0.22f), Vector3.zero, gradeColor);
        bazookaBand = band.GetComponent<Renderer>();
        MakeLinkPart(root, "BandFront", new Vector3(0.08f, 0.08f, 0.02f), new Vector3(0f, 0f, 0.42f), Vector3.zero, GoldTrim);

        // 筒の先から顔を出している、赤い弾頭
        MakeLinkPart(root, "Warhead", new Vector3(0.055f, 0.055f, 0.06f), new Vector3(0f, 0f, 0.465f), Vector3.zero, BazookaHead);
        MakeLinkPart(root, "WarheadTip", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 45f), BazookaHead);

        // 持ち手(後ろ)と、前の持ち手
        MakeLinkPart(root, "Grip", new Vector3(0.03f, 0.09f, 0.04f), new Vector3(0f, -0.07f, 0.03f), new Vector3(-15f, 0f, 0f), new Color(0.15f, 0.15f, 0.17f));
        MakeLinkPart(root, "GripWrap", new Vector3(0.034f, 0.014f, 0.044f), new Vector3(0f, -0.06f, 0.032f), new Vector3(-15f, 0f, 0f), LeatherColor);
        MakeLinkPart(root, "ForeGrip", new Vector3(0.03f, 0.07f, 0.03f), new Vector3(0f, -0.06f, 0.3f), Vector3.zero, LeatherColor);

        // 照準(上に、前と後ろの 2 つ)
        MakeLinkPart(root, "SightFront", new Vector3(0.014f, 0.03f, 0.014f), new Vector3(0f, 0.05f, 0.4f), Vector3.zero, SightColor);
        MakeLinkPart(root, "SightRear", new Vector3(0.03f, 0.024f, 0.012f), new Vector3(0f, 0.048f, 0.05f), Vector3.zero, SightColor);

        // 発射の準備ができると光る宝石(撃った直後は暗く、だんだん明るくなる)
        var gem = MakeLinkPart(root, "ReadyGem", Vector3.one * 0.02f, new Vector3(0f, 0.046f, 0.2f), new Vector3(45f, 45f, 0f), BazookaReady);
        bazookaGem = gem.GetComponent<Renderer>();
        bazookaGemShown = BazookaReady;

        bazookaRoot.SetActive(false);
    }

    /// <summary>バズーカの宝石:撃った直後は暗く、次の弾がこめられるにつれて明るくなり、準備ができると脈打つ。</summary>
    void UpdateBazookaGem()
    {
        if (!IsBazooka || bazookaGem == null) return;

        float ready = CooldownOver ? 1f : 1f - Mathf.Clamp01((cooldownUntil - Time.time) / Mathf.Max(0.05f, lastCooldownSeconds));
        Color c = Color.Lerp(BazookaCold, BazookaReady, ready);
        if (ready >= 1f) c = Color.Lerp(BazookaReady, Color.white, 0.5f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f)));

        if (c != bazookaGemShown)
        {
            bazookaGem.material.color = c;
            bazookaGemShown = c;
        }
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
        lastCooldownSeconds = cooldownSeconds;
        fireFlashUntil = Time.time + 0.06f + 0.1f * charge01;
        StartCoroutine(Haptic(0.4f + 0.6f * charge01, 0.05f + 0.1f * charge01 + 0.2f * Mathf.InverseLerp(0.6f, 1f, charge01)));
    }

    /// <summary>弾がないときの演出。</summary>
    public void PlayEmpty()
    {
        ShowMessage("NO AMMO", new Color(1f, 0.3f, 0.3f), 0.8f);
        GameAudio.PlayEmpty(transform.position);
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

using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 左右 2 丁の銃をまとめて管理する。
///   ・トリガーを引いている時間で威力が決まる(離した瞬間に撃つ)
///   ・両手を近づけて、両方のトリガーを引くと「両手撃ち」になり、2 丁ぶんが合体して威力が増す
///   ・残弾は両手で共有。撃つと減り、アイテムで回復する
///   ・グレードは両手で共通。アイテムで上がり、威力が増す
///   ・敵を倒すと、アイテム(グレードアップ / 残弾回復)を落とすことがある
///
/// 空の GameObject にこのコンポーネントを付けるだけで動く(銃は起動時に自動で作る)。
/// </summary>
[DisallowMultipleComponent]
public class GunSystem : MonoBehaviour
{
    public static GunSystem Instance { get; private set; }

    [Header("威力(トリガーを引いていた時間で決まる)")]
    [Tooltip("すぐ離したときの威力。")]
    [SerializeField] float minDamage = 1f;
    [Tooltip("フルチャージしたときの威力。")]
    [SerializeField] float maxDamage = 5f;
    [Tooltip("この秒数引き続けると、威力が最大になる。")]
    [SerializeField] float fullChargeSeconds = 1.2f;
    [Tooltip("撃ったあと、次のチャージを始められるまでの秒数。")]
    [SerializeField] float fireInterval = 0.15f;

    [Header("両手撃ち")]
    [Tooltip("2 丁の銃(コントローラー)の中心どうしがこの距離(m)以内、つまり、手がくっついているくらいのとき、両手撃ちになる。"
             + "コントローラーを左右でくっつけると、中心は約 8〜12cm 離れる。発動しにくいときは 0.15 くらいまで上げる。")]
    [SerializeField] float comboDistance = 0.12f;
    [Tooltip("2 丁ぶんの威力を足したものに、さらにかける倍率。")]
    [SerializeField] float comboMultiplier = 1.5f;
    [Tooltip("両手撃ちの弾の太さ(半径 m)。太いほど当たりやすい。")]
    [SerializeField] float comboBeamRadius = 0.06f;

    [Header("残弾(両手で共有)")]
    [SerializeField] int startAmmo = 24;
    [SerializeField] int maxAmmo = 40;
    [Tooltip("残弾回復アイテム 1 個で増える数。")]
    [SerializeField] int ammoPerPack = 8;
    [Tooltip("ウェーブが始まるとき、残弾がこの数より少なければ、この数まで補充する。")]
    [SerializeField] int waveStartMinAmmo = 10;
    [Tooltip("残弾が 0 のとき、この秒数ごとに 1 発だけ回復する(詰み防止)。")]
    [SerializeField] float emptyRegenSeconds = 3f;

    [Header("グレード(両手で共通)")]
    [SerializeField] int maxGrade = 5;
    [Tooltip("グレードが 1 上がるごとに、威力が何割増えるか(0.3 = 30% 増)。")]
    [SerializeField] float damageBonusPerGrade = 0.3f;

    [Header("アイテムのドロップ(敵を倒したとき)")]
    [Range(0f, 1f)] [SerializeField] float gradeItemChance = 0.35f;
    [Range(0f, 1f)] [SerializeField] float ammoItemChance = 0.45f;
    [Tooltip("残弾がこの数以下のときは、必ず残弾回復アイテムが出る。")]
    [SerializeField] int lowAmmoGuaranteedDrop = 6;

    [Header("武器の切り替え(銃口を頭上へ向ける)")]
    [Tooltip("オフにすると、銃口を上へ向けての切り替えをしない。")]
    [SerializeField] bool skySwitchEnabled = true;
    [Tooltip("銃口を、水平からこの角度(度)以上、上へ向けたら、切り替えの合図の始まり。90 が真上。誤作動が多いときは上げ、切り替わりにくいときは下げる。")]
    [Range(30f, 90f)] [SerializeField] float skySwitchAngle = 60f;
    [Tooltip("上へ向けた状態を、この秒数続けると、切り替わる(0 なら、向けた瞬間に切り替わる)。")]
    [SerializeField] float skySwitchHold = 0.1f;
    [Tooltip("切り替えたあと、銃口がこの角度(度)より下がるまで、次の切り替えを受け付けない。")]
    [Range(0f, 80f)] [SerializeField] float skySwitchResetPitch = 40f;
    [Tooltip("切り替えたあと、同じ手で次の切り替えを受け付けるまでの秒数。")]
    [SerializeField] float switchCooldown = 0.7f;

    [Header("バズーカ(銃口を頭上へ向けた手の銃が変わる)")]
    [Tooltip("爆発の中心での、基本の威力(グレードの倍率がかかる)。ふつうの敵は 3、頑丈な敵は 9、巨大な敵は 30 くらいの体力。")]
    [SerializeField] float bazookaDamage = 12f;
    [Tooltip("爆発の半径(m)。この範囲の敵にダメージ。中心から遠いほど弱く、端では 50%。")]
    [SerializeField] float bazookaRadius = 3.5f;
    [Tooltip("弾が敵に当たらなくても、敵のこの距離(m)以内を通ると、その場で爆発する(近接信管)。0 にすると、何かに当たるまで爆発しない。")]
    [SerializeField] float bazookaProximity = 1.5f;
    [Tooltip("1 発撃ってから、次を撃てるまでの秒数。")]
    [SerializeField] float bazookaFireInterval = 1.5f;
    [Tooltip("1 発で減る残弾(両手で共有)。")]
    [SerializeField] int bazookaAmmoCost = 2;
    [Tooltip("弾の飛ぶ速さ(m/秒)。")]
    [SerializeField] float rocketSpeed = 45f;

    [Header("その他")]
    [SerializeField] float range = 120f;
    [SerializeField] LayerMask hitMask = ~0;
    [Tooltip("銃口の向きの微調整(度)。プラスで下向き。")]
    [SerializeField] float muzzlePitchDegrees = 0f;
    [Tooltip("Editor でヘッドセットがないとき、マウスで撃てるようにする。")]
    [SerializeField] bool editorMouseFallback = true;

    public float FullChargeSeconds => fullChargeSeconds;
    public float Range => range;
    public LayerMask HitMask => hitMask;
    public float MuzzlePitchDegrees => muzzlePitchDegrees;
    public bool EditorMouseFallback => editorMouseFallback;

    /// <summary>両方のトリガーを引いている間 true(ゲームオーバー後の再開に使う)。</summary>
    public bool BothTriggersHeld => left != null && right != null && left.Held && right.Held;

    public int Ammo { get; private set; }
    public int Grade { get; private set; } = 1;

    static readonly Color LowAmmoColor = new Color(1f, 0.3f, 0.3f);
    static readonly Color GradeUpColor = new Color(1f, 0.85f, 0.2f);
    static readonly Color AmmoUpColor = new Color(0.4f, 1f, 0.5f);
    static readonly Color ComboColor = new Color(0.5f, 1f, 1f);
    static readonly Color HeadshotColor = new Color(1f, 0.35f, 0.2f);
    static readonly Color LinkColor = new Color(0.4f, 1f, 1f);
    static readonly Color UnlinkColor = new Color(1f, 0.6f, 0.2f);
    static readonly Color BazookaColor = new Color(1f, 0.55f, 0.15f);

    // Editor でヘッドセットがないときの、左右の銃の間隔(片側ぶん、m)。
    // ふだんは離れていて、Space キーを押している間だけ、中央に寄って「両手を合わせた」状態になる。
    const float EditorApartHalfGap = 0.38f;
    const float EditorTogetherHalfGap = 0.03f;   // 2 丁の中心が 6cm(手がくっついた状態)
    /// <summary>両手撃ちの判定を、離した直後もこの秒数だけ保つ(手がずれて撃ち損ねないように)。</summary>
    const float ComboGraceSeconds = 0.15f;
    /// <summary>「揃った」表示を、手が離れてもこの秒数は保つ(境目で、表示がちらつかないように)。</summary>
    const float LinkKeepSeconds = 0.12f;

    /// <summary>Editor の動作確認中の、左右の銃の間隔(片側ぶん、m)。</summary>
    public float EditorHandHalfGap { get; private set; } = EditorApartHalfGap;

    VRGun left;
    VRGun right;
    SkyPointDetector leftSky;
    SkyPointDetector rightSky;
    float leftSwitchReadyAt;
    float rightSwitchReadyAt;
    float emptyTimer;
    float togetherUntil;

    // 銃が揃った(手がくっついた)状態の表示
    bool linked;
    float linkedUntil;
    GameObject linkOrb;
    GameObject linkBridge;
    Renderer linkOrbRenderer;
    Renderer linkBridgeRenderer;

    void Awake()
    {
        Instance = this;

        // 以前の「1 丁だけ」の VRGun が同じオブジェクトに残っていたら、取り除く
        foreach (var old in GetComponents<VRGun>()) Destroy(old);
    }

    void OnEnable() { Target.Killed += OnTargetKilled; }
    void OnDisable() { Target.Killed -= OnTargetKilled; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (!PlayerView.Ensure())
        {
            enabled = false;
            return;
        }

        Ammo = Mathf.Min(startAmmo, maxAmmo);
        Grade = 1;

        leftSky = new SkyPointDetector();
        rightSky = new SkyPointDetector();
        right = CreateGun("Gun_Right", true, PlayerView.RightHand);
        left = CreateGun("Gun_Left", false, PlayerView.LeftHand);

        ApplyGradeVisuals();
        RefreshLabels();
        if (PlayerView.IsEditorSimulation)
        {
            Debug.Log("[GunSystem] Editor の操作: マウス左ボタン=右手の銃 / マウス右ボタンまたは Z キー=左手の銃 / Space を押している間=両手を合わせる / X キー=右手の武器を切り替え / C キー=左手の武器を切り替え(銃 ⇄ バズーカ。実機では、銃口を頭上へ向ける)");
        }
        Debug.Log($"[GunSystem] 左右の銃を作りました(残弾 {Ammo}/{maxAmmo}、グレード {Grade})");
    }

    VRGun CreateGun(string objectName, bool isRight, Transform anchor)
    {
        var go = new GameObject(objectName);
        var gun = go.AddComponent<VRGun>();
        gun.Init(this, isRight, anchor);
        return gun;
    }

    void Update()
    {
#if UNITY_EDITOR
        // Editor でヘッドセットがないとき、Space を押している間だけ、左右の銃を中央に寄せる
        if (PlayerView.IsEditorSimulation && editorMouseFallback)
        {
            Keyboard keyboard = Keyboard.current;
            bool together = keyboard != null && keyboard.spaceKey.isPressed;
            float goal = together ? EditorTogetherHalfGap : EditorApartHalfGap;
            EditorHandHalfGap = Mathf.MoveTowards(EditorHandHalfGap, goal, 6f * Time.deltaTime);

            // 動作確認用:ヘッドセットがないときは、キーで「銃口を頭上へ向ける」の代わりにする
            if (left != null && right != null && keyboard != null)
            {
                if (keyboard.xKey.wasPressedThisFrame) ToggleWeapon(right);
                if (keyboard.cKey.wasPressedThisFrame) ToggleWeapon(left);
            }
        }
#endif
    }

    // 銃の Update(トリガーや狙いの読み取り)が全部終わってから判断するため、LateUpdate で処理する
    void LateUpdate()
    {
        if (left == null || right == null) return;

        UpdateSkyPoint();

        TryBeginCharge(left);
        TryBeginCharge(right);

        UpdateLink();

        // 両手を近づけて、両方チャージしているときは、両手撃ちの状態
        if (left.Charging && right.Charging && HandsTogether()) togetherUntil = Time.time + ComboGraceSeconds;
        if (!left.Charging && !right.Charging) togetherUntil = 0f;
        bool together = left.Charging && right.Charging && Time.time < togetherUntil;
        left.SetComboGlow(together);
        right.SetComboGlow(together);

        RegenWhenEmpty();

        bool leftReleased = left.Charging && !left.Held;
        bool rightReleased = right.Charging && !right.Held;
        if (!leftReleased && !rightReleased) return;

        if (together)
        {
            FireCombo();
        }
        else
        {
            if (leftReleased) Shoot(left, left.EndCharge());
            if (rightReleased) Shoot(right, right.EndCharge());
        }
        RefreshLabels();
    }

    void TryBeginCharge(VRGun gun)
    {
        // バズーカは、ため撃ちではなく、トリガーを引いた瞬間に 1 発撃つ
        if (gun.IsBazooka)
        {
            TryFireBazooka(gun);
            return;
        }

        if (gun.Charging || !gun.Held || gun.NeedsRelease || !gun.CooldownOver) return;

        if (Ammo <= 0)
        {
            gun.NeedsRelease = true;
            gun.PlayEmpty();
            return;
        }
        gun.BeginCharge();
    }

    // ---- 銃が揃った状態の表現(トリガーを引いていなくても、手がくっついたら出る) ----

    /// <summary>
    /// 左右の銃が「揃った」(手がくっついた)かどうかを見て、揃った瞬間と、解除された瞬間に、演出を出す。
    /// 揃っているあいだは、照準線が水色に太くなり、先端の照準が脈打ち、左右の銃口のあいだに光の橋と玉が出る。
    /// </summary>
    void UpdateLink()
    {
        // 起動の直後は、コントローラーの位置がまだ定まらず、2 つが重なって見えることがあるので、少し待つ
        if (Time.timeSinceLevelLoad < 3f) return;

        // 合体(LINK)は、両手とも銃のときだけ。どちらかがバズーカなら、解除する
        bool bothGuns = !left.IsBazooka && !right.IsBazooka;
        if (HandsTogether()) linkedUntil = Time.time + LinkKeepSeconds;
        bool now = bothGuns && Time.time < linkedUntil;

        if (now != linked)
        {
            linked = now;
            if (linked) OnLinked();
            else OnUnlinked();
        }

        left.SetLinked(linked);
        right.SetLinked(linked);
        UpdateLinkVisual();
    }

    Vector3 MuzzleMidpoint() { return (left.Muzzle + right.Muzzle) * 0.5f; }

    void OnLinked()
    {
        Vector3 mid = MuzzleMidpoint();
        Vector3 forward = (left.Forward + right.Forward).normalized;
        GameAudio.PlayLink(mid);
        PulseHaptics(0.7f, 0.06f);
        ShotBurst.Spawn(ShotBurst.Shape.Ring, mid, forward, 12, 0.9f, 0.012f, LinkColor, 0.35f, 0f, 0f, 3f);
        ShotBurst.Spawn(ShotBurst.Shape.Sphere, mid, Vector3.up, 8, 0.7f, 0.01f, Color.white, 0.3f, 0f, 0f, 3f);
        Announce("LINK", LinkColor, 0.6f);
    }

    void OnUnlinked()
    {
        Vector3 mid = MuzzleMidpoint();
        GameAudio.PlayUnlink(mid);
        PulseHaptics(0.35f, 0.04f);
        ShotBurst.Spawn(ShotBurst.Shape.Sphere, mid, Vector3.up, 10, 1.2f, 0.012f, UnlinkColor, 0.4f, 0f, 2f, 3f);
        Announce("SPLIT", UnlinkColor, 0.5f);
    }

    /// <summary>揃っているあいだだけ、左右の銃口を結ぶ光の橋と、真ん中の光の玉を出す。</summary>
    void UpdateLinkVisual()
    {
        if (!linked)
        {
            if (linkOrb != null && linkOrb.activeSelf) linkOrb.SetActive(false);
            if (linkBridge != null && linkBridge.activeSelf) linkBridge.SetActive(false);
            return;
        }

        if (linkOrb == null)
        {
            linkOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            linkOrb.name = "LinkOrb";
            Destroy(linkOrb.GetComponent<Collider>());
            linkOrb.transform.SetParent(transform, false);
            linkOrbRenderer = linkOrb.GetComponent<Renderer>();
            Paint.Apply(linkOrbRenderer, LinkColor);

            linkBridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            linkBridge.name = "LinkBridge";
            Destroy(linkBridge.GetComponent<Collider>());
            linkBridge.transform.SetParent(transform, false);
            linkBridgeRenderer = linkBridge.GetComponent<Renderer>();
            Paint.Apply(linkBridgeRenderer, LinkColor);
        }

        Vector3 a = left.Muzzle;
        Vector3 b = right.Muzzle;
        Vector3 between = b - a;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);

        if (!linkOrb.activeSelf) linkOrb.SetActive(true);
        linkOrb.transform.position = (a + b) * 0.5f;
        linkOrb.transform.localScale = Vector3.one * (0.045f + 0.02f * pulse);
        linkOrbRenderer.material.color = Color.Lerp(LinkColor, Color.white, pulse * 0.6f);

        bool showBridge = between.sqrMagnitude > 0.000001f;
        if (linkBridge.activeSelf != showBridge) linkBridge.SetActive(showBridge);
        if (showBridge)
        {
            float thickness = 0.006f + 0.004f * pulse;
            linkBridge.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(between));
            linkBridge.transform.localScale = new Vector3(thickness, thickness, between.magnitude);
            linkBridgeRenderer.material.color = Color.Lerp(LinkColor, Color.white, pulse * 0.4f);
        }
    }

    bool HandsTogether()
    {
        // 実機でも Editor でも、2 丁の銃の実際の距離で判定する
        return Vector3.Distance(left.Origin, right.Origin) <= comboDistance;
    }

    // ---- 武器の切り替え(銃口を頭上へ)と、バズーカ ----

    /// <summary>左右の銃口が頭上を向いたかを見て、成立したら、その手の武器を切り替える。</summary>
    void UpdateSkyPoint()
    {
        if (!skySwitchEnabled || PlayerView.IsEditorSimulation) return;

        ApplySkySettings(leftSky);
        ApplySkySettings(rightSky);

        bool leftSignal = leftSky.Update(left.Forward, left.Held);
        bool rightSignal = rightSky.Update(right.Forward, right.Held);

        if (leftSignal && Time.unscaledTime >= leftSwitchReadyAt) ToggleWeapon(left);
        if (rightSignal && Time.unscaledTime >= rightSwitchReadyAt) ToggleWeapon(right);
    }

    void ApplySkySettings(SkyPointDetector detector)
    {
        detector.MinPitchDegrees = skySwitchAngle;
        detector.HoldSeconds = skySwitchHold;
        detector.ResetPitchDegrees = Mathf.Min(skySwitchResetPitch, skySwitchAngle - 5f);
    }

    /// <summary>その手の武器を、銃 ⇄ バズーカで切り替える。</summary>
    void ToggleWeapon(VRGun gun)
    {
        bool toBazooka = !gun.IsBazooka;

        // ため撃ちの途中だったら、やめる(弾は減らさない)
        if (gun.Charging) gun.EndCharge();

        gun.SetWeapon(toBazooka);
        // トリガーを引いたままでも、持ち替えた瞬間に撃たない(一度離すまで待つ)
        gun.NeedsRelease = gun.Held;

        if (gun.IsRight)
        {
            rightSwitchReadyAt = Time.unscaledTime + switchCooldown;
            rightSky.Reset();
        }
        else
        {
            leftSwitchReadyAt = Time.unscaledTime + switchCooldown;
            leftSky.Reset();
        }

        Vector3 muzzle = gun.Muzzle;
        Color color = toBazooka ? BazookaColor : ComboColor;
        gun.ShowMessage(toBazooka ? "BAZOOKA" : "GUN", color, 0.9f);
        GameAudio.PlaySwitch(muzzle, toBazooka);
        gun.PlayHapticPulse(0.8f, 0.1f);
        ShotBurst.Spawn(ShotBurst.Shape.Ring, muzzle, gun.Forward, 10, 0.8f, 0.012f, color, 0.3f, 0f, 0f, 3f);

        string hand = gun.IsRight ? "右" : "左";
        Debug.Log($"[GunSystem] {hand}手の武器を切り替えました: {(toBazooka ? "バズーカ" : "銃")}");
        RefreshLabels();
    }

    /// <summary>バズーカ:トリガーを引いた瞬間に 1 発。押しっぱなしでは連射しない。</summary>
    void TryFireBazooka(VRGun gun)
    {
        if (!gun.Held || gun.NeedsRelease || !gun.CooldownOver) return;

        // 押しっぱなしでは撃たない。弾が足りなくても、一度離すまで、続けて鳴らさない
        gun.NeedsRelease = true;

        if (Ammo < bazookaAmmoCost)
        {
            gun.PlayEmpty();
            return;
        }

        Ammo -= bazookaAmmoCost;
        gun.PlayShotFeedback(1f, bazookaFireInterval);

        Vector3 muzzle = gun.Muzzle;
        Vector3 forward = gun.Forward;
        float damage = bazookaDamage * GradeMultiplier;
        Rocket.Launch(muzzle, forward, rocketSpeed, range, damage, bazookaRadius, bazookaProximity, hitMask);

        // 発射の演出:銃口の火と煙、手元の後ろへ吹き出す煙
        GameAudio.PlayBazookaLaunch(muzzle);
        ShotBurst.Spawn(ShotBurst.Shape.Cone, muzzle, forward, 14, 7f, 0.05f, new Color(1f, 0.7f, 0.2f), 0.35f, 0f, 1f, 3f);
        ShotBurst.Spawn(ShotBurst.Shape.Cone, gun.Origin, -forward, 10, 4f, 0.06f, new Color(0.8f, 0.8f, 0.8f), 0.5f, 0f, 0f, 3f);

        string hand = gun.IsRight ? "右" : "左";
        Debug.Log($"[GunSystem] {hand}手でバズーカ発射: 威力 {damage:0.0}、半径 {bazookaRadius:0.0} m、残弾 {Ammo}");
        RefreshLabels();
    }

    // ---- 撃つ ----

    float GradeMultiplier => 1f + damageBonusPerGrade * (Grade - 1);

    float ShotDamage(float charge01)
    {
        return Mathf.Lerp(minDamage, maxDamage, charge01) * GradeMultiplier;
    }

    /// <summary>片手で 1 発撃つ。</summary>
    void Shoot(VRGun gun, float charge01)
    {
        if (Ammo < 1)
        {
            gun.PlayEmpty();
            return;
        }

        Ammo--;
        float damage = ShotDamage(charge01);
        gun.PlayShotFeedback(charge01, fireInterval);

        string hand = gun.IsRight ? "右" : "左";
        string hitName = gun.HasHit ? gun.LastHit.collider.name : "なし";
        Debug.Log($"[GunSystem] {hand}手で発射: チャージ {charge01:0.00}、威力 {damage:0.0}、命中 {hitName}、残弾 {Ammo}");

        Vector3 muzzle = gun.Muzzle;
        Vector3 end = gun.HasHit ? gun.LastHit.point : muzzle + gun.Forward * 100f;
        ShotEffects.Fire(muzzle, end, gun.HasHit, charge01);
        GameAudio.PlayShot(muzzle, charge01);

        if (gun.HasHit) DamageTarget(gun.LastHit.collider, damage);
    }

    /// <summary>両手撃ち。2 丁ぶんの弾を合体させて、1 発の太い弾にする。</summary>
    void FireCombo()
    {
        float chargeL = left.EndCharge();
        float chargeR = right.EndCharge();

        togetherUntil = 0f;
        // 片方がまだトリガーを引いていても、そのまま続けてチャージしないようにする
        left.NeedsRelease = true;
        right.NeedsRelease = true;

        if (Ammo < 2)
        {
            // 残弾が足りないときは、チャージの大きいほうの 1 丁だけで撃つ
            if (chargeL >= chargeR) Shoot(left, chargeL);
            else Shoot(right, chargeR);
            return;
        }

        Ammo -= 2;

        float damage = (ShotDamage(chargeL) + ShotDamage(chargeR)) * comboMultiplier;
        Vector3 origin = (left.Origin + right.Origin) * 0.5f;
        Vector3 direction = (left.Forward + right.Forward).normalized;

        RaycastHit hit;
        bool hasHit = Physics.SphereCast(origin, comboBeamRadius, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore);
        float length = hasHit ? hit.distance : 100f;

        ShowBeam(origin, direction, length);
        GameAudio.PlayCombo(origin);

        float bigger = Mathf.Max(chargeL, chargeR);
        left.PlayShotFeedback(bigger, fireInterval * 2f);
        right.PlayShotFeedback(bigger, fireInterval * 2f);
        left.ShowMessage("COMBO!", ComboColor, 0.7f);
        right.ShowMessage("COMBO!", ComboColor, 0.7f);

        if (hasHit) DamageTarget(hit.collider, damage);
    }

    /// <summary>当たったコライダーの敵にダメージを与える。頭(HeadHitbox)に当たったら、一撃で倒す。</summary>
    void DamageTarget(Collider col, float damage)
    {
        Target target = col.GetComponentInParent<Target>();
        if (target == null || target.IsDead) return;

        if (col.GetComponent<HeadHitbox>() != null)
        {
            Debug.Log($"[GunSystem] ヘッドショット! {target.name} を一撃で倒しました");
            target.HeadShot();
            Announce("HEADSHOT!", HeadshotColor, 0.7f);
            return;
        }

        target.TakeDamage(damage);
    }

    /// <summary>両手撃ちの弾の軌跡を、太い光の線で見せる(少しずつ細くなって消える)。</summary>
    static void ShowBeam(Vector3 origin, Vector3 direction, float length)
    {
        ShotEffects.Trail(origin, origin + direction * length, 0.08f, ComboColor, 0.5f);
    }

    // ---- 残弾・グレード・アイテム ----

    void RegenWhenEmpty()
    {
        if (Ammo > 0)
        {
            emptyTimer = 0f;
            return;
        }

        emptyTimer += Time.deltaTime;
        if (emptyTimer >= emptyRegenSeconds)
        {
            emptyTimer = 0f;
            Ammo = 1;
            RefreshLabels();
        }
    }

    /// <summary>砦がダメージを受けたときなどに、両方のコントローラーを短く震わせる。</summary>
    public void PulseHaptics(float amplitude, float seconds)
    {
        if (left != null) left.PlayHapticPulse(amplitude, seconds);
        if (right != null) right.PlayHapticPulse(amplitude, seconds);
    }

    /// <summary>左右のコントローラーの上に、短いメッセージを出す(高さ補正の完了など)。</summary>
    public void Announce(string message, Color color, float seconds)
    {
        if (left != null) left.ShowMessage(message, color, seconds);
        if (right != null) right.ShowMessage(message, color, seconds);
    }

    /// <summary>ウェーブが始まるときに呼ばれる。残弾が少なすぎれば補充する。</summary>
    public void OnWaveStart()
    {
        if (Ammo < waveStartMinAmmo)
        {
            Ammo = Mathf.Min(waveStartMinAmmo, maxAmmo);
            RefreshLabels();
        }
    }

    /// <summary>アイテムを取ったときに呼ばれる。</summary>
    public void CollectItem(ItemKind kind)
    {
        string message;
        Color color;

        if (kind == ItemKind.Grade && Grade < maxGrade)
        {
            Grade++;
            ApplyGradeVisuals();
            message = "GRADE UP!";
            color = GradeUpColor;
            GameAudio.PlayPickup(true);
        }
        else
        {
            // 残弾回復アイテム。最大グレードでのグレードアップ品も、残弾の補給として使う。
            Ammo = Mathf.Min(maxAmmo, Ammo + ammoPerPack);
            message = "AMMO +" + ammoPerPack;
            color = AmmoUpColor;
            GameAudio.PlayPickup(false);
        }

        RefreshLabels();
        if (left != null)
        {
            left.ShowMessage(message, color, 1f);
            left.PlayPickupFeedback();
        }
        if (right != null)
        {
            right.ShowMessage(message, color, 1f);
            right.PlayPickupFeedback();
        }
    }

    void OnTargetKilled(Target target)
    {
        ItemKind? drop = null;

        if (Ammo <= lowAmmoGuaranteedDrop)
        {
            drop = ItemKind.Ammo;
        }
        else
        {
            float roll = Random.value;
            if (roll < gradeItemChance) drop = ItemKind.Grade;
            else if (roll < gradeItemChance + ammoItemChance) drop = ItemKind.Ammo;
        }

        if (drop.HasValue) ItemPickup.Spawn(drop.Value, target.DropPoint);

        // 頑丈な敵・巨大な敵は、追加でアイテムを落とす(グレードアップ → 残弾回復 の順)
        for (int i = 0; i < target.BonusDrops; i++)
        {
            ItemKind bonus = i % 2 == 0 ? ItemKind.Grade : ItemKind.Ammo;
            ItemPickup.Spawn(bonus, target.DropPoint + Random.insideUnitSphere * 0.5f);
        }
    }

    void ApplyGradeVisuals()
    {
        if (left != null) left.SetGrade(Grade);
        if (right != null) right.SetGrade(Grade);
    }

    string StatusText(VRGun gun)
    {
        string text = "AMMO " + Ammo + "/" + maxAmmo + "\nGRADE " + Grade;
        if (gun.IsBazooka) text += "\nBAZOOKA";
        return text;
    }

    void RefreshLabels()
    {
        if (left == null || right == null) return;

        Color color = Ammo <= lowAmmoGuaranteedDrop ? LowAmmoColor : Color.white;
        left.SetStatus(StatusText(left), color);
        right.SetStatus(StatusText(right), color);
    }
}

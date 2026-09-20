using UnityEngine;

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
    [Tooltip("両手がこの距離(m)以内のとき、両手撃ちになる。")]
    [SerializeField] float comboDistance = 0.25f;
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

    VRGun left;
    VRGun right;
    float emptyTimer;

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

        right = CreateGun("Gun_Right", true, PlayerView.RightHand);
        left = CreateGun("Gun_Left", false, PlayerView.LeftHand);

        ApplyGradeVisuals();
        RefreshLabels();
        Debug.Log($"[GunSystem] 左右の銃を作りました(残弾 {Ammo}/{maxAmmo}、グレード {Grade})");
    }

    VRGun CreateGun(string objectName, bool isRight, Transform anchor)
    {
        var go = new GameObject(objectName);
        var gun = go.AddComponent<VRGun>();
        gun.Init(this, isRight, anchor);
        return gun;
    }

    // 銃の Update(トリガーや狙いの読み取り)が全部終わってから判断するため、LateUpdate で処理する
    void LateUpdate()
    {
        if (left == null || right == null) return;

        TryBeginCharge(left);
        TryBeginCharge(right);

        // 両手を近づけて、両方チャージしているときは、両手撃ちの状態
        bool together = left.Charging && right.Charging && HandsTogether();
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
        if (gun.Charging || !gun.Held || gun.NeedsRelease || !gun.CooldownOver) return;

        if (Ammo <= 0)
        {
            gun.NeedsRelease = true;
            gun.PlayEmpty();
            return;
        }
        gun.BeginCharge();
    }

    bool HandsTogether()
    {
        // Editor では、マウスの左右ボタンを同時に押している間は「両手が合わさっている」とみなす
        if (PlayerView.IsEditorSimulation) return true;

        return Vector3.Distance(PlayerView.LeftHand.position, PlayerView.RightHand.position) <= comboDistance;
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

        if (gun.HasHit) DamageTarget(gun.LastHit.collider, damage);
    }

    /// <summary>両手撃ち。2 丁ぶんの弾を合体させて、1 発の太い弾にする。</summary>
    void FireCombo()
    {
        float chargeL = left.EndCharge();
        float chargeR = right.EndCharge();

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
        float length = hasHit ? hit.distance : 20f;

        ShowBeam(origin, direction, Mathf.Min(length, 20f));

        float bigger = Mathf.Max(chargeL, chargeR);
        left.PlayShotFeedback(bigger, fireInterval * 2f);
        right.PlayShotFeedback(bigger, fireInterval * 2f);
        left.ShowMessage("COMBO!", ComboColor, 0.7f);
        right.ShowMessage("COMBO!", ComboColor, 0.7f);

        if (hasHit) DamageTarget(hit.collider, damage);
    }

    static void DamageTarget(Collider col, float damage)
    {
        Target target = col.GetComponentInParent<Target>();
        if (target != null) target.TakeDamage(damage);
    }

    /// <summary>両手撃ちの弾の軌跡を、太い光の線で一瞬だけ見せる。</summary>
    static void ShowBeam(Vector3 origin, Vector3 direction, float length)
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "ComboBeam";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.position = origin + direction * (length * 0.5f);
        beam.transform.rotation = Quaternion.LookRotation(direction);
        beam.transform.localScale = new Vector3(0.05f, 0.05f, length);
        beam.GetComponent<Renderer>().material.color = ComboColor;
        Destroy(beam, 0.15f);
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
        }
        else
        {
            // 残弾回復アイテム。最大グレードでのグレードアップ品も、残弾の補給として使う。
            Ammo = Mathf.Min(maxAmmo, Ammo + ammoPerPack);
            message = "AMMO +" + ammoPerPack;
            color = AmmoUpColor;
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
    }

    void ApplyGradeVisuals()
    {
        if (left != null) left.SetGrade(Grade);
        if (right != null) right.SetGrade(Grade);
    }

    void RefreshLabels()
    {
        if (left == null || right == null) return;

        string text = "AMMO " + Ammo + "/" + maxAmmo + "\nGRADE " + Grade;
        Color color = Ammo <= lowAmmoGuaranteedDrop ? LowAmmoColor : Color.white;
        left.SetStatus(text, color);
        right.SetStatus(text, color);
    }
}

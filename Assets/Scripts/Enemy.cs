using UnityEngine;

/// <summary>敵の種類。地上を歩いてくるものと、空を飛んでくるもの。</summary>
public enum EnemyKind
{
    Ground,
    Flying,
}

/// <summary>地上の敵の種類。</summary>
public enum EnemyVariant
{
    Normal,
    Armored,   // 頑丈(鎧のゴブリン)
    Giant,     // 巨大(オーガ)
    Runner,    // 俊敏(小さくて速い)
}

/// <summary>敵の基本の性能。WaveSpawner の Inspector で調整できる。</summary>
[System.Serializable]
public class EnemyStats
{
    [Tooltip("体力。")]
    public float health = 3f;
    [Tooltip("進む速さ(m/秒)。")]
    public float speed = 2.5f;
    [Tooltip("砦に与えるダメージ(1 回あたり)。")]
    public float attackDamage = 3f;
    [Tooltip("攻撃の間隔(秒)。")]
    public float attackInterval = 1.6f;
    [Tooltip("倒したときの得点。")]
    public int score = 10;
    [Tooltip("大きさの倍率(1 = 標準)。")]
    public float sizeMultiplier = 1f;
    [Tooltip("倒したときに、通常の抽選とは別に、必ず落とすアイテムの数。")]
    public int bonusDrops = 0;

    public EnemyStats() { }

    public EnemyStats(float health, float speed, float attackDamage, float attackInterval, int score,
                      float sizeMultiplier = 1f, int bonusDrops = 0)
    {
        this.sizeMultiplier = sizeMultiplier;
        this.bonusDrops = bonusDrops;
        this.health = health;
        this.speed = speed;
        this.attackDamage = attackDamage;
        this.attackInterval = attackInterval;
        this.score = score;
    }
}

/// <summary>
/// 砦に向かって進み、到着したら近接攻撃で砦の耐久値を減らす敵。
/// ゴブリン(地上)とコウモリ(空中)を、標準の立体を組み合わせて作る。
/// 体力や倒されたときの処理は、同じオブジェクトに付ける Target が担当する。
/// </summary>
public class Enemy : MonoBehaviour
{
    const float AttackDuration = 0.5f;
    /// <summary>出現したとき、0 から本来の大きさまで、この秒数かけて大きくなる(いきなり現れて見えないように)。</summary>
    const float GrowSeconds = 1.2f;

    /// <summary>遠くにいるあいだの、進む速さの倍率(WaveSpawner が設定する)。</summary>
    public static float FarSpeedBoost = 2.5f;
    public static float FarMinSpeed = 6f;
    public static float BoostStartMeters = 30f;
    public static float BoostFullMeters = 70f;

    EnemyKind kind;
    Vector3 standPoint;
    Vector3 lookPoint;
    float speed;
    float attackDamage;
    float attackInterval;

    Target target;
    Transform model;
    Transform armRight;
    Transform armLeft;
    Transform legRight;
    Transform legLeft;
    Transform wingRight;
    Transform wingLeft;
    Transform hpBar;
    Transform hpFill;
    Renderer hpFillRenderer;

    float clock;
    float walkPhase;
    float age;
    float attackTimer;
    float attackAnim = -1f;
    bool damageDone;
    bool arrived;

    /// <summary>
    /// 敵を 1 体作る。
    /// standPoint = 到着したときに止まる位置、lookPoint = 到着後に攻撃する(向く)位置。
    /// </summary>
    public static Enemy Spawn(EnemyKind kind, Vector3 spawnPosition, Vector3 standPoint, Vector3 lookPoint,
                              float scale, float speedMultiplier, float healthMultiplier, EnemyStats stats,
                              EnemyVariant variant = EnemyVariant.Normal)
    {
        string rootName = kind == EnemyKind.Flying ? "Bat" : variant + "Goblin";
        var root = new GameObject(rootName);
        root.transform.position = spawnPosition;
        root.transform.localScale = Vector3.one * scale;

        Vector3 first = standPoint - spawnPosition;
        if (kind == EnemyKind.Ground) first.y = 0f;
        if (first.sqrMagnitude > 0.0001f) root.transform.rotation = Quaternion.LookRotation(first, Vector3.up);

        var enemy = root.AddComponent<Enemy>();
        enemy.kind = kind;
        enemy.standPoint = standPoint;
        enemy.lookPoint = lookPoint;
        enemy.speed = stats.speed * speedMultiplier;
        enemy.attackDamage = stats.attackDamage;
        enemy.attackInterval = stats.attackInterval;
        enemy.attackTimer = stats.attackInterval * 0.5f;

        var modelObject = new GameObject("Model");
        modelObject.transform.SetParent(root.transform, false);
        enemy.model = modelObject.transform;
        modelObject.transform.localScale = Vector3.zero;   // 出現したときは 0 から、ゆっくり大きくなる(Update)

        if (kind == EnemyKind.Ground)
        {
            enemy.BuildGoblin(variant);
            if (variant == EnemyVariant.Armored || variant == EnemyVariant.Giant) enemy.BuildHpBar(2.05f);
            // 体の当たり判定は、頭より下だけ。頭は別の当たり判定(HeadHitbox)にして、ヘッドショットを見分ける
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.55f, 0f);
            capsule.radius = 0.45f;
            capsule.height = 1.1f;
            enemy.AddHeadHitbox(new Vector3(0f, 1.33f, 0.05f), 0.33f);
        }
        else
        {
            enemy.BuildBat();
            // 体の当たり判定は、頭を含まない位置にずらす。頭は別の当たり判定(HeadHitbox)
            var sphere = root.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 0f, -0.25f);
            sphere.radius = 0.6f;
            enemy.AddHeadHitbox(new Vector3(0f, 0.08f, 0.42f), 0.26f);
        }

        // 見た目を作り終えてから、体力などを Target に渡す
        enemy.target = root.AddComponent<Target>();
        float dropHeight = kind == EnemyKind.Ground ? 1f * scale : 0f;
        enemy.target.Configure(stats.health * healthMultiplier, stats.score, dropHeight, stats.bonusDrops);
        return enemy;
    }

    /// <summary>頭の当たり判定を足す。ここに当たると、ヘッドショットで一撃になる(見た目には出ない)。</summary>
    void AddHeadHitbox(Vector3 localCenter, float radius)
    {
        var go = new GameObject("HeadHitbox");
        go.transform.SetParent(model, false);
        go.transform.localPosition = localCenter;
        go.AddComponent<SphereCollider>().radius = radius;
        go.AddComponent<HeadHitbox>();
    }

    // ----------------------------------------------------------------- 見た目

    static Transform Part(Transform parent, PrimitiveType type, string partName, Vector3 localPosition, Vector3 localScale,
                          Color color, Vector3 localEuler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = partName;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        Paint.Apply(go.GetComponent<Renderer>(), color);
        return go.transform;
    }

    static Transform Pivot(Transform parent, string pivotName, Vector3 localPosition)
    {
        var go = new GameObject(pivotName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    void BuildGoblin(EnemyVariant variant)
    {
        Color skin = new Color(0.38f, 0.62f, 0.25f);
        Color darkSkin = new Color(0.28f, 0.48f, 0.2f);
        Color cloth = new Color(0.45f, 0.3f, 0.15f);
        Color wood = new Color(0.4f, 0.25f, 0.12f);
        Color darkWood = new Color(0.3f, 0.18f, 0.08f);
        Color eye = new Color(1f, 0.15f, 0.1f);

        if (variant == EnemyVariant.Giant)
        {
            skin = new Color(0.62f, 0.5f, 0.36f);
            darkSkin = new Color(0.5f, 0.4f, 0.28f);
            cloth = new Color(0.5f, 0.15f, 0.12f);
        }
        else if (variant == EnemyVariant.Runner)
        {
            skin = new Color(0.65f, 0.78f, 0.25f);
            darkSkin = new Color(0.5f, 0.65f, 0.2f);
            eye = new Color(1f, 0.95f, 0.2f);
        }

        // 足(付け根で回して、歩く動きにする)
        legRight = Pivot(model, "LegRight", new Vector3(0.16f, 0.5f, 0f));
        legLeft = Pivot(model, "LegLeft", new Vector3(-0.16f, 0.5f, 0f));
        foreach (var leg in new[] { legRight, legLeft })
        {
            Part(leg, PrimitiveType.Cylinder, "Leg", new Vector3(0f, -0.25f, 0f), new Vector3(0.2f, 0.25f, 0.2f), darkSkin);
            Part(leg, PrimitiveType.Cube, "Foot", new Vector3(0f, -0.46f, 0.07f), new Vector3(0.22f, 0.08f, 0.32f), wood);
        }

        // 体、腰布、頭
        Part(model, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.82f, 0f), new Vector3(0.6f, 0.34f, 0.5f), skin);
        Part(model, PrimitiveType.Cube, "Loincloth", new Vector3(0f, 0.55f, 0f), new Vector3(0.56f, 0.2f, 0.42f), cloth);
        Part(model, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.3f, 0.05f), new Vector3(0.55f, 0.48f, 0.52f), skin);
        Part(model, PrimitiveType.Sphere, "Nose", new Vector3(0f, 1.25f, 0.3f), new Vector3(0.1f, 0.1f, 0.12f), darkSkin);
        Part(model, PrimitiveType.Sphere, "EyeRight", new Vector3(0.12f, 1.36f, 0.25f), Vector3.one * 0.09f, eye);
        Part(model, PrimitiveType.Sphere, "EyeLeft", new Vector3(-0.12f, 1.36f, 0.25f), Vector3.one * 0.09f, eye);
        // とがった耳
        Part(model, PrimitiveType.Cube, "EarRight", new Vector3(0.36f, 1.4f, 0f), new Vector3(0.34f, 0.09f, 0.05f), skin, new Vector3(0f, 0f, 20f));
        Part(model, PrimitiveType.Cube, "EarLeft", new Vector3(-0.36f, 1.4f, 0f), new Vector3(0.34f, 0.09f, 0.05f), skin, new Vector3(0f, 0f, -20f));

        // 腕(右手に棍棒)
        armRight = Pivot(model, "ArmRight", new Vector3(0.36f, 1.05f, 0f));
        armLeft = Pivot(model, "ArmLeft", new Vector3(-0.36f, 1.05f, 0f));
        Part(armRight, PrimitiveType.Cylinder, "Arm", new Vector3(0f, -0.22f, 0f), new Vector3(0.14f, 0.22f, 0.14f), skin);
        Part(armLeft, PrimitiveType.Cylinder, "Arm", new Vector3(0f, -0.22f, 0f), new Vector3(0.14f, 0.22f, 0.14f), skin);
        Part(armRight, PrimitiveType.Cylinder, "ClubShaft", new Vector3(0f, -0.55f, 0.05f), new Vector3(0.1f, 0.28f, 0.1f), wood);
        Part(armRight, PrimitiveType.Sphere, "ClubHead", new Vector3(0f, -0.86f, 0.05f), Vector3.one * 0.26f, darkWood);

        Color red = new Color(0.75f, 0.12f, 0.1f);
        if (variant == EnemyVariant.Armored)
        {
            // 鎧、兜、盾
            Color steel = new Color(0.62f, 0.65f, 0.7f);
            Color darkSteel = new Color(0.4f, 0.42f, 0.48f);
            Part(model, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 1.5f, 0.03f), new Vector3(0.6f, 0.34f, 0.58f), steel);
            Part(model, PrimitiveType.Cube, "HelmetCrest", new Vector3(0f, 1.7f, 0.03f), new Vector3(0.05f, 0.16f, 0.36f), red);
            Part(model, PrimitiveType.Cube, "ChestPlate", new Vector3(0f, 0.85f, 0.2f), new Vector3(0.62f, 0.5f, 0.14f), steel);
            Part(model, PrimitiveType.Sphere, "PauldronRight", new Vector3(0.4f, 1.12f, 0f), new Vector3(0.3f, 0.22f, 0.3f), darkSteel);
            Part(model, PrimitiveType.Sphere, "PauldronLeft", new Vector3(-0.4f, 1.12f, 0f), new Vector3(0.3f, 0.22f, 0.3f), darkSteel);
            Part(armLeft, PrimitiveType.Cube, "Shield", new Vector3(-0.1f, -0.35f, 0.18f), new Vector3(0.06f, 0.55f, 0.42f), darkSteel);
            Part(armLeft, PrimitiveType.Cube, "ShieldBoss", new Vector3(-0.15f, -0.35f, 0.18f), new Vector3(0.05f, 0.14f, 0.14f), steel);
        }
        else if (variant == EnemyVariant.Giant)
        {
            // 角、牙、大きなおなか
            Color bone = new Color(0.9f, 0.86f, 0.75f);
            Part(model, PrimitiveType.Cube, "HornRight", new Vector3(0.22f, 1.62f, 0.05f), new Vector3(0.08f, 0.3f, 0.08f), bone, new Vector3(0f, 0f, -25f));
            Part(model, PrimitiveType.Cube, "HornLeft", new Vector3(-0.22f, 1.62f, 0.05f), new Vector3(0.08f, 0.3f, 0.08f), bone, new Vector3(0f, 0f, 25f));
            Part(model, PrimitiveType.Cube, "TuskRight", new Vector3(0.12f, 1.15f, 0.27f), new Vector3(0.05f, 0.15f, 0.05f), bone, new Vector3(-20f, 0f, 0f));
            Part(model, PrimitiveType.Cube, "TuskLeft", new Vector3(-0.12f, 1.15f, 0.27f), new Vector3(0.05f, 0.15f, 0.05f), bone, new Vector3(-20f, 0f, 0f));
            Part(model, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.75f, 0.12f), new Vector3(0.62f, 0.5f, 0.5f), skin);
        }
        else if (variant == EnemyVariant.Runner)
        {
            // 赤いバンダナ
            Part(model, PrimitiveType.Cylinder, "Bandana", new Vector3(0f, 1.45f, 0.05f), new Vector3(0.56f, 0.03f, 0.53f), red);
        }
    }

    /// <summary>頭の上に、体力バーを付ける(いつもカメラのほうを向く)。</summary>
    void BuildHpBar(float y)
    {
        hpBar = Pivot(transform, "HpBar", new Vector3(0f, y, 0f));
        Part(hpBar, PrimitiveType.Cube, "HpBack", Vector3.zero, new Vector3(1.04f, 0.16f, 0.02f), new Color(0.08f, 0.08f, 0.08f));
        hpFill = Part(hpBar, PrimitiveType.Cube, "HpFill", new Vector3(0f, 0f, -0.02f), new Vector3(1f, 0.1f, 0.02f), Color.green);
        hpFillRenderer = hpFill.GetComponent<Renderer>();
    }

    void UpdateHpBar()
    {
        float fraction = target != null ? target.HealthFraction : 0f;
        float width = Mathf.Max(0.001f, fraction);
        hpFill.localScale = new Vector3(width, 0.1f, 0.02f);
        hpFill.localPosition = new Vector3(-(1f - width) * 0.5f, 0f, -0.02f);
        hpFillRenderer.material.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.9f, 0.25f), fraction);

        if (PlayerView.Eye != null)
        {
            Vector3 fromEye = hpBar.position - PlayerView.Eye.position;
            if (fromEye.sqrMagnitude > 0.0001f) hpBar.rotation = Quaternion.LookRotation(fromEye);
        }
    }

    void BuildBat()
    {
        Color fur = new Color(0.25f, 0.15f, 0.3f);
        Color wing = new Color(0.4f, 0.2f, 0.45f);
        Color eye = new Color(1f, 0.15f, 0.1f);

        Part(model, PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(0.5f, 0.42f, 0.7f), fur);
        Part(model, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.08f, 0.42f), new Vector3(0.34f, 0.3f, 0.34f), fur);
        Part(model, PrimitiveType.Cube, "EarRight", new Vector3(0.1f, 0.3f, 0.4f), new Vector3(0.08f, 0.22f, 0.05f), fur, new Vector3(0f, 0f, -15f));
        Part(model, PrimitiveType.Cube, "EarLeft", new Vector3(-0.1f, 0.3f, 0.4f), new Vector3(0.08f, 0.22f, 0.05f), fur, new Vector3(0f, 0f, 15f));
        Part(model, PrimitiveType.Sphere, "EyeRight", new Vector3(0.09f, 0.13f, 0.57f), Vector3.one * 0.07f, eye);
        Part(model, PrimitiveType.Sphere, "EyeLeft", new Vector3(-0.09f, 0.13f, 0.57f), Vector3.one * 0.07f, eye);
        Part(model, PrimitiveType.Cube, "FangRight", new Vector3(0.05f, -0.05f, 0.58f), new Vector3(0.03f, 0.09f, 0.03f), Color.white);
        Part(model, PrimitiveType.Cube, "FangLeft", new Vector3(-0.05f, -0.05f, 0.58f), new Vector3(0.03f, 0.09f, 0.03f), Color.white);
        Part(model, PrimitiveType.Cube, "Tail", new Vector3(0f, -0.02f, -0.5f), new Vector3(0.08f, 0.04f, 0.35f), fur);

        // 翼(付け根で回して、羽ばたく)
        wingRight = Pivot(model, "WingRight", new Vector3(0.2f, 0.08f, 0f));
        wingLeft = Pivot(model, "WingLeft", new Vector3(-0.2f, 0.08f, 0f));
        Part(wingRight, PrimitiveType.Cube, "WingInner", new Vector3(0.4f, 0f, -0.05f), new Vector3(0.7f, 0.03f, 0.55f), wing);
        Part(wingRight, PrimitiveType.Cube, "WingOuter", new Vector3(0.9f, 0f, -0.15f), new Vector3(0.4f, 0.03f, 0.35f), wing, new Vector3(0f, -15f, 0f));
        Part(wingLeft, PrimitiveType.Cube, "WingInner", new Vector3(-0.4f, 0f, -0.05f), new Vector3(0.7f, 0.03f, 0.55f), wing);
        Part(wingLeft, PrimitiveType.Cube, "WingOuter", new Vector3(-0.9f, 0f, -0.15f), new Vector3(0.4f, 0.03f, 0.35f), wing, new Vector3(0f, 15f, 0f));
    }

    // ----------------------------------------------------------------- 動き

    void Update()
    {
        // 倒された敵は、Target が小さくして消す
        if (target != null && target.IsDead) return;

        float dt = Time.deltaTime;
        clock += dt;
        if (age < GrowSeconds)
        {
            age += dt;
            model.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / GrowSeconds));
        }
        if (hpBar != null) UpdateHpBar();

        Fort fort = Fort.Instance;
        bool fortAlive = fort != null && !fort.IsDestroyed;

        if (fortAlive)
        {
            if (!arrived) Approach(dt);
            else Attack(dt, fort);
        }

        Animate(dt);
    }

    void Approach(float dt)
    {
        Vector3 to = standPoint - transform.position;
        if (kind == EnemyKind.Ground) to.y = 0f;

        float dist = to.magnitude;
        if (dist <= 0.05f)
        {
            arrived = true;
            return;
        }

        // 砦から遠いあいだは、速く進む(近づくにつれて、本来の速さになる)
        float farSpeed = Mathf.Max(speed * FarSpeedBoost, FarMinSpeed);
        float moveSpeed = Mathf.Lerp(speed, farSpeed, Mathf.InverseLerp(BoostStartMeters, BoostFullMeters, dist));

        Vector3 dir = to / dist;
        float step = Mathf.Min(dist, moveSpeed * dt);
        transform.position += dir * step;
        FaceTowards(dir, 8f, dt);
        walkPhase += dt * moveSpeed * 5f;

        if (dist - step <= 0.05f) arrived = true;
    }

    void Attack(float dt, Fort fort)
    {
        FaceTowards(lookPoint - transform.position, 6f, dt);

        if (attackAnim < 0f)
        {
            attackTimer += dt;
            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                attackAnim = 0f;
                damageDone = false;
            }
            return;
        }

        attackAnim += dt / AttackDuration;
        float hitAt = kind == EnemyKind.Ground ? 0.75f : 0.5f;
        if (!damageDone && attackAnim >= hitAt)
        {
            damageDone = true;
            fort.TakeDamage(attackDamage);
        }
        if (attackAnim >= 1f) attackAnim = -1f;
    }

    void FaceTowards(Vector3 direction, float rate, float dt)
    {
        // 地上の敵は横向きだけ、空中の敵は上下の向きも少しだけ変える
        direction.y = kind == EnemyKind.Ground ? 0f : direction.y * 0.5f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-rate * dt));
    }

    void Animate(float dt)
    {
        float attackK = attackAnim;
        float lunge = attackK >= 0f ? Mathf.Sin(attackK * Mathf.PI) : 0f;

        if (kind == EnemyKind.Ground)
        {
            float moving = arrived ? 0f : 1f;
            float swing = Mathf.Sin(walkPhase) * 35f * moving;
            legRight.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legLeft.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            armLeft.localRotation = Quaternion.Euler(swing * 0.6f - 10f, 0f, 0f);

            float armAngle;
            if (attackK < 0f)
            {
                armAngle = -25f - swing * 0.4f;
            }
            else if (attackK < 0.5f)
            {
                armAngle = Mathf.Lerp(-25f, -170f, attackK / 0.5f);   // 振りかぶる
            }
            else
            {
                armAngle = Mathf.Lerp(-170f, -10f, (attackK - 0.5f) / 0.5f);   // 振り下ろす
            }
            armRight.localRotation = Quaternion.Euler(armAngle, 0f, 0f);

            model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.08f * moving, lunge * 0.35f);
        }
        else
        {
            float flap = Mathf.Sin(clock * (arrived ? 9f : 14f)) * 45f;
            wingRight.localRotation = Quaternion.Euler(0f, 0f, flap);
            wingLeft.localRotation = Quaternion.Euler(0f, 0f, -flap);

            // 羽ばたきながら上下にゆれる。攻撃のときは、砦に向かって急降下する
            model.localPosition = new Vector3(0f, Mathf.Sin(clock * 2.2f) * 0.12f - lunge * 0.3f, lunge * 1.2f);
        }
    }
}

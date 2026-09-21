using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// バズーカの弾。銃口から飛んでいき、次のどれかで爆発して、まわりの敵にまとめてダメージを与える。
///   ・何かに当たったとき(敵、地面、柵など。地面なら、まわりの敵を巻き込む)
///   ・敵に当たらなくても、敵の近く(近接信管の距離)を通ったとき
/// 弾は 1 フレームごとに、進む分の線(レイ)で当たりを調べるので、速くても壁や敵をすり抜けない。
/// </summary>
public class Rocket : MonoBehaviour
{
    static readonly Color BodyColor = new Color(0.9f, 0.88f, 0.82f);
    static readonly Color NoseColor = new Color(0.95f, 0.2f, 0.1f);
    static readonly Color FinColor = new Color(0.25f, 0.26f, 0.3f);
    static readonly Color FlameColor = new Color(3f, 1.5f, 0.3f);
    static readonly Color SmokeColor = new Color(0.8f, 0.8f, 0.8f);

    /// <summary>撃ってからこの距離(m)までは、近接信管を働かせない(手元の近くで、空の敵などに反応して爆発しないように)。</summary>
    const float FuseArmDistance = 2.5f;
    /// <summary>爆発の中心を、当たった面から、面の外向きにずらす距離(m)。地面に当たったとき、爆発の球が地面に埋まらず、地面の上の敵を確実に巻き込む。</summary>
    const float SurfaceLift = 0.4f;

    static readonly Collider[] FuseBuffer = new Collider[48];

    Vector3 direction;
    float speed;
    float travelled;
    float fuseRadius;
    float remaining;
    float damage;
    float radius;
    int mask;
    float puffTimer;
    Transform flame;

    /// <summary>弾を撃つ。origin = 銃口、direction = 向き、maxDistance = ここまで飛んで何も当たらなければ消える。</summary>
    public static void Launch(Vector3 origin, Vector3 direction, float speed, float maxDistance,
                              float damage, float radius, float fuseRadius, LayerMask mask)
    {
        var go = new GameObject("Rocket");
        go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));

        Part(go.transform, PrimitiveType.Cube, "Body", new Vector3(0.09f, 0.09f, 0.4f), Vector3.zero, BodyColor);
        Part(go.transform, PrimitiveType.Cube, "Nose", new Vector3(0.065f, 0.065f, 0.12f), new Vector3(0f, 0f, 0.26f), NoseColor);
        Part(go.transform, PrimitiveType.Cube, "FinH", new Vector3(0.26f, 0.014f, 0.1f), new Vector3(0f, 0f, -0.16f), FinColor);
        Part(go.transform, PrimitiveType.Cube, "FinV", new Vector3(0.014f, 0.26f, 0.1f), new Vector3(0f, 0f, -0.16f), FinColor);
        var flameGo = Part(go.transform, PrimitiveType.Sphere, "Flame", new Vector3(0.16f, 0.16f, 0.36f), new Vector3(0f, 0f, -0.32f), FlameColor);

        var rocket = go.AddComponent<Rocket>();
        rocket.direction = direction.normalized;
        rocket.speed = speed;
        rocket.remaining = maxDistance;
        rocket.damage = damage;
        rocket.radius = radius;
        rocket.fuseRadius = fuseRadius;
        rocket.mask = mask;
        rocket.flame = flameGo.transform;
    }

    static GameObject Part(Transform parent, PrimitiveType type, string partName, Vector3 scale, Vector3 localPos, Color color)
    {
        var part = Paint.Prim(type, partName, color);
        part.transform.SetParent(parent, false);
        part.transform.localScale = scale;
        part.transform.localPosition = localPos;
        return part;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float step = speed * dt;
        if (step <= 0f) return;

        Vector3 position = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(position, direction, out hit, step, mask, QueryTriggerInteraction.Ignore))
        {
            // 当たった面の外側(地面なら少し上)で爆発させる
            Detonate(hit.point + hit.normal * SurfaceLift, radius, damage);
            Destroy(gameObject);
            return;
        }

        transform.position = position + direction * step;
        remaining -= step;
        travelled += step;

        // 近接信管:敵の近くを通ったら、当たらなくても、その場で爆発する
        if (fuseRadius > 0f && travelled >= FuseArmDistance && EnemyNearby(transform.position))
        {
            Detonate(transform.position, radius, damage);
            Destroy(gameObject);
            return;
        }
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 後ろの炎はゆらゆらと、煙は少しずつ残す
        float flicker = 0.75f + 0.5f * Random.value;
        flame.localScale = new Vector3(0.16f * flicker, 0.16f * flicker, 0.36f * (0.7f + 0.6f * Random.value));

        puffTimer -= dt;
        if (puffTimer <= 0f)
        {
            puffTimer = 0.035f;
            ShotBurst.Spawn(ShotBurst.Shape.Sphere, transform.position - direction * 0.35f, Vector3.up,
                            1, 0.4f, 0.13f, SmokeColor, 0.45f, 0f, -0.3f, 2f);
        }
    }

    bool EnemyNearby(Vector3 center)
    {
        int count = Physics.OverlapSphereNonAlloc(center, fuseRadius, FuseBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Target target = FuseBuffer[i].GetComponentInParent<Target>();
            if (target != null && !target.IsDead) return true;
        }
        return false;
    }

    /// <summary>爆発。radius(m)の中の敵に、中心に近いほど大きなダメージを与える(端でも 50%)。頭の判定は関係なく、体に当たった扱い。</summary>
    public static void Detonate(Vector3 center, float radius, float damage)
    {
        // 敵ごとに、いちばん近いコライダーまでの距離を調べる
        var nearest = new Dictionary<Target, float>();
        foreach (var col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Target target = col.GetComponentInParent<Target>();
            if (target == null || target.IsDead) continue;

            float distance = Vector3.Distance(center, col.ClosestPoint(center));
            float known;
            if (!nearest.TryGetValue(target, out known) || distance < known) nearest[target] = distance;
        }

        int count = 0;
        foreach (var pair in nearest)
        {
            float k = Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(radius * 0.3f, radius, pair.Value));
            pair.Key.TakeDamage(damage * k);
            count++;
        }

        Debug.Log($"[Bazooka] 爆発: 半径 {radius:0.0} m、基本ダメージ {damage:0.0}、巻き込んだ敵 {count} 体");

        ExplosionFx.Spawn(center, radius);
        GameAudio.PlayBazookaBoom(center);
        HitStop.Trigger(0.05f, 0.3f);
        if (GunSystem.Instance != null) GunSystem.Instance.PulseHaptics(0.6f, 0.12f);
    }
}

/// <summary>爆発の見た目。火の玉がふくらんでしぼみ、火花・破片・地面の衝撃波の輪・上へ昇る煙が出る。</summary>
public class ExplosionFx : MonoBehaviour
{
    static readonly Color FireColor = new Color(3f, 1.1f, 0.25f);
    static readonly Color CoreColor = new Color(3f, 2.8f, 1.8f);
    static readonly Color SparkColor = new Color(1f, 0.75f, 0.25f);
    static readonly Color DebrisColor = new Color(0.22f, 0.17f, 0.12f);
    static readonly Color SmokeColor = new Color(0.45f, 0.45f, 0.47f);

    const float Life = 0.55f;

    Transform fire;
    Transform core;
    float radius;
    float age;

    public static void Spawn(Vector3 center, float radius)
    {
        var go = new GameObject("Explosion");
        go.transform.position = center;
        var fx = go.AddComponent<ExplosionFx>();
        fx.radius = radius;

        var f = Paint.Prim(PrimitiveType.Sphere, "Fire", FireColor);
        f.transform.SetParent(go.transform, false);
        fx.fire = f.transform;
        var c = Paint.Prim(PrimitiveType.Sphere, "Core", CoreColor);
        c.transform.SetParent(go.transform, false);
        fx.core = c.transform;
        fx.Apply(0f);

        ShotBurst.Spawn(ShotBurst.Shape.Sphere, center, Vector3.up, 20, radius * 3.2f, 0.16f, SparkColor, 0.8f, 0f, 6f, 2.5f);
        ShotBurst.Spawn(ShotBurst.Shape.Sphere, center, Vector3.up, 10, radius * 2f, 0.22f, DebrisColor, 1.0f, 0f, 9f, 2f);
        ShotBurst.Spawn(ShotBurst.Shape.Ring, center, Vector3.up, 18, radius * 3.5f, 0.1f, SparkColor, 0.45f, 0f, 0f, 3f);
        ShotBurst.Spawn(ShotBurst.Shape.Cone, center, Vector3.up, 8, radius * 1.6f, 0.3f, SmokeColor, 1.0f, 0f, -1.5f, 1.5f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= Life)
        {
            Destroy(gameObject);
            return;
        }
        Apply(age / Life);
    }

    /// <summary>t(0〜1)に合わせて、火の玉の大きさを決める。最初の 30% でふくらみ、残りでしぼむ。</summary>
    void Apply(float t)
    {
        float grow = t < 0.3f ? Mathf.Sqrt(t / 0.3f) : 1f;
        float shrink = t < 0.3f ? 1f : 1f - (t - 0.3f) / 0.7f;
        float size = radius * 1.3f * grow * shrink;
        fire.localScale = Vector3.one * Mathf.Max(0.01f, size);
        core.localScale = Vector3.one * Mathf.Max(0.01f, size * 0.6f * (1f - t));
    }
}

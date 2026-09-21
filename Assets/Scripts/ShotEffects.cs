using UnityEngine;

/// <summary>
/// 撃った弾の見た目。銃口の光、飛んでいく弾、弾道(軌跡の線)、着弾の光を、標準の立体で作る。
/// 当たり判定は撃った瞬間に決まっている(GunSystem)。ここは見た目だけ。
/// 遠くへ撃つほど、線と光を太くして、見えるようにしている。
/// </summary>
public class ShotEffects : MonoBehaviour
{
    enum Mode { Bullet, Trail }

    const float BulletSpeed = 120f;
    const float TrailSeconds = 0.5f;

    Mode mode;

    // 弾(飛んでいく光)
    Vector3 origin;
    Vector3 direction;
    float total;
    float length;
    float travelled;
    bool hit;
    bool impactShown;
    float charge01;
    float big;
    Color color;

    // 弾道(残る線)
    float age;
    float life;
    float thickness;

    /// <summary>
    /// 1 発ぶんの演出。muzzle = 銃口、target = 弾の行き先、hit = 何かに当たったか。
    /// トリガーを長く引いたため撃ち(big が大きい)ほど、光も火花も衝撃波も派手になる。
    /// </summary>
    public static void Fire(Vector3 muzzle, Vector3 target, bool hit, float charge01)
    {
        Vector3 delta = target - muzzle;
        float distance = delta.magnitude;
        if (distance < 0.05f) return;
        Vector3 dir = delta / distance;

        // ため撃ちの派手さ(0〜1)。チャージ 4 割から効き始めて、満タンで最大
        float big = Mathf.Clamp01(Mathf.InverseLerp(0.4f, 1f, charge01));

        Color color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.45f, 0.1f), charge01);
        Color hot = Color.Lerp(color, Color.white, 0.6f);
        float boost = DistanceBoost(distance);

        // 銃口の光(ため撃ちは、大きな光と、白い芯)
        Flash("MuzzleFlash", muzzle, 0.05f + 0.06f * charge01 + 0.2f * big, color, 0.06f + 0.08f * big);
        if (big > 0.2f) Flash("MuzzleCore", muzzle, 0.03f + 0.12f * big, Color.white, 0.05f + 0.06f * big);

        // 銃口の火花(前へ扇状に飛ぶ)と、衝撃波の輪
        if (big > 0.1f)
        {
            ShotBurst.Spawn(ShotBurst.Shape.Cone, muzzle, dir, 4 + Mathf.RoundToInt(16f * big),
                            5f + 9f * big, 0.02f + 0.02f * big, hot, 0.35f + 0.2f * big, 0f, 3f);
        }
        if (big > 0.3f)
        {
            ShotBurst.Spawn(ShotBurst.Shape.Ring, muzzle + dir * 0.1f, dir, 12,
                            2.5f + 3f * big, 0.03f + 0.02f * big, color, 0.3f, 0f, 2f);
        }

        // 弾道(銃口から着弾点までの線。撃った瞬間に出て、少しずつ細くなって消える。ため撃ちは太く、長く残り、白っぽい)
        float trailSeconds = TrailSeconds * (1f + 0.8f * big);
        Trail(muzzle, target, Mathf.Lerp(0.012f, 0.03f, charge01) * (1f + 1.2f * big),
              Color.Lerp(color, Color.white, 0.35f + 0.4f * big), trailSeconds);

        // 弾道に沿って、衝撃波の輪が、弾が通り過ぎるタイミングで次々に広がる(強いため撃ちだけ)
        if (big > 0.5f)
        {
            int rings = 2 + Mathf.RoundToInt(3f * big);
            float span = Mathf.Min(distance, 70f);
            for (int i = 1; i <= rings; i++)
            {
                float dist = span * i / (rings + 1f);
                float b = DistanceBoost(dist);
                ShotBurst.Spawn(ShotBurst.Shape.Ring, muzzle + dir * dist, dir, 10,
                                (2f + 2.5f * big) * b, 0.05f * b, color, 0.45f, dist / BulletSpeed, 0f, 1f);
            }
        }

        // 飛んでいく弾(明るく太い光。ため撃ちは、さらに大きく長く)
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Tracer";
        Destroy(go.GetComponent<Collider>());
        Paint.Apply(go.GetComponent<Renderer>(), Color.Lerp(color, Color.white, 0.5f * big));

        var fx = go.AddComponent<ShotEffects>();
        fx.mode = Mode.Bullet;
        fx.origin = muzzle;
        fx.direction = dir;
        fx.total = distance;
        fx.length = Mathf.Lerp(0.6f, 2f, charge01) * boost * (1f + big);
        fx.hit = hit;
        fx.charge01 = charge01;
        fx.big = big;
        fx.color = color;

        float bulletThickness = Mathf.Lerp(0.03f, 0.08f, charge01) * boost * (1f + 1.2f * big);
        go.transform.localScale = new Vector3(bulletThickness, bulletThickness, 0.01f);
        go.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(dir));
    }

    /// <summary>from から to へ、しばらく残る光の線を引く。両手撃ちの弾にも使う。</summary>
    public static void Trail(Vector3 from, Vector3 to, float baseThickness, Color color, float seconds)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.05f) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Trail";
        Destroy(go.GetComponent<Collider>());
        Paint.Apply(go.GetComponent<Renderer>(), color);

        float thickness = baseThickness * DistanceBoost(distance);
        go.transform.SetPositionAndRotation(from + delta * 0.5f, Quaternion.LookRotation(delta / distance));
        go.transform.localScale = new Vector3(thickness, thickness, distance);

        var fx = go.AddComponent<ShotEffects>();
        fx.mode = Mode.Trail;
        fx.life = seconds;
        fx.thickness = thickness;
    }

    /// <summary>遠くへ撃つほど、線や光を太くする(1〜3 倍)。</summary>
    static float DistanceBoost(float distance)
    {
        return Mathf.Lerp(1f, 3f, Mathf.Clamp01(distance / 60f));
    }

    void Update()
    {
        if (mode == Mode.Trail)
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / life);
            float t = thickness * (1f - k);
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(t, t, scale.z);
            if (age >= life) Destroy(gameObject);
            return;
        }

        travelled += BulletSpeed * Time.deltaTime;

        float head = Mathf.Min(travelled, total);
        float tail = Mathf.Clamp(travelled - length, 0f, total);

        if (travelled >= total && !impactShown)
        {
            impactShown = true;
            if (hit)
            {
                Vector3 point = origin + direction * total;
                float size = Mathf.Max(0.15f + 0.25f * charge01, total * 0.008f) * (1f + 1.5f * big);
                Flash("Impact", point, size, color, 0.15f + 0.1f * big);

                // ため撃ちの着弾:四方への火花と、衝撃波の輪、重い衝撃音
                if (big > 0.1f)
                {
                    float b = DistanceBoost(total);
                    Color hot = Color.Lerp(color, Color.white, 0.6f);
                    ShotBurst.Spawn(ShotBurst.Shape.Sphere, point, -direction, 8 + Mathf.RoundToInt(20f * big),
                                    (3f + 6f * big) * b, 0.04f * b * (0.8f + big), hot, 0.5f, 0f, 6f);
                    if (big > 0.5f)
                    {
                        ShotBurst.Spawn(ShotBurst.Shape.Ring, point, -direction, 16,
                                        (2f + 3f * big) * b, 0.06f * b, color, 0.4f, 0f, 2f);
                    }
                }
                GameAudio.PlayImpact(point, big);
            }
        }

        if (tail >= total)
        {
            Destroy(gameObject);
            return;
        }

        float visibleLength = Mathf.Max(0.01f, head - tail);
        Vector3 s = transform.localScale;
        s.z = visibleLength;
        transform.localScale = s;
        transform.position = origin + direction * ((head + tail) * 0.5f);
    }

    static void Flash(string flashName, Vector3 position, float size, Color color, float seconds)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = flashName;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * size;
        Paint.Apply(go.GetComponent<Renderer>(), color);
        Destroy(go, seconds);
    }
}

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
    Color color;

    // 弾道(残る線)
    float age;
    float life;
    float thickness;

    /// <summary>1 発ぶんの演出。muzzle = 銃口、target = 弾の行き先、hit = 何かに当たったか。</summary>
    public static void Fire(Vector3 muzzle, Vector3 target, bool hit, float charge01)
    {
        Vector3 delta = target - muzzle;
        float distance = delta.magnitude;
        if (distance < 0.05f) return;

        Color color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.45f, 0.1f), charge01);
        float boost = DistanceBoost(distance);

        Flash("MuzzleFlash", muzzle, 0.05f + 0.06f * charge01, color, 0.06f);

        // 弾道(銃口から着弾点までの線。撃った瞬間に出て、少しずつ細くなって消える)
        Trail(muzzle, target, Mathf.Lerp(0.012f, 0.03f, charge01), Color.Lerp(color, Color.white, 0.35f), TrailSeconds);

        // 飛んでいく弾(明るく太い光)
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Tracer";
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material.color = color;

        var fx = go.AddComponent<ShotEffects>();
        fx.mode = Mode.Bullet;
        fx.origin = muzzle;
        fx.direction = delta / distance;
        fx.total = distance;
        fx.length = Mathf.Lerp(0.6f, 2f, charge01) * boost;
        fx.hit = hit;
        fx.charge01 = charge01;
        fx.color = color;

        float bulletThickness = Mathf.Lerp(0.03f, 0.08f, charge01) * boost;
        go.transform.localScale = new Vector3(bulletThickness, bulletThickness, 0.01f);
        go.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(fx.direction));
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
        go.GetComponent<Renderer>().material.color = color;

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
                float size = Mathf.Max(0.15f + 0.25f * charge01, total * 0.008f);
                Flash("Impact", point, size, color, 0.15f);
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
        go.GetComponent<Renderer>().material.color = color;
        Destroy(go, seconds);
    }
}

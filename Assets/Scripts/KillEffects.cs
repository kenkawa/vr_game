using UnityEngine;

/// <summary>
/// 敵を倒したときの演出。標準の立体だけで作る。
///   ・白からオレンジへ変わりながら広がる光の玉(一瞬)
///   ・敵の体の色をした破片が、四方へ飛び散って落ちる
///   ・得点の数字(+10 など)が、上へ浮かんで消える
/// 遠くの敵でも見えるように、プレイヤーから遠いほど大きく作る。
/// </summary>
public class KillEffects : MonoBehaviour
{
    enum Mode { Debris, Flash, Popup }

    Mode mode;
    float age;
    float life;
    Vector3 velocity;
    Vector3 spin;
    Vector3 startScale;
    float startSize;
    float endSize;
    TextMesh text;
    Color textColor;

    /// <summary>倒された位置(center)に、演出を出す。colors は敵の体の色、scale は敵の大きさ。</summary>
    public static void Play(Vector3 center, Color[] colors, float scale, int score, bool headshot = false)
    {
        float distance = PlayerView.Eye != null ? Vector3.Distance(PlayerView.Eye.position, center) : 10f;
        // 遠いほど大きく見せる(1〜2.4 倍)
        float boost = Mathf.Lerp(1f, 2.4f, Mathf.Clamp01(distance / 40f));
        float size = Mathf.Max(0.5f, scale) * boost;

        SpawnFlash(center, size);
        SpawnDebris(center, colors, size);
        SpawnPopup(center, score, distance, headshot);
    }

    /// <summary>
    /// ヘッドショットの派手な演出(頭の位置に)。大きな光の玉、金と赤の火花、
    /// プレイヤーのほうを向いた衝撃波の輪、水平に広がる輪、真上に噴き上がる火花。
    /// </summary>
    public static void HeadshotBurst(Vector3 head, float scale)
    {
        float distance = PlayerView.Eye != null ? Vector3.Distance(PlayerView.Eye.position, head) : 10f;
        float boost = Mathf.Lerp(1f, 2.6f, Mathf.Clamp01(distance / 40f));
        float size = Mathf.Max(0.5f, scale) * boost;

        Color gold = new Color(1f, 0.85f, 0.2f);
        Color red = new Color(1f, 0.25f, 0.1f);
        Vector3 toEye = PlayerView.Eye != null ? (PlayerView.Eye.position - head).normalized : Vector3.back;

        SpawnFlash(head, size * 1.6f);
        ShotBurst.Spawn(ShotBurst.Shape.Sphere, head, Vector3.up, 22, 7f * boost, 0.07f * size, gold, 0.7f, 0f, 4f, 3f);
        ShotBurst.Spawn(ShotBurst.Shape.Sphere, head, Vector3.up, 14, 4f * boost, 0.1f * size, red, 0.8f, 0f, 3f, 3f);
        ShotBurst.Spawn(ShotBurst.Shape.Ring, head, toEye, 16, 7f * boost, 0.08f * size, red, 0.5f, 0f, 0f, 2f);
        ShotBurst.Spawn(ShotBurst.Shape.Ring, head, Vector3.up, 16, 6f * boost, 0.08f * size, gold, 0.6f, 0f, 0f, 2f);
        ShotBurst.Spawn(ShotBurst.Shape.Cone, head, Vector3.up, 12, 10f * boost, 0.07f * size, gold, 0.8f, 0f, 6f, 1.5f);
    }

    static void SpawnFlash(Vector3 center, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "KillFlash";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = center;
        go.transform.localScale = Vector3.one * size * 0.4f;
        Paint.Apply(go.GetComponent<Renderer>(), Color.white);

        var fx = go.AddComponent<KillEffects>();
        fx.mode = Mode.Flash;
        fx.life = 0.22f;
        fx.startSize = size * 0.4f;
        fx.endSize = size * 2.2f;
    }

    static void SpawnDebris(Vector3 center, Color[] colors, float size)
    {
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "KillDebris";
            Destroy(go.GetComponent<Collider>());

            Color color = (colors != null && colors.Length > 0) ? colors[Random.Range(0, colors.Length)] : Color.white;
            Paint.Apply(go.GetComponent<Renderer>(), color);

            float piece = Random.Range(0.08f, 0.2f) * size;
            go.transform.position = center + Random.insideUnitSphere * 0.2f * size;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * piece;

            var fx = go.AddComponent<KillEffects>();
            fx.mode = Mode.Debris;
            fx.life = Random.Range(0.7f, 1.1f);
            fx.startScale = go.transform.localScale;
            Vector3 outward = Random.onUnitSphere;
            outward.y = Mathf.Abs(outward.y) * 0.6f + 0.4f;   // 上寄りに飛ばす
            fx.velocity = outward.normalized * Random.Range(3f, 7f) * Mathf.Sqrt(size);
            fx.spin = Random.onUnitSphere * Random.Range(180f, 540f);
        }
    }

    static void SpawnPopup(Vector3 center, int score, float distance, bool headshot)
    {
        // 遠いほど文字を大きくして、読めるようにする(ヘッドショットは、さらに大きく、赤く)
        float characterSize = Mathf.Clamp(distance * 0.012f, 0.05f, 0.5f) * (headshot ? 1.35f : 1f);
        Color gold = headshot ? new Color(1f, 0.3f, 0.15f) : new Color(1f, 0.85f, 0.2f);

        TextMesh tm = HudText.Create("KillScore", null, 64, characterSize, gold);
        tm.text = headshot ? "HEADSHOT!\n+" + score : "+" + score;
        tm.transform.position = center + Vector3.up * 0.6f;

        var fx = tm.gameObject.AddComponent<KillEffects>();
        fx.mode = Mode.Popup;
        fx.life = headshot ? 1.4f : 1f;
        fx.text = tm;
        fx.textColor = gold;
        fx.velocity = Vector3.up * Mathf.Max(1.5f, distance * 0.05f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / life);

        switch (mode)
        {
            case Mode.Flash:
            {
                float size = Mathf.Lerp(startSize, endSize, Mathf.Sqrt(k));
                transform.localScale = Vector3.one * size;
                GetComponent<Renderer>().material.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.1f), k);
                break;
            }
            case Mode.Debris:
            {
                velocity += Physics.gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                transform.Rotate(spin * Time.deltaTime, Space.Self);
                transform.localScale = startScale * (1f - k * k);
                break;
            }
            case Mode.Popup:
            {
                transform.position += velocity * Time.deltaTime;
                if (PlayerView.Eye != null)
                {
                    Vector3 fromEye = transform.position - PlayerView.Eye.position;
                    if (fromEye.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(fromEye);
                }
                Color c = textColor;
                c.a = 1f - k * k;
                text.color = c;
                break;
            }
        }

        if (age >= life) Destroy(gameObject);
    }
}

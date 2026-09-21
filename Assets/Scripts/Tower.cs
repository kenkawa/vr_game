using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ステージ 2 の舞台:プレイヤーが頂上に立つ、高い塔(とう)。標準の立体と、プログラムで作る模様(石のレンガなど)で、起動時に作る。
///   ・プレイヤーは、塔の頂上の丸い足場(既定は半径 0.9m)の真ん中に立つ。足場のふちには、何も立てない(手すりは、目の前で視界の邪魔だったので、なくした。床に金の線があるだけ)
///   ・塔のまわりは、全方向(360 度)から敵が来る。地上の敵は、塔をぐるりと囲む外柵(木の柵の輪)まで来て、柵と塔を攻撃する。
///     空の敵は、頂上のまわりを飛び回って襲ってくる
///   ・耐久値が 0 になると、ゲームオーバー
///
/// 外柵を塔の足元ではなく、遠く(既定 15.5m)の輪にしている理由:
///   頂上(高さ 12m)の真ん中に立つと、足場のふちに隠れて、塔の足元の地面は見えない。見えはじめる距離は、足場の半径の約 8.5 倍
///   (足場の半径 × (1 + 塔の高さ ÷ 目の高さ 1.6m)。半径 0.9m なら約 7.7m、1.6m なら約 13.6m)。そこで、柵の輪を、見えはじめる距離の外に置いて、
///   敵が柵で止まったとき、頭を少し下へ向けるだけで、全員が見えて、撃てるようにした。
///
/// 柵は、視界をさえぎらないように、低くて、すき間のあるものにしてある(既定 0.5m。頂上の目の高さから見ると、柵のすぐ外の敵の足元への視線は、
/// 柵の位置で地面から約 0.75m を通るので、それより低ければ、いちばん手前の敵も全身が見える)。
///
/// 位置の基準は、PlayerView の足元の中心(塔の中心軸)と、正面の向き。
/// 周りの景色(山・森・岩・草など)は Scenery.BuildRing が作る。
/// </summary>
public class Tower : MonoBehaviour, IStronghold
{
    /// <summary>頂上の足場の半径(m)の既定値。狭いほど、頂上に立っている感じが強くなり、塔の足元の近くまで、地面が見える。</summary>
    public const float DefaultPlatformRadius = 0.9f;

    float platformRadius = DefaultPlatformRadius;
    /// <summary>頂上の足場の半径(m)。</summary>
    public float PlatformRadius => platformRadius;

    static readonly Color GroundTint = new Color(0.9f, 0.95f, 1f);
    static readonly Color DirtTint = new Color(0.95f, 0.9f, 0.9f);
    static readonly Color PlazaTint = new Color(0.82f, 0.78f, 0.74f);
    static readonly Color StoneColor = new Color(0.74f, 0.73f, 0.78f);
    static readonly Color TrimColor = new Color(0.88f, 0.84f, 0.78f);
    static readonly Color WoodColor = new Color(0.36f, 0.24f, 0.14f);
    static readonly Color DarkWoodColor = new Color(0.25f, 0.15f, 0.08f);
    static readonly Color GateWoodColor = new Color(0.66f, 0.46f, 0.30f);
    static readonly Color IronColor = new Color(0.14f, 0.15f, 0.18f);
    static readonly Color GoldColor = new Color(1f, 0.78f, 0.3f);
    static readonly Color RedColor = new Color(0.62f, 0.09f, 0.12f);
    static readonly Color DamageColor = new Color(1f, 0.25f, 0.2f);

    // 塔の胴の 4 段(下から)。段ごとに、細くなる。頂上に近い段ほど、足場より細くして、足場のふちで視界が切れるようにする
    static readonly float[] SegmentHeights = { 3.5f, 3.5f, 3f, 1.75f };
    static readonly float[] SegmentRadii = { 3.4f, 2.6f, 1.95f, 1.3f };

    float maxHealth = 100f;
    float height = 12f;
    float fenceRadius = 15.5f;
    float fenceHeight = 0.5f;
    readonly List<Renderer> stone = new List<Renderer>();
    readonly List<Color> stoneColors = new List<Color>();

    public string Label => "TOWER";
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => Health <= 0f;
    public float GroundY => PlayerView.FloorY - height;
    public float TopY => PlayerView.FloorY;
    /// <summary>外柵の輪の半径(m)。地上の敵は、この外側で止まる。</summary>
    public float FenceRadius => fenceRadius;

    /// <summary>柵が影を落とすか。柵の杭は数が多いので、既定では落とさない。Build の前に設定する。</summary>
    public bool CastShadows { get; set; } = false;

    /// <summary>地面の明るさ(1 = 元のまま)。小さいほど暗くなって、明るい色の敵が目立つ。Build の前に設定する。</summary>
    public float GroundBrightness { get; set; } = 1f;

    Color Darken(Color c)
    {
        return new Color(c.r * GroundBrightness, c.g * GroundBrightness, c.b * GroundBrightness, 1f);
    }

    public event Action<float> Damaged;
    public event Action Broken;

    void OnDestroy()
    {
        if (Stronghold.Current == (IStronghold)this) Stronghold.Current = null;
    }

    /// <summary>足元の中心軸から、正面を 0 度、右を + として angleDegrees の方向の、水平な単位ベクトル。</summary>
    public static Vector3 Direction(float angleDegrees)
    {
        return Quaternion.AngleAxis(angleDegrees, Vector3.up) * PlayerView.FlatForward;
    }

    /// <summary>足元の中心軸の、高さ y の点。</summary>
    public static Vector3 Axis(float y)
    {
        return new Vector3(PlayerView.Center.x, y, PlayerView.Center.z);
    }

    // ----------------------------------------------------------------- 作る

    /// <summary>塔を作る。PlayerView.Ensure() のあとに呼ぶ。</summary>
    public void Build(float maxHealthValue, float heightMeters, float fenceRadiusMeters, float fenceHeightMeters = 0.5f,
                      float platformRadiusMeters = DefaultPlatformRadius)
    {
        Stronghold.Current = this;
        maxHealth = maxHealthValue;
        height = heightMeters;
        fenceRadius = fenceRadiusMeters;
        fenceHeight = Mathf.Max(0f, fenceHeightMeters);
        platformRadius = Mathf.Clamp(platformRadiusMeters, 0.4f, 3f);
        Health = maxHealth;

        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        float top = TopY;
        float ground = GroundY;
        const float tile = ProcTex.TileMeters;

        // 塔の胴の半径。上の段ほど細くするが、足場が狭いほど、上の段を、もっと細くする。
        // 頂上の真ん中(目の高さは、足場の床から 1.6m)から、足場のふちの上を通る視線は、下へ向かって、外へ広がる(円すい)。
        // 胴が、この円すいの外へはみ出すと、塔の足元の地面が見えなくなるので、円すいの内側におさめる
        float[] radii = new float[SegmentRadii.Length];
        float built = 0f;
        for (int i = 0; i < radii.Length; i++)
        {
            built += SegmentHeights[i];
            float depth = Mathf.Max(0.01f, height - built);                       // 段の上のふちの、足場の床からの深さ
            float allowed = platformRadius * (1f + depth / 1.6f) * 0.92f;
            radii[i] = Mathf.Min(SegmentRadii[i], allowed);
        }
        radii[radii.Length - 1] = Mathf.Min(radii[radii.Length - 1], platformRadius - 0.05f);

        Texture2D stoneTex = ProcTex.Stone();
        Texture2D flagTex = ProcTex.Flagstone();
        Texture2D plankTex = ProcTex.Planks();

        var trim = new MeshKit.Batch();     // 明るい石の飾り(段の境目の帯、門の枠)
        var iron = new MeshKit.Batch();     // 鉄(矢を撃つ細い窓、門の帯、たいまつの受け)
        var wood = new MeshKit.Batch();     // 木(旗ざお、かがり火の柱、バナーの棒)
        var posts = new MeshKit.Batch();    // 柵の杭
        var beams = new MeshKit.Batch();    // 柵の横木
        Mesh cube = MeshKit.Cube();
        Mesh cyl6 = MeshKit.Cylinder(6);
        Mesh cyl8 = MeshKit.Cylinder(8);
        Mesh cone6 = MeshKit.Cone(6);

        // 地面(草)。当たり判定あり:銃やバズーカが地面に当たる。塔のまわりに、土の輪と、敷石の広場
        Solid("Ground", PrimitiveType.Cube, Axis(ground - 0.5f), new Vector3(900f, 1f, 900f),
            Darken(GroundTint), ProcTex.Grass(), 150f, 150f, true, false);
        Disc("DirtRing", Axis(ground + 0.02f), fenceRadius + 6f, 0.04f, Darken(DirtTint), ProcTex.Dirt(), (fenceRadius + 6f) * 2f / tile);
        Disc("Plaza", Axis(ground + 0.04f), 9f, 0.04f, PlazaTint, flagTex, 18f / tile);

        // 塔の胴:台座と、4 段の石のレンガ。段の境目に、明るい石の帯
        Disc("Plinth", Axis(ground + 0.3f), 3.9f, 0.6f, TrimColor, stoneTex, 3.9f * 2f * Mathf.PI / tile, 0.6f / tile, true);
        float y = ground;
        for (int i = 0; i < SegmentHeights.Length; i++)
        {
            float h = SegmentHeights[i];
            float r = radii[i];
            Disc("Shaft" + (i + 1), Axis(y + h * 0.5f), r, h, StoneColor, stoneTex, 2f * Mathf.PI * r / tile, h / tile, true);
            y += h;
            if (i < SegmentHeights.Length - 1)
            {
                float beltDiameter = radii[i] + radii[i + 1] + 0.3f;
                trim.Add(cyl8, Axis(y - 0.12f), Quaternion.identity, new Vector3(beltDiameter, 0.26f, beltDiameter));
            }
        }

        // 頂上の足場:敷石(当たり判定つき)と、下側の帯。半径 PlatformRadius の外へは、はみ出させない(視界をさえぎらないため)
        Disc("Platform", Axis(top - 0.125f), PlatformRadius, 0.25f, PlazaTint, flagTex, PlatformRadius * 2f / tile, 0.25f / tile, true);
        Disc("PlatformTrim", Axis(top - 0.33f), PlatformRadius, 0.16f, TrimColor, stoneTex, PlatformRadius * 2f * Mathf.PI / tile, 0.16f / tile, true);
        AddCollider("PlatformCollider", Axis(top - 0.125f), new Vector3(PlatformRadius * 2f, 0.25f, PlatformRadius * 2f));

        // 足元の模様:金の輪と、赤い円(絨毯)
        Disc("CarpetGold", Axis(top + 0.006f), platformRadius * 0.8875f, 0.012f, GoldColor, null, 1f);
        Disc("CarpetRed", Axis(top + 0.012f), platformRadius * 0.7875f, 0.012f, RedColor, null, 1f);

        // 頂上の足場のふちには、視界をさえぎるものを、何も立てない。
        // (手すり、旗ざお、かがり火は、目の前で、黒っぽい柵のように見えて、敵を見づらくしたので、なくした)
        // ふちが分かるように、床に、金の細い線を引くだけにする(高さは 6mm。視線には、かからない)
        Disc("RimGold", Axis(top + 0.003f), PlatformRadius - 0.02f, 0.006f, GoldColor, null, 1f);
        Disc("RimInner", Axis(top + 0.006f), PlatformRadius - 0.09f, 0.006f, PlazaTint, flagTex, (PlatformRadius - 0.09f) * 2f / tile);

        // 旗ざお 4 本は、遠くの外柵の輪の上に立てる(高い柱の先で、赤い旗が風で揺れる)。柱は細く、目の前ではないので、邪魔にならない。
        // 敵の通り道(15 度おき)の、間に置く
        foreach (float a in new[] { 52.5f, 142.5f, 232.5f, 322.5f })
        {
            Vector3 p = Axis(ground) + Direction(a) * fenceRadius;
            wood.Add(cyl6, p, Quaternion.identity, new Vector3(0.12f, 4.6f, 0.12f));
            BuildFlag(p + Vector3.up * 4.4f, Quaternion.LookRotation(Direction(a + 90f)), 2f);
        }

        // 塔の飾り:正面の門と石の枠、鉄の帯、両脇のたいまつ
        Vector3 front = Direction(0f);
        Vector3 rightSide = Direction(90f);
        Quaternion frontRot = Quaternion.LookRotation(front);
        float gateR = radii[0] + 0.02f;
        var gate = Solid("Gate", PrimitiveType.Cube, Axis(ground + 1.25f) + front * gateR, new Vector3(1.9f, 2.5f, 0.2f),
            GateWoodColor, plankTex, 1.9f / tile, 2.5f / tile, false, false);
        gate.transform.rotation = frontRot;
        trim.Add(cube, Axis(ground + 1.4f) + front * (gateR + 0.05f) - rightSide * 1.15f, frontRot, new Vector3(0.4f, 2.8f, 0.5f));
        trim.Add(cube, Axis(ground + 1.4f) + front * (gateR + 0.05f) + rightSide * 1.15f, frontRot, new Vector3(0.4f, 2.8f, 0.5f));
        trim.Add(cube, Axis(ground + 2.95f) + front * (gateR + 0.05f), frontRot, new Vector3(2.7f, 0.4f, 0.5f));
        foreach (float gy in new[] { 0.5f, 1.25f, 2.0f })
        {
            iron.Add(cube, Axis(ground + gy) + front * (gateR + 0.14f), frontRot, new Vector3(1.9f, 0.12f, 0.04f));
        }
        foreach (float side in new[] { -1.9f, 1.9f })
        {
            Vector3 p = Axis(ground) + front * (gateR + 0.3f) + rightSide * side;
            wood.Add(cyl8, p, Quaternion.identity, new Vector3(0.1f, 1.6f, 0.1f));
            iron.Add(cyl8, p + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.3f, 0.07f, 0.3f));
            FlameFlicker.Create(transform, p + Vector3.up * 1.68f, 1f, 0f, 3);
        }

        // 塔の胴の飾り:2 段目に、赤いバナー 4 枚。3 段目に、矢を撃つ細い窓
        foreach (float a in new[] { 45f, 135f, 225f, 315f })
        {
            BuildBanner(Axis(ground + SegmentHeights[0] + SegmentHeights[1] - 0.3f) + Direction(a) * (radii[1] + 0.06f),
                Quaternion.LookRotation(Direction(a)), wood, cube);
        }
        float slitY = ground + SegmentHeights[0] + SegmentHeights[1] + SegmentHeights[2] * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f + 22.5f;
            iron.Add(cube, Axis(slitY) + Direction(a) * (radii[2] + 0.01f), Quaternion.LookRotation(Direction(a)), new Vector3(0.12f, 0.7f, 0.06f));
        }

        // 外柵(木の柵の輪)。視界をさえぎらないように、低くて、すき間のある柵にする(高さは fenceHeight。0 なら作らない)。
        // 頂上の目の高さ(13.6m)から見ると、柵のすぐ外側に立つ敵の足元への視線は、柵の位置で、地面から約 0.75m の高さを通る。
        // 柵(杭の先まで)をそれより低くしておけば、いちばん手前の敵も、足元まで全身が見える。地上の敵は、この柵の外側まで来て、柵と塔を攻撃する
        if (fenceHeight > 0.05f)
        {
            float tip = Mathf.Min(0.18f, fenceHeight * 0.4f);
            float circumference = 2f * Mathf.PI * fenceRadius;
            int postCount = Mathf.CeilToInt(circumference / 1.1f);
            for (int i = 0; i < postCount; i++)
            {
                float a = i * 360f / postCount;
                Vector3 p = Axis(ground) + Direction(a) * fenceRadius;
                float postHeight = fenceHeight * (0.85f + 0.15f * Mathf.Abs(Mathf.Sin(i * 12.9898f))) - tip;
                posts.Add(cyl6, p, Quaternion.identity, new Vector3(0.16f, postHeight, 0.16f));
                posts.Add(cone6, p + Vector3.up * postHeight, Quaternion.identity, new Vector3(0.16f, tip, 0.16f));
            }
            int beamCount = Mathf.CeilToInt(circumference / 2f);
            for (int i = 0; i < beamCount; i++)
            {
                float a0 = i * 360f / beamCount;
                float a1 = (i + 1) * 360f / beamCount;
                Vector3 p = Axis(ground) + Direction(a0) * (fenceRadius - 0.09f);
                Vector3 q = Axis(ground) + Direction(a1) * (fenceRadius - 0.09f);
                Vector3 along = q - p;
                Quaternion rot = Quaternion.LookRotation(along.normalized);
                beams.Add(cube, (p + q) * 0.5f + Vector3.up * (fenceHeight * 0.55f), rot, new Vector3(0.06f, 0.07f, along.magnitude + 0.05f));
            }
        }

        // 柵の内側に、低いかがり火(8 か所)。柱を低くして、柵のすぐ外の敵への視線をさえぎらないようにする(視線は、この位置で、地面から約 2m 以上の高さを通る)
        float fireRadius = fenceRadius - 1.6f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = Axis(ground) + Direction(i * 45f + 22.5f) * fireRadius;
            wood.Add(cyl8, p, Quaternion.identity, new Vector3(0.14f, 0.9f, 0.14f));
            iron.Add(cyl8, p + Vector3.up * 0.9f, Quaternion.identity, new Vector3(0.34f, 0.08f, 0.34f));
            FlameFlicker.Create(transform, p + Vector3.up * 1.0f, 0.9f, 0f, 2);
        }

        AddBatch("Trim", trim, TrimColor, true, false);
        AddBatch("Iron", iron, IronColor, false, false);
        AddBatch("Wood", wood, WoodColor, false, false);
        AddBatch("FencePosts", posts, WoodColor, false, CastShadows);
        AddBatch("FenceBeams", beams, DarkWoodColor, false, false);

        // 周りの景色(山、森、岩、茂み、草、水晶)。全方向へ、敵の通り道(24 本)を避けて置く
        Scenery.BuildRing(transform, ground);
    }

    // ----------------------------------------------------------------- 飾りを作る道具

    /// <summary>標準の立体を 1 つ、色と模様つきで作る(ワールド座標で置く)。collider が false なら、当たり判定を外す。</summary>
    GameObject Solid(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Color color,
                     Texture2D texture, float tilingU, float tilingV, bool collider, bool flashes)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        if (!collider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        var rend = go.GetComponent<Renderer>();
        if (texture != null) Paint.ApplyTextured(rend, color, texture, tilingU, tilingV);
        else Paint.Apply(rend, color);
        rend.shadowCastingMode = ShadowCastingMode.Off;

        if (flashes)
        {
            stone.Add(rend);
            stoneColors.Add(color);
        }
        return go;
    }

    /// <summary>
    /// 丸い板(円柱)を、中心 position、半径 radius、厚さ thickness で作る。当たり判定はなし。
    /// tilingU は、模様を、円周に何回くり返すか(上面では、直径の方向にも同じ回数)。tilingV は、側面の縦方向。
    /// flashes が true なら、ダメージを受けたとき赤く光る対象になる。
    /// </summary>
    GameObject Disc(string objectName, Vector3 position, float radius, float thickness, Color color,
                    Texture2D texture, float tilingU, float tilingV = -1f, bool flashes = false)
    {
        if (tilingV < 0f) tilingV = tilingU;
        // Unity の標準の円柱は、高さ 2m・直径 1m。scale.y が「厚さの半分」になる
        return Solid(objectName, PrimitiveType.Cylinder, position, new Vector3(radius * 2f, thickness * 0.5f, radius * 2f),
            color, texture, tilingU, tilingV, false, flashes);
    }

    /// <summary>見えない当たり判定だけの箱(頂上の床など)。</summary>
    void AddCollider(string objectName, Vector3 position, Vector3 size)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size;
    }

    void AddBatch(string batchName, MeshKit.Batch batch, Color color, bool flashesOnDamage, bool castShadows)
    {
        GameObject go = MeshKit.Spawn(batchName, batch, color, transform, castShadows);
        if (go == null) return;

        if (flashesOnDamage)
        {
            stone.Add(go.GetComponent<Renderer>());
            stoneColors.Add(color);
        }
    }

    Transform Attach(Transform parent, PrimitiveType type, string partName, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var go = Paint.Prim(type, partName, color);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        return go.transform;
    }

    /// <summary>揺れる旗。position は、旗ざおの先(旗の付け根)。rotation の右方向へ、旗がなびく。</summary>
    void BuildFlag(Vector3 position, Quaternion rotation, float scale = 1f)
    {
        var pivot = new GameObject("Flag").transform;
        pivot.SetParent(transform, false);
        pivot.SetPositionAndRotation(position, rotation);
        pivot.localScale = Vector3.one * scale;
        Attach(pivot, PrimitiveType.Cube, "Cloth", new Vector3(0.32f, 0f, 0f), new Vector3(0.64f, 0.34f, 0.02f), RedColor);
        Attach(pivot, PrimitiveType.Cube, "Stripe", new Vector3(0.32f, 0f, 0f), new Vector3(0.64f, 0.07f, 0.024f), GoldColor);

        var sway = pivot.gameObject.AddComponent<Sway>();
        sway.axis = Vector3.up;
        sway.degrees = 16f;
        sway.speed = 2.4f;
    }

    /// <summary>バナー(赤い垂れ幕)。position は、上の棒の中心。rotation の前が、外向き。</summary>
    void BuildBanner(Vector3 position, Quaternion rotation, MeshKit.Batch wood, Mesh cube)
    {
        wood.Add(cube, position, rotation, new Vector3(1.1f, 0.06f, 0.06f));

        var pivot = new GameObject("Banner").transform;
        pivot.SetParent(transform, false);
        pivot.SetPositionAndRotation(position, rotation);

        Attach(pivot, PrimitiveType.Cube, "Cloth", new Vector3(0f, -1.4f, 0f), new Vector3(0.9f, 2.8f, 0.04f), RedColor);
        Attach(pivot, PrimitiveType.Cube, "TrimLeft", new Vector3(-0.42f, -1.4f, 0.01f), new Vector3(0.06f, 2.8f, 0.05f), GoldColor);
        Attach(pivot, PrimitiveType.Cube, "TrimRight", new Vector3(0.42f, -1.4f, 0.01f), new Vector3(0.06f, 2.8f, 0.05f), GoldColor);
        var emblem = Attach(pivot, PrimitiveType.Cube, "Emblem", new Vector3(0f, -1.2f, 0.03f), new Vector3(0.4f, 0.4f, 0.05f), GoldColor);
        emblem.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var sway = pivot.gameObject.AddComponent<Sway>();
        sway.axis = Vector3.right;
        sway.degrees = 2.5f;
        sway.speed = 1.1f;
    }

    // ----------------------------------------------------------------- ダメージ

    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;

        Health = Mathf.Max(0f, Health - damage);
        Damaged?.Invoke(Health);
        StartCoroutine(FlashRed());
        GameAudio.PlayFortHit(Axis(TopY - 2f));
        if (GunSystem.Instance != null) GunSystem.Instance.PulseHaptics(0.8f, 0.12f);

        if (IsDestroyed) Broken?.Invoke();
    }

    /// <summary>耐久値を回復する(ウェーブをクリアしたときなど)。</summary>
    public void Repair(float amount)
    {
        if (IsDestroyed) return;
        Health = Mathf.Min(maxHealth, Health + amount);
        Damaged?.Invoke(Health);
    }

    IEnumerator FlashRed()
    {
        for (int i = 0; i < stone.Count; i++)
        {
            if (stone[i] != null) stone[i].material.color = Color.Lerp(stoneColors[i], DamageColor, 0.6f);
        }
        yield return new WaitForSeconds(0.12f);
        for (int i = 0; i < stone.Count; i++)
        {
            if (stone[i] != null) stone[i].material.color = stoneColors[i];
        }
    }
}

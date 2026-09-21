using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// プレイヤーが立つ砦(とりで)。標準の立体(箱と円柱)と、プログラムで作る模様(石のレンガなど)で、起動時に作る。
///   ・プレイヤーは砦の上(城壁の通路)に立つ
///   ・敵は、砦の正面の壁に向かって進み、到達すると攻撃して砦の耐久値を減らす
///   ・耐久値が 0 になると、ゲームオーバー
/// 位置の基準は、PlayerView の足元の中心と正面の向き。
///
/// 見た目(幻想的な中世ファンタジー):石のレンガ模様、四隅の塔と円すいの屋根、はためく旗とバナー、
/// ゆらめくたいまつ、門、木の柵、赤い絨毯、樽と木箱。同じ色の飾りは 1 つの立体にまとめて、描画の回数を減らしている。
/// 周りの景色(山・森・岩・草など)は Scenery が作る。
/// </summary>
public class Fort : MonoBehaviour, IStronghold
{
    public static Fort Instance { get; private set; }

    /// <summary>足元の中心から、正面の壁までの距離(m)。</summary>
    public const float FaceForward = 1f;
    /// <summary>足元の中心から、外柵(木の柵)までの距離(m)。地上の敵はこの柵の前で止まる。</summary>
    public const float PalisadeForward = FaceForward + 3.5f;
    /// <summary>砦の幅の半分(m)。</summary>
    public const float HalfWidth = 4.8f;

    static readonly Color GroundTint = new Color(0.9f, 0.95f, 1f);
    static readonly Color RoadTint = new Color(0.95f, 0.9f, 0.9f);
    static readonly Color StoneColor = new Color(0.74f, 0.73f, 0.78f);
    static readonly Color DarkStoneColor = new Color(0.55f, 0.55f, 0.62f);
    static readonly Color TrimColor = new Color(0.88f, 0.84f, 0.78f);
    static readonly Color FloorColor = new Color(0.82f, 0.78f, 0.74f);
    static readonly Color GateWoodColor = new Color(0.66f, 0.46f, 0.30f);
    static readonly Color WoodColor = new Color(0.36f, 0.24f, 0.14f);
    static readonly Color DarkWoodColor = new Color(0.25f, 0.15f, 0.08f);
    static readonly Color RopeColor = new Color(0.55f, 0.42f, 0.25f);
    static readonly Color IronColor = new Color(0.14f, 0.15f, 0.18f);
    static readonly Color GoldColor = new Color(1f, 0.78f, 0.3f);
    static readonly Color RedColor = new Color(0.62f, 0.09f, 0.12f);
    static readonly Color RoofColor = new Color(0.22f, 0.28f, 0.48f);
    static readonly Color DamageColor = new Color(1f, 0.25f, 0.2f);

    float maxHealth = 100f;
    float height = 6f;
    Vector3 right;
    Quaternion facing;
    readonly List<Renderer> stone = new List<Renderer>();
    readonly List<Color> stoneColors = new List<Color>();

    public string Label => "FORT";
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => Health <= 0f;
    /// <summary>地面の高さ。</summary>
    public float GroundY => PlayerView.FloorY - height;
    /// <summary>砦の上面(プレイヤーが立つ床)の高さ。</summary>
    public float TopY => PlayerView.FloorY;

    /// <summary>たいまつに、暖色の光(点光源)を付けるか。重いときは false にする。Build の前に設定する。</summary>
    public bool TorchLights { get; set; } = true;
    /// <summary>砦と柵が影を落とすか(太陽の影が切れているときは、意味がない)。Build の前に設定する。</summary>
    public bool CastShadows { get; set; } = true;
    /// <summary>地面の明るさ(1 = 元のまま)。小さいほど暗くなって、明るい色の敵が目立つ。Build の前に設定する。</summary>
    public float GroundBrightness { get; set; } = 1f;
    /// <summary>外柵の高さ(m。杭の先まで)。低いほど、柵のすぐ外の敵が、足元まで見える。0 なら柵を作らない。Build の前に設定する。</summary>
    public float FenceHeight { get; set; } = 0.8f;

    Color Darken(Color c)
    {
        return new Color(c.r * GroundBrightness, c.g * GroundBrightness, c.b * GroundBrightness, 1f);
    }

    /// <summary>ダメージを受けたときに通知される(残りの耐久値)。</summary>
    public event Action<float> Damaged;
    /// <summary>耐久値が 0 になったときに通知される。</summary>
    public event Action Broken;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Stronghold.Current == (IStronghold)this) Stronghold.Current = null;
    }

    /// <summary>
    /// 足元の中心から見た位置を、ワールド座標にする。
    /// rightMeters は右へ、forwardMeters は正面へ、y は高さ(ワールドの高さ)。
    /// </summary>
    public Vector3 Point(float rightMeters, float y, float forwardMeters)
    {
        Vector3 flat = new Vector3(PlayerView.Center.x, 0f, PlayerView.Center.z)
                       + right * rightMeters
                       + PlayerView.FlatForward * forwardMeters;
        return new Vector3(flat.x, y, flat.z);
    }

    /// <summary>砦を作る。PlayerView.Ensure() のあとに呼ぶ。</summary>
    public void Build(float maxHealthValue, float heightMeters)
    {
        Instance = this;
        Stronghold.Current = this;
        maxHealth = maxHealthValue;
        height = heightMeters;
        Health = maxHealth;

        // ワールド座標で置いたものを、そのまま子にするので、砦の親は原点に置く
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        right = Vector3.Cross(Vector3.up, PlayerView.FlatForward).normalized;
        facing = Quaternion.LookRotation(PlayerView.FlatForward);

        float top = TopY;
        float ground = GroundY;
        float w = HalfWidth;
        const float tile = ProcTex.TileMeters;

        Texture2D stoneTex = ProcTex.Stone();
        Texture2D flagTex = ProcTex.Flagstone();
        Texture2D plankTex = ProcTex.Planks();

        // 同じ色の飾りを、1 つの立体にまとめるための入れ物
        var trim = new MeshKit.Batch();       // 明るい石の飾り(帯、縁、門の枠)
        var darkStone = new MeshKit.Batch();  // 暗い石(胸壁の出っ張り、塔の縁)
        var iron = new MeshKit.Batch();       // 鉄(矢を撃つ細い窓、門の帯、たいまつの受け)
        var gold = new MeshKit.Batch();       // 金(屋根の先、絨毯の縁、盾の縁)
        var red = new MeshKit.Batch();        // 赤(絨毯、盾)
        var wood = new MeshKit.Batch();       // 木(たいまつの柱、樽、木箱、旗ざお)
        var roofs = new MeshKit.Batch();      // 塔の円すい屋根
        var posts = new MeshKit.Batch();      // 柵の杭
        var ropes = new MeshKit.Batch();      // 杭を結ぶ縄
        Mesh cube = MeshKit.Cube();
        Mesh cyl8 = MeshKit.Cylinder(8);
        Mesh cyl6 = MeshKit.Cylinder(6);
        Mesh cone8 = MeshKit.Cone(8);
        Mesh cone6 = MeshKit.Cone(6);

        // 地面(草の模様)と、正面へ伸びる土の道(荷車のあとつき)
        AddBlock("Ground", PrimitiveType.Cube, Point(0f, ground - 0.5f, 0f), new Vector3(900f, 1f, 900f),
            Darken(GroundTint), false, true, ProcTex.Grass(), 150f, 150f);   // 当たり判定あり:銃やバズーカが地面に当たる(バズーカは地面で爆発する)
        AddBlock("Road", PrimitiveType.Cube, Point(0f, ground + 0.02f, FaceForward + 110f), new Vector3(7f, 0.04f, 220f),
            Darken(RoadTint), false, false, ProcTex.Dirt(), 1f, 17f);

        // 砦の本体(石のレンガ)と、プレイヤーが立つ床(敷石)。正面の壁の面は、足元から FaceForward の位置
        float bodyDepth = 5f;
        float bodyCenterZ = FaceForward - bodyDepth * 0.5f;
        AddBlock("KeepBody", PrimitiveType.Cube, Point(0f, top - height * 0.5f, bodyCenterZ), new Vector3(w * 2f, height, bodyDepth),
            StoneColor, true, true, stoneTex, w * 2f / tile, height / tile, false);
        AddBlock("Floor", PrimitiveType.Cube, Point(0f, top + 0.03f, bodyCenterZ), new Vector3(w * 2f - 0.2f, 0.06f, bodyDepth - 0.2f),
            FloorColor, true, true, flagTex, (w * 2f - 0.2f) / tile, (bodyDepth - 0.2f) / tile);

        // 赤い絨毯(金の縁つき)。プレイヤーの足元の通路
        Box(red, cube, 0f, top + 0.075f, bodyCenterZ, 1.7f, 0.03f, bodyDepth - 0.6f);
        Box(gold, cube, -0.85f, top + 0.08f, bodyCenterZ, 0.07f, 0.035f, bodyDepth - 0.6f);
        Box(gold, cube, 0.85f, top + 0.08f, bodyCenterZ, 0.07f, 0.035f, bodyDepth - 0.6f);

        // 胸壁。正面は、真下の敵が見えて撃てるように、低い壁にしてある(見た目だけで、弾は通る)。
        float frontZ = FaceForward - 0.15f;
        float backZ = FaceForward - bodyDepth + 0.2f;
        AddBlock("ParapetFront", PrimitiveType.Cube, Point(0f, top + 0.2f, frontZ), new Vector3(w * 2f, 0.4f, 0.3f),
            StoneColor, true, false, stoneTex, w * 2f / tile, 0.4f / tile, false);
        AddBlock("ParapetBack", PrimitiveType.Cube, Point(0f, top + 0.5f, backZ), new Vector3(w * 2f, 1f, 0.4f),
            StoneColor, true, false, stoneTex, w * 2f / tile, 1f / tile, false);
        AddBlock("ParapetLeft", PrimitiveType.Cube, Point(-(w - 0.2f), top + 0.5f, bodyCenterZ), new Vector3(0.4f, 1f, bodyDepth),
            StoneColor, true, false, stoneTex, bodyDepth / tile, 1f / tile, false);
        AddBlock("ParapetRight", PrimitiveType.Cube, Point(w - 0.2f, top + 0.5f, bodyCenterZ), new Vector3(0.4f, 1f, bodyDepth),
            StoneColor, true, false, stoneTex, bodyDepth / tile, 1f / tile, false);

        // 正面の低い壁の上の縁(明るい石)と、出っ張り
        Box(trim, cube, 0f, top + 0.44f, frontZ, w * 2f + 0.2f, 0.08f, 0.42f);
        foreach (float mx in new[] { -4.0f, 4.0f })
        {
            Box(darkStone, cube, mx, top + 0.6f, frontZ, 0.7f, 0.4f, 0.3f);
        }
        for (float x = -4.2f; x <= 4.21f; x += 1.4f)
        {
            Box(darkStone, cube, x, top + 1.25f, backZ, 0.7f, 0.5f, 0.4f);
        }
        for (float z = backZ + 0.8f; z <= frontZ - 0.6f; z += 1.4f)
        {
            Box(darkStone, cube, -(w - 0.2f), top + 1.25f, z, 0.4f, 0.5f, 0.7f);
            Box(darkStone, cube, w - 0.2f, top + 1.25f, z, 0.4f, 0.5f, 0.7f);
        }

        // 正面の壁の飾り:根元の台座、腰の帯、矢を撃つ細い窓
        Box(trim, cube, 0f, ground + 0.45f, FaceForward + 0.12f, w * 2f + 0.3f, 0.9f, 0.4f);
        Box(trim, cube, 0f, top - 1.0f, FaceForward + 0.1f, w * 2f + 0.1f, 0.28f, 0.3f);
        foreach (float sx in new[] { -1.5f, 1.5f })
        {
            Box(iron, cube, sx, top - 3.4f, FaceForward + 0.02f, 0.12f, 0.7f, 0.06f);
        }
        foreach (float sx in new[] { -4.1f, 4.1f })
        {
            Box(iron, cube, sx, top - 2.6f, FaceForward + 0.02f, 0.12f, 0.7f, 0.06f);
        }

        // 四隅の塔(石のレンガ)と、円すいの屋根、旗
        float towerHeight = height + 2f;
        foreach (float tx in new[] { -w, w })
        {
            foreach (float tz in new[] { FaceForward, backZ - 0.2f })
            {
                AddBlock("Tower", PrimitiveType.Cylinder, Point(tx, ground + towerHeight * 0.5f, tz),
                    new Vector3(1.9f, towerHeight * 0.5f, 1.9f), StoneColor, true, false, stoneTex, 5f, towerHeight / tile, false);
                Post(darkStone, cyl8, tx, top + 2.0f, tz, 2.2f, 0.08f);
                Post(trim, cyl8, tx, top + 1.5f, tz, 2.15f, 0.25f);

                roofs.Add(cone8, Point(tx, top + 2.08f, tz), facing, new Vector3(2.5f, 2.8f, 2.5f));
                Post(gold, cyl6, tx, top + 4.85f, tz, 0.16f, 0.3f);
                Post(gold, cyl6, tx, top + 5.1f, tz, 0.04f, 1.0f);
                BuildFlag(Point(tx, top + 5.85f, tz));
            }
        }

        // 正面の門(板と鉄の帯)と、石の枠
        AddBlock("Gate", PrimitiveType.Cube, Point(0f, ground + 1.3f, FaceForward + 0.05f), new Vector3(2.2f, 2.6f, 0.2f),
            GateWoodColor, false, false, plankTex, 2.2f / tile, 2.6f / tile);
        Box(trim, cube, -1.3f, ground + 1.45f, FaceForward + 0.1f, 0.4f, 2.9f, 0.5f);
        Box(trim, cube, 1.3f, ground + 1.45f, FaceForward + 0.1f, 0.4f, 2.9f, 0.5f);
        Box(trim, cube, 0f, ground + 3.12f, FaceForward + 0.1f, 3.0f, 0.45f, 0.5f);
        foreach (float gy in new[] { 0.5f, 1.3f, 2.1f })
        {
            Box(iron, cube, 0f, ground + gy, FaceForward + 0.17f, 2.2f, 0.12f, 0.04f);
        }

        // バナー(赤い垂れ幕。風でゆれる)
        BuildBanner(Point(-3.0f, top - 0.25f, FaceForward + 0.32f), wood, cube);
        BuildBanner(Point(3.0f, top - 0.25f, FaceForward + 0.32f), wood, cube);

        // たいまつ:通路の左右(床と銃を照らす)と、正面の壁(敵が来る手前の地面を照らす)
        BuildTorchStand(Point(-2.4f, top + 0.06f, 0.3f), iron, wood, cyl8);
        BuildTorchStand(Point(2.4f, top + 0.06f, 0.3f), iron, wood, cyl8);
        BuildWallSconce(Point(-1.5f, top - 1.5f, FaceForward), iron, cube, cyl8);
        BuildWallSconce(Point(1.5f, top - 1.5f, FaceForward), iron, cube, cyl8);

        // 通路の飾り:後ろの隅に、樽と木箱。側の壁に、赤と金の盾
        Post(wood, cyl8, -3.8f, top + 0.06f, backZ + 0.8f, 0.62f, 0.84f);
        Post(iron, cyl8, -3.8f, top + 0.24f, backZ + 0.8f, 0.65f, 0.04f);
        Post(iron, cyl8, -3.8f, top + 0.7f, backZ + 0.8f, 0.65f, 0.04f);
        Post(wood, cyl8, -3.1f, top + 0.06f, backZ + 0.9f, 0.62f, 0.84f);
        Box(wood, cube, -3.9f, top + 0.41f, backZ + 1.7f, 0.7f, 0.7f, 0.7f, new Vector3(0f, 20f, 0f));
        Post(wood, cyl8, 3.7f, top + 0.06f, backZ + 0.8f, 0.62f, 0.84f);
        Post(iron, cyl8, 3.7f, top + 0.24f, backZ + 0.8f, 0.65f, 0.04f);
        Post(iron, cyl8, 3.7f, top + 0.7f, backZ + 0.8f, 0.65f, 0.04f);
        Box(wood, cube, 3.15f, top + 0.38f, backZ + 0.9f, 0.65f, 0.65f, 0.65f, new Vector3(0f, -15f, 0f));
        Box(wood, cube, 3.8f, top + 0.33f, backZ + 1.7f, 0.55f, 0.55f, 0.55f, new Vector3(0f, 35f, 0f));
        foreach (float shieldZ in new[] { bodyCenterZ - 1.2f, bodyCenterZ + 1.2f })
        {
            AddShield(gold, red, cube, -(w - 0.42f), top + 0.55f, shieldZ);
            AddShield(gold, red, cube, w - 0.42f, top + 0.55f, shieldZ);
        }

        // 外柵(木の柵)。視界と弾をさえぎらないように、低くて、すき間のある柵にする(高さは FenceHeight。0 なら作らない)。
        // 当たり判定はなし(弾やバズーカは、柵を通り抜けて、後ろの敵に当たる)。地上の敵は、この柵の前まで来て、柵と砦を攻撃する
        float fenceHalf = w + 1.5f;
        if (FenceHeight > 0.05f)
        {
            float tip = Mathf.Min(0.18f, FenceHeight * 0.4f);
            for (float px = -fenceHalf; px <= fenceHalf + 0.01f; px += 0.9f)
            {
                float postHeight = FenceHeight * (0.85f + 0.15f * Mathf.Abs(Mathf.Sin(px * 12.9898f))) - tip;
                Post(posts, cyl6, px, ground, PalisadeForward, 0.2f, postHeight);
                posts.Add(cone6, Point(px, ground + postHeight, PalisadeForward), facing, new Vector3(0.2f, tip, 0.2f));
            }
            Box(ropes, cube, 0f, ground + FenceHeight * 0.55f, PalisadeForward - 0.1f, fenceHalf * 2f + 0.4f, 0.07f, 0.07f);
        }

        // 柵の内側に、低いかがり火(光る炎だけ。光源は付けない)。柵のすぐ外の敵への視線をさえぎらないように、低くして、少し内側に置く
        foreach (float fx in new[] { -2.6f, 2.6f })
        {
            BuildWatchFire(Point(fx, ground, PalisadeForward - 1.3f), wood, iron, cyl8);
        }

        // まとめた飾りを、それぞれ 1 つの立体にして出す
        AddBatch("Trim", trim, TrimColor, true, false);
        AddBatch("DarkStone", darkStone, DarkStoneColor, true, false);
        AddBatch("Iron", iron, IronColor, false, false);
        AddBatch("Gold", gold, GoldColor, false, false);
        AddBatch("Red", red, RedColor, false, false);
        AddBatch("Wood", wood, WoodColor, false, false);
        AddBatch("Roofs", roofs, RoofColor, false, false);
        AddBatch("FencePosts", posts, WoodColor, true, true);
        AddBatch("FenceRopes", ropes, RopeColor, false, false);

        // 周りの景色(山、遠くの城、森、岩、茂み、草、水晶、道の縁石)
        Scenery.Build(this, transform);
    }

    // ----------------------------------------------------------------- 飾りを作る道具

    /// <summary>箱を、まとめる入れ物に足す。位置は、足元の中心から見た(右, 高さ, 正面)。</summary>
    void Box(MeshKit.Batch batch, Mesh cube, float x, float y, float f, float sx, float sy, float sz, Vector3 euler = default)
    {
        batch.Add(cube, Point(x, y, f), facing * Quaternion.Euler(euler), new Vector3(sx, sy, sz));
    }

    /// <summary>縦の円柱を、まとめる入れ物に足す。yBase は底の高さ。</summary>
    void Post(MeshKit.Batch batch, Mesh cylinder, float x, float yBase, float f, float diameter, float cylinderHeight)
    {
        batch.Add(cylinder, Point(x, yBase, f), facing, new Vector3(diameter, cylinderHeight, diameter));
    }

    void AddBatch(string batchName, MeshKit.Batch batch, Color color, bool flashesOnDamage, bool castShadows)
    {
        GameObject go = MeshKit.Spawn(batchName, batch, color, transform, castShadows && CastShadows);
        if (go == null) return;

        if (flashesOnDamage)
        {
            stone.Add(go.GetComponent<Renderer>());
            stoneColors.Add(color);
        }
    }

    Transform Attach(Transform parent, PrimitiveType type, string partName, Vector3 localPosition, Vector3 localScale,
                     Color color, Vector3 localEuler = default)
    {
        var go = Paint.Prim(type, partName, color);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        return go.transform;
    }

    /// <summary>揺れる旗を作る。position は、旗ざおの先(旗の付け根)のワールド座標。</summary>
    void BuildFlag(Vector3 position)
    {
        var pivot = new GameObject("Flag").transform;
        pivot.SetParent(transform, false);
        pivot.SetPositionAndRotation(position, facing);
        Attach(pivot, PrimitiveType.Cube, "Cloth", new Vector3(0.32f, 0f, 0f), new Vector3(0.64f, 0.34f, 0.02f), RedColor);
        Attach(pivot, PrimitiveType.Cube, "Stripe", new Vector3(0.32f, 0f, 0f), new Vector3(0.64f, 0.07f, 0.024f), GoldColor);

        var sway = pivot.gameObject.AddComponent<Sway>();
        sway.axis = Vector3.up;
        sway.degrees = 16f;
        sway.speed = 2.4f;
    }

    /// <summary>バナー(赤い垂れ幕、金の縁と紋章)。position は、上の棒の中心。風でゆっくり揺れる。</summary>
    void BuildBanner(Vector3 position, MeshKit.Batch wood, Mesh cube)
    {
        // 上の棒は、まとめる入れ物のほうへ
        wood.Add(cube, position, facing, new Vector3(1.1f, 0.06f, 0.06f));

        var pivot = new GameObject("Banner").transform;
        pivot.SetParent(transform, false);
        pivot.SetPositionAndRotation(position, facing);

        Attach(pivot, PrimitiveType.Cube, "Cloth", new Vector3(0f, -1.4f, 0f), new Vector3(0.9f, 2.8f, 0.04f), RedColor);
        Attach(pivot, PrimitiveType.Cube, "TrimLeft", new Vector3(-0.42f, -1.4f, 0.01f), new Vector3(0.06f, 2.8f, 0.05f), GoldColor);
        Attach(pivot, PrimitiveType.Cube, "TrimRight", new Vector3(0.42f, -1.4f, 0.01f), new Vector3(0.06f, 2.8f, 0.05f), GoldColor);
        Attach(pivot, PrimitiveType.Cube, "Emblem", new Vector3(0f, -1.2f, 0.03f), new Vector3(0.4f, 0.4f, 0.05f), GoldColor, new Vector3(0f, 0f, 45f));

        var sway = pivot.gameObject.AddComponent<Sway>();
        sway.axis = Vector3.right;
        sway.degrees = 2.5f;
        sway.speed = 1.1f;
    }

    /// <summary>通路に立つ、たいまつ(石の台、木の柱、鉄の皿、炎、暖色の光)。</summary>
    void BuildTorchStand(Vector3 basePosition, MeshKit.Batch iron, MeshKit.Batch wood, Mesh cyl8)
    {
        Post(wood, cyl8, ToRight(basePosition), basePosition.y, ToForward(basePosition), 0.1f, 1.05f);
        Post(iron, cyl8, ToRight(basePosition), basePosition.y + 1.03f, ToForward(basePosition), 0.3f, 0.07f);
        Post(iron, cyl8, ToRight(basePosition), basePosition.y + 1.1f, ToForward(basePosition), 0.36f, 0.025f);
        FlameFlicker.Create(transform, basePosition + Vector3.up * 1.16f, 1f, TorchLights ? 2.4f : 0f, 4);
    }

    /// <summary>正面の壁に付いた、たいまつ(鉄の受け皿と炎)。壁の前の地面を、暖かく照らす。</summary>
    void BuildWallSconce(Vector3 wallPosition, MeshKit.Batch iron, Mesh cube, Mesh cyl8)
    {
        float x = ToRight(wallPosition);
        float f = ToForward(wallPosition);
        float y = wallPosition.y;
        Box(iron, cube, x, y, f + 0.03f, 0.16f, 0.5f, 0.05f);                 // 壁に付く板
        Box(iron, cube, x, y - 0.08f, f + 0.2f, 0.06f, 0.06f, 0.4f);          // 前に出る腕
        Post(iron, cyl8, x, y - 0.07f, f + 0.4f, 0.26f, 0.07f);               // 皿
        FlameFlicker.Create(transform, Point(x, y + 0.02f, f + 0.4f), 0.9f, TorchLights ? 2.4f : 0f, 3);
    }

    /// <summary>柵の内側の、低いかがり火(柱の上で、炎が燃える)。</summary>
    void BuildWatchFire(Vector3 basePosition, MeshKit.Batch wood, MeshKit.Batch iron, Mesh cyl8)
    {
        float x = ToRight(basePosition);
        float f = ToForward(basePosition);
        Post(wood, cyl8, x, basePosition.y, f, 0.14f, 1.0f);
        Post(iron, cyl8, x, basePosition.y + 1.0f, f, 0.34f, 0.08f);
        FlameFlicker.Create(transform, basePosition + Vector3.up * 1.1f, 0.9f, 0f, 2);
    }

    void AddShield(MeshKit.Batch gold, MeshKit.Batch red, Mesh cube, float x, float y, float f)
    {
        // ひし形の盾(金の縁に、赤い面)。壁の内側を向く
        Box(gold, cube, x, y, f, 0.04f, 0.56f, 0.56f, new Vector3(45f, 0f, 0f));
        Box(red, cube, x + (x < 0f ? 0.015f : -0.015f), y, f, 0.04f, 0.44f, 0.44f, new Vector3(45f, 0f, 0f));
    }

    // 足元の中心から見た「右」と「正面」の距離を、ワールド座標から求める
    float ToRight(Vector3 world)
    {
        Vector3 d = world - new Vector3(PlayerView.Center.x, world.y, PlayerView.Center.z);
        return Vector3.Dot(d, right);
    }

    float ToForward(Vector3 world)
    {
        Vector3 d = world - new Vector3(PlayerView.Center.x, world.y, PlayerView.Center.z);
        return Vector3.Dot(d, PlayerView.FlatForward);
    }

    GameObject AddBlock(string blockName, PrimitiveType type, Vector3 position, Vector3 scale, Color color,
                        bool flashesOnDamage, bool hasCollider = true, Texture2D texture = null,
                        float tilingU = 1f, float tilingV = 1f, bool castShadows = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = blockName;
        go.transform.SetPositionAndRotation(position, facing);
        go.transform.localScale = scale;
        go.transform.SetParent(transform, true);

        if (!hasCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        var rend = go.GetComponent<Renderer>();
        if (texture != null) Paint.ApplyTextured(rend, color, texture, tilingU, tilingV);
        else Paint.Apply(rend, color);
        rend.shadowCastingMode = castShadows && CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        if (flashesOnDamage)
        {
            stone.Add(rend);
            stoneColors.Add(color);
        }
        return go;
    }

    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;

        Health = Mathf.Max(0f, Health - damage);
        Damaged?.Invoke(Health);
        StartCoroutine(FlashRed());
        GameAudio.PlayFortHit(Point(0f, TopY - 1.5f, FaceForward + 0.5f));
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

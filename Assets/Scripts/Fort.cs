using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーが立つ砦(とりで)。標準の立体(箱と円柱)を組み合わせて、起動時に作る。
///   ・プレイヤーは砦の上(城壁の通路)に立つ
///   ・敵は、砦の正面の壁に向かって進み、到達すると攻撃して砦の耐久値を減らす
///   ・耐久値が 0 になると、ゲームオーバー
/// 位置の基準は、PlayerView の足元の中心と正面の向き。
/// </summary>
public class Fort : MonoBehaviour
{
    public static Fort Instance { get; private set; }

    /// <summary>足元の中心から、正面の壁までの距離(m)。</summary>
    public const float FaceForward = 1f;
    /// <summary>足元の中心から、外柵(木の柵)までの距離(m)。地上の敵はこの柵の前で止まる。</summary>
    public const float PalisadeForward = FaceForward + 3.5f;
    /// <summary>砦の幅の半分(m)。</summary>
    public const float HalfWidth = 4.8f;

    static readonly Color GrassColor = new Color(0.25f, 0.45f, 0.2f);
    static readonly Color RoadColor = new Color(0.5f, 0.4f, 0.25f);
    static readonly Color StoneColor = new Color(0.55f, 0.55f, 0.58f);
    static readonly Color DarkStoneColor = new Color(0.42f, 0.42f, 0.46f);
    static readonly Color FloorColor = new Color(0.68f, 0.66f, 0.62f);
    static readonly Color WoodColor = new Color(0.35f, 0.22f, 0.12f);
    static readonly Color DarkWoodColor = new Color(0.25f, 0.15f, 0.08f);
    static readonly Color DamageColor = new Color(1f, 0.25f, 0.2f);

    float maxHealth = 100f;
    float height = 6f;
    Vector3 right;
    Quaternion facing;
    readonly List<Renderer> stone = new List<Renderer>();
    readonly List<Color> stoneColors = new List<Color>();

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => Health <= 0f;
    /// <summary>地面の高さ。</summary>
    public float GroundY => PlayerView.FloorY - height;
    /// <summary>砦の上面(プレイヤーが立つ床)の高さ。</summary>
    public float TopY => PlayerView.FloorY;

    /// <summary>ダメージを受けたときに通知される(残りの耐久値)。</summary>
    public event Action<float> Damaged;
    /// <summary>耐久値が 0 になったときに通知される。</summary>
    public event Action Broken;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        maxHealth = maxHealthValue;
        height = heightMeters;
        Health = maxHealth;

        right = Vector3.Cross(Vector3.up, PlayerView.FlatForward).normalized;
        facing = Quaternion.LookRotation(PlayerView.FlatForward);

        float top = TopY;
        float ground = GroundY;
        float w = HalfWidth;

        // 地面と、正面へ伸びる道
        AddBlock("Ground", PrimitiveType.Cube, Point(0f, ground - 0.5f, 0f), new Vector3(500f, 1f, 500f), GrassColor, false);
        AddBlock("Road", PrimitiveType.Cube, Point(0f, ground + 0.02f, FaceForward + 80f), new Vector3(7f, 0.04f, 160f), RoadColor, false);

        // 砦の本体と、プレイヤーが立つ床(正面の壁の面は、足元から FaceForward の位置)
        float bodyDepth = 5f;
        float bodyCenterZ = FaceForward - bodyDepth * 0.5f;
        AddBlock("KeepBody", PrimitiveType.Cube, Point(0f, top - height * 0.5f, bodyCenterZ), new Vector3(w * 2f, height, bodyDepth), StoneColor, true);
        AddBlock("Floor", PrimitiveType.Cube, Point(0f, top + 0.03f, bodyCenterZ), new Vector3(w * 2f - 0.2f, 0.06f, bodyDepth - 0.2f), FloorColor, true);

        // 胸壁。正面は、真下の敵が見えて撃てるように、低い壁にしてある(見た目だけで、弾は通る)。
        float frontZ = FaceForward - 0.15f;
        float backZ = FaceForward - bodyDepth + 0.2f;
        AddBlock("ParapetFront", PrimitiveType.Cube, Point(0f, top + 0.2f, frontZ), new Vector3(w * 2f, 0.4f, 0.3f), StoneColor, true, false);
        AddBlock("ParapetBack", PrimitiveType.Cube, Point(0f, top + 0.5f, backZ), new Vector3(w * 2f, 1f, 0.4f), StoneColor, true, false);
        AddBlock("ParapetLeft", PrimitiveType.Cube, Point(-(w - 0.2f), top + 0.5f, bodyCenterZ), new Vector3(0.4f, 1f, bodyDepth), StoneColor, true, false);
        AddBlock("ParapetRight", PrimitiveType.Cube, Point(w - 0.2f, top + 0.5f, bodyCenterZ), new Vector3(0.4f, 1f, bodyDepth), StoneColor, true, false);

        foreach (float mx in new[] { -4.0f, 4.0f })
        {
            AddBlock("MerlonFront", PrimitiveType.Cube, Point(mx, top + 0.6f, frontZ), new Vector3(0.7f, 0.4f, 0.3f), DarkStoneColor, true, false);
        }
        for (float x = -4.2f; x <= 4.21f; x += 1.4f)
        {
            AddBlock("MerlonBack", PrimitiveType.Cube, Point(x, top + 1.25f, backZ), new Vector3(0.7f, 0.5f, 0.4f), DarkStoneColor, true, false);
        }
        for (float z = backZ + 0.8f; z <= frontZ - 0.6f; z += 1.4f)
        {
            AddBlock("MerlonLeft", PrimitiveType.Cube, Point(-(w - 0.2f), top + 1.25f, z), new Vector3(0.4f, 0.5f, 0.7f), DarkStoneColor, true, false);
            AddBlock("MerlonRight", PrimitiveType.Cube, Point(w - 0.2f, top + 1.25f, z), new Vector3(0.4f, 0.5f, 0.7f), DarkStoneColor, true, false);
        }

        // 四隅の塔
        float towerHeight = height + 2f;
        foreach (float tx in new[] { -w, w })
        {
            foreach (float tz in new[] { FaceForward, backZ - 0.2f })
            {
                AddBlock("Tower", PrimitiveType.Cylinder, Point(tx, ground + towerHeight * 0.5f, tz),
                    new Vector3(1.9f, towerHeight * 0.5f, 1.9f), StoneColor, true, false);
                AddBlock("TowerCap", PrimitiveType.Cylinder, Point(tx, top + 2.04f, tz),
                    new Vector3(2.2f, 0.08f, 2.2f), DarkStoneColor, true, false);
            }
        }

        // 正面の門
        AddBlock("Gate", PrimitiveType.Cube, Point(0f, ground + 1.3f, FaceForward + 0.05f), new Vector3(2.2f, 2.6f, 0.2f), WoodColor, false, false);

        // 外柵(木の柵)。地上の敵は、この柵の前まで来て、柵と砦を攻撃する
        float fenceHalf = w + 1.5f;
        for (float px = -fenceHalf; px <= fenceHalf + 0.01f; px += 0.55f)
        {
            float postHeight = 1.5f + 0.4f * Mathf.Abs(Mathf.Sin(px * 12.9898f));
            AddBlock("FencePost", PrimitiveType.Cylinder, Point(px, ground + postHeight * 0.5f, PalisadeForward),
                new Vector3(0.3f, postHeight * 0.5f, 0.3f), WoodColor, true, false);
        }
        AddBlock("FenceBeamLow", PrimitiveType.Cube, Point(0f, ground + 0.5f, PalisadeForward - 0.17f),
            new Vector3(fenceHalf * 2f + 0.4f, 0.12f, 0.1f), DarkWoodColor, true, false);
        AddBlock("FenceBeamHigh", PrimitiveType.Cube, Point(0f, ground + 1.1f, PalisadeForward - 0.17f),
            new Vector3(fenceHalf * 2f + 0.4f, 0.12f, 0.1f), DarkWoodColor, true, false);

        BuildTrees(ground);
    }

    /// <summary>遠近感と高さが分かるように、周りに木を立てる(敵の通り道と道は避ける。毎回同じ配置)。</summary>
    void BuildTrees(float ground)
    {
        var rng = new System.Random(20260920);
        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < 40; attempt++)
        {
            float x = (float)(rng.NextDouble() * 180.0 - 90.0);
            float f = (float)(rng.NextDouble() * 145.0 - 15.0);

            bool outsideEnemyCone = Mathf.Abs(x) > f * 1.1f + 8f;
            bool beyondSpawn = f > 60f;
            if (!outsideEnemyCone && !beyondSpawn) continue;
            if (Mathf.Abs(x) < 8f && f < 12f) continue;     // 砦の真後ろと真正面
            if (Mathf.Abs(x) < 7f && f > 0f) continue;      // 道の上

            float trunkHeight = 2f + (float)rng.NextDouble() * 1.5f;
            float crown = 2.4f + (float)rng.NextDouble() * 1.4f;
            float shade = (float)rng.NextDouble() * 0.12f;

            AddBlock("TreeTrunk", PrimitiveType.Cylinder, Point(x, ground + trunkHeight * 0.5f, f),
                new Vector3(0.5f, trunkHeight * 0.5f, 0.5f), WoodColor, false, false);
            AddBlock("TreeCrown", PrimitiveType.Sphere, Point(x, ground + trunkHeight + crown * 0.35f, f),
                Vector3.one * crown, new Color(0.15f + shade, 0.4f + shade, 0.15f), false, false);
            placed++;
        }
    }

    GameObject AddBlock(string blockName, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool flashesOnDamage, bool hasCollider = true)
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
        rend.material.color = color;
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

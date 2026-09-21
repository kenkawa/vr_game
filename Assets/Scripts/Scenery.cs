using UnityEngine;

/// <summary>
/// 砦のまわりの景色(明るい真昼の色)。遠くの山、森、岩、茂み、草、光る水晶、道の縁石。
/// 数が多いので、同じ色のものは 1 つの立体にまとめて、描画の回数を少なくしている(Quest 2 向け)。
/// 配置はいつも同じ(乱数の種を固定)。敵の通り道(正面に向かって開く扇形)と、道と、砦の上には置かない。
/// Fort.Build から呼ばれる。
/// </summary>
public static class Scenery
{
    static readonly Color MountainColor = new Color(0.40f, 0.50f, 0.66f);
    static readonly Color TrunkColor = new Color(0.27f, 0.18f, 0.11f);
    static readonly Color PineDark = new Color(0.14f, 0.34f, 0.26f);
    static readonly Color PineLight = new Color(0.20f, 0.40f, 0.22f);
    static readonly Color LeafGreen = new Color(0.28f, 0.48f, 0.22f);
    static readonly Color LeafGold = new Color(0.58f, 0.52f, 0.20f);
    static readonly Color RockGray = new Color(0.36f, 0.36f, 0.42f);
    static readonly Color RockMoss = new Color(0.30f, 0.40f, 0.30f);
    static readonly Color BushDark = new Color(0.18f, 0.34f, 0.20f);
    static readonly Color BushLight = new Color(0.28f, 0.42f, 0.18f);
    static readonly Color TuftGreen = new Color(0.32f, 0.52f, 0.24f);
    static readonly Color TuftDry = new Color(0.62f, 0.56f, 0.28f);
    static readonly Color CrystalColor = new Color(0.4f, 1.2f, 2.4f);
    static readonly Color RoadStone = new Color(0.42f, 0.42f, 0.47f);

    static float R(System.Random rng) { return (float)rng.NextDouble(); }
    static float R(System.Random rng, float min, float max) { return min + (float)rng.NextDouble() * (max - min); }

    public static void Build(Fort fort, Transform parent)
    {
        var root = new GameObject("Scenery").transform;
        root.SetParent(parent, false);

        float ground = fort.GroundY;
        var rng = new System.Random(20260920);

        BuildMountains(root, ground, rng);
        BuildForest(fort, root, ground, rng);
        BuildRocksAndBushes(fort, root, ground, rng);
        BuildGrassTufts(fort, root, ground, rng);
        BuildCrystals(fort, root, ground, rng);
        BuildRoadStones(fort, root, ground, rng);
    }

    /// <summary>敵の通り道(正面へ開く扇形)、道、砦の上を避けた位置なら true。f は正面への距離、x は右への距離。</summary>
    static bool IsFree(float x, float f, float coneMargin)
    {
        if (f > 0f && f <= 150f && Mathf.Abs(x) <= f * 1.2f + coneMargin) return false;   // 敵が通る扇形
        if (f > 150f && Mathf.Abs(x) < 7f) return false;                                   // 道
        if (Mathf.Abs(x) < 9f && f > -10f && f < 14f) return false;                        // 砦と柵のまわり
        return true;
    }

    static Quaternion Facing()
    {
        return Quaternion.LookRotation(PlayerView.FlatForward);
    }

    // ----------------------------------------------------------------- 山

    /// <summary>地平線をぐるりと囲む山。霧でかすんで、青紫のシルエットになる。地面のふちも隠れる。</summary>
    static void BuildMountains(Transform root, float ground, System.Random rng)
    {
        var batch = new MeshKit.Batch();
        Mesh cone = MeshKit.Cone(7);
        const int count = 28;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + R(rng) * 0.7f) / count * 360f;
            float radius = R(rng, 300f, 345f);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * PlayerView.FlatForward;
            Vector3 pos = new Vector3(PlayerView.Center.x, ground - 6f, PlayerView.Center.z) + dir * radius;

            float width = R(rng, 120f, 210f);
            float height = R(rng, 38f, 68f);
            batch.Add(cone, pos, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f),
                new Vector3(width, height, width * R(rng, 0.7f, 1.2f)));
        }
        MeshKit.Spawn("Mountains", batch, MountainColor, root, false);
    }

    // ----------------------------------------------------------------- 森

    static void BuildForest(Fort fort, Transform root, float ground, System.Random rng)
    {
        var trunks = new MeshKit.Batch();
        var pineA = new MeshKit.Batch();
        var pineB = new MeshKit.Batch();
        var roundA = new MeshKit.Batch();
        var roundB = new MeshKit.Batch();
        Mesh cyl = MeshKit.Cylinder(6);
        Mesh cone = MeshKit.Cone(7);
        Mesh sphere = MeshKit.Sphere(4, 7, true);

        int placed = 0;
        for (int attempt = 0; attempt < 1200 && placed < 150; attempt++)
        {
            float x = R(rng, -120f, 120f);
            float f = R(rng, -40f, 240f);
            if (!IsFree(x, f, 10f)) continue;

            Vector3 basePos = fort.Point(x, ground, f);
            float s = R(rng, 0.8f, 1.7f);
            bool pine = R(rng) < 0.65f;
            float trunkHeight = (pine ? 1.6f : 2.2f) * s;
            trunks.Add(cyl, basePos, Quaternion.identity, new Vector3(0.45f * s, trunkHeight, 0.45f * s));

            if (pine)
            {
                // 三角の枝を 3 段重ねた、松
                MeshKit.Batch leaves = R(rng) < 0.5f ? pineA : pineB;
                float y = trunkHeight * 0.7f;
                for (int k = 0; k < 3; k++)
                {
                    float w = (3.6f - k * 0.9f) * s;
                    float h = (2.6f - k * 0.2f) * s;
                    leaves.Add(cone, basePos + Vector3.up * y, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), new Vector3(w, h, w));
                    y += h * 0.55f;
                }
            }
            else
            {
                // 丸い葉の、広葉樹(葉のかたまりを 2 つ)
                MeshKit.Batch leaves = R(rng) < 0.5f ? roundA : roundB;
                float crown = R(rng, 3f, 4.6f) * s;
                leaves.Add(sphere, basePos + Vector3.up * (trunkHeight + crown * 0.3f),
                    Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), new Vector3(crown, crown * 0.85f, crown));
                leaves.Add(sphere, basePos + new Vector3(R(rng, -0.6f, 0.6f) * s, trunkHeight + crown * 0.62f, R(rng, -0.6f, 0.6f) * s),
                    Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), new Vector3(crown * 0.7f, crown * 0.6f, crown * 0.7f));
            }
            placed++;
        }

        MeshKit.Spawn("TreeTrunks", trunks, TrunkColor, root, false);
        MeshKit.Spawn("PineDark", pineA, PineDark, root, false);
        MeshKit.Spawn("PineLight", pineB, PineLight, root, false);
        MeshKit.Spawn("LeafGreen", roundA, LeafGreen, root, false);
        MeshKit.Spawn("LeafGold", roundB, LeafGold, root, false);
    }

    // ----------------------------------------------------------------- 岩と茂み

    static void BuildRocksAndBushes(Fort fort, Transform root, float ground, System.Random rng)
    {
        var rocks = new MeshKit.Batch();
        var mossRocks = new MeshKit.Batch();
        var bushesA = new MeshKit.Batch();
        var bushesB = new MeshKit.Batch();
        Mesh rock = MeshKit.Sphere(3, 6, true);
        Mesh bush = MeshKit.Sphere(4, 7, true);

        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < 60; attempt++)
        {
            float x = R(rng, -80f, 80f);
            float f = R(rng, -25f, 130f);
            if (!IsFree(x, f, 6f)) continue;

            Vector3 scale = new Vector3(R(rng, 0.7f, 2.0f), R(rng, 0.4f, 1.1f), R(rng, 0.6f, 1.8f));
            Quaternion rot = Quaternion.Euler(R(rng, -10f, 10f), R(rng, 0f, 360f), R(rng, -10f, 10f));
            (R(rng) < 0.3f ? mossRocks : rocks).Add(rock, fort.Point(x, ground + scale.y * 0.25f, f), rot, scale);
            placed++;
        }

        placed = 0;
        for (int attempt = 0; attempt < 700 && placed < 70; attempt++)
        {
            float x = R(rng, -80f, 80f);
            float f = R(rng, -25f, 130f);
            if (!IsFree(x, f, 4f)) continue;

            Vector3 scale = new Vector3(R(rng, 0.9f, 1.6f), R(rng, 0.6f, 1.0f), R(rng, 0.9f, 1.6f));
            (R(rng) < 0.5f ? bushesA : bushesB).Add(bush, fort.Point(x, ground + scale.y * 0.3f, f),
                Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), scale);
            placed++;
        }

        MeshKit.Spawn("Rocks", rocks, RockGray, root, false);
        MeshKit.Spawn("MossRocks", mossRocks, RockMoss, root, false);
        MeshKit.Spawn("BushesDark", bushesA, BushDark, root, false);
        MeshKit.Spawn("BushesLight", bushesB, BushLight, root, false);
    }

    // ----------------------------------------------------------------- 草

    /// <summary>砦のまわりに、草のかたまり(細い三角の葉を 3〜4 枚)。地面ののっぺり感を減らす。</summary>
    static void BuildGrassTufts(Fort fort, Transform root, float ground, System.Random rng)
    {
        var green = new MeshKit.Batch();
        var dry = new MeshKit.Batch();
        Mesh blade = MeshKit.Cone(3);

        int placed = 0;
        for (int attempt = 0; attempt < 1200 && placed < 420; attempt++)
        {
            float x = R(rng, -28f, 28f);
            float f = R(rng, -12f, 60f);
            if (Mathf.Abs(x) < 3.7f && f > 0f) continue;                    // 道
            if (Mathf.Abs(x) < 7f && f > -6f && f < 6f) continue;          // 砦と柵

            MeshKit.Batch target = R(rng) < 0.28f ? dry : green;
            int blades = 3 + (int)(R(rng) * 2f);
            float height = R(rng, 0.35f, 0.8f);
            for (int b = 0; b < blades; b++)
            {
                Vector3 pos = fort.Point(x + R(rng, -0.15f, 0.15f), ground, f + R(rng, -0.15f, 0.15f));
                target.Add(blade, pos, Quaternion.Euler(R(rng, -14f, 14f), R(rng, 0f, 360f), R(rng, -14f, 14f)),
                    new Vector3(0.07f, height * R(rng, 0.7f, 1.2f), 0.07f));
            }
            placed++;
        }

        MeshKit.Spawn("GrassTufts", green, TuftGreen, root, false);
        MeshKit.Spawn("GrassTuftsDry", dry, TuftDry, root, false);
    }

    // ----------------------------------------------------------------- 光る水晶

    /// <summary>青く光る水晶の群れ。砦の左右と後ろに置く(幻想的な雰囲気づくり)。色が 1 を超える明るさなので、暗くても光って見える。</summary>
    static void BuildCrystals(Fort fort, Transform root, float ground, System.Random rng)
    {
        var batch = new MeshKit.Batch();
        Mesh shard = MeshKit.Cone(5);

        for (int cluster = 0; cluster < 16; cluster++)
        {
            float f = R(rng, -20f, 45f);
            float side = R(rng) < 0.5f ? -1f : 1f;
            float x = side * (Mathf.Max(12f, f * 1.2f + 9f) + R(rng, 0f, 14f));
            if (!IsFree(x, f, 8f)) continue;

            int shards = 3 + (int)(R(rng) * 3f);
            for (int s = 0; s < shards; s++)
            {
                float w = R(rng, 0.3f, 0.6f);
                float h = R(rng, 0.9f, 2.4f);
                Vector3 pos = fort.Point(x + R(rng, -0.5f, 0.5f), ground - 0.05f, f + R(rng, -0.5f, 0.5f));
                batch.Add(shard, pos, Quaternion.Euler(R(rng, -18f, 18f), R(rng, 0f, 360f), R(rng, -18f, 18f)),
                    new Vector3(w, h, w));
            }
        }
        MeshKit.Spawn("Crystals", batch, CrystalColor, root, false);
    }

    // ----------------------------------------------------------------- 道の縁石

    static void BuildRoadStones(Fort fort, Transform root, float ground, System.Random rng)
    {
        var batch = new MeshKit.Batch();
        Mesh cube = MeshKit.Cube();
        for (float f = 9f; f < 220f; f += R(rng, 1.8f, 2.8f))
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (3.75f + R(rng, 0f, 0.3f));
                Vector3 scale = new Vector3(R(rng, 0.35f, 0.7f), R(rng, 0.15f, 0.3f), R(rng, 0.3f, 0.6f));
                batch.Add(cube, fort.Point(x, ground + scale.y * 0.35f, f), Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), scale);
            }
        }
        MeshKit.Spawn("RoadStones", batch, RoadStone, root, false);
    }

    // ----------------------------------------------------------------- 塔のまわり(全方向)

    /// <summary>敵の通り道の数(塔のステージで、地上の敵は、この本数の道のどれかに沿って、全方向から歩いてくる。15 度おき)。</summary>
    public const int RingLanes = 24;
    /// <summary>通り道の、中心からの左右の幅(度)。敵が出るときの、角度のばらつき(±2 度)より、少し広くしてある。</summary>
    const float RingLaneHalfDegrees = 3.5f;

    /// <summary>塔のまわりの景色を作る(全方向)。ground は地面の高さ。Tower.Build から呼ばれる。</summary>
    public static void BuildRing(Transform parent, float ground)
    {
        var root = new GameObject("Scenery").transform;
        root.SetParent(parent, false);
        var rng = new System.Random(20260921);

        BuildMountains(root, ground, rng);
        BuildRingForest(root, ground, rng);
        BuildRingRocksAndBushes(root, ground, rng);
        BuildRingGrassTufts(root, ground, rng);
        BuildRingCrystals(root, ground, rng);
    }

    /// <summary>塔の中心軸から、正面 0 度・右が + の angle 方向へ、r メートルの地面の点。</summary>
    static Vector3 RingPoint(float ground, float r, float angle)
    {
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * PlayerView.FlatForward;
        return new Vector3(PlayerView.Center.x, ground, PlayerView.Center.z) + dir * r;
    }

    /// <summary>
    /// その位置に、景色を置いてよければ true。塔と外柵のまわり(半径 minRadius の内側)と、
    /// 敵が歩いてくる通り道(出現する 140m 以内)の上には、置かない。
    /// </summary>
    static bool RingFree(float r, float angle, float minRadius)
    {
        if (r < minRadius) return false;
        if (r < 140f)
        {
            float step = 360f / RingLanes;
            float fromLane = Mathf.Repeat(angle + step * 0.5f, step) - step * 0.5f;   // いちばん近い通り道からのずれ(度)
            if (Mathf.Abs(fromLane) < RingLaneHalfDegrees) return false;
        }
        return true;
    }

    static void BuildRingForest(Transform root, float ground, System.Random rng)
    {
        var trunks = new MeshKit.Batch();
        var pineA = new MeshKit.Batch();
        var pineB = new MeshKit.Batch();
        var roundA = new MeshKit.Batch();
        var roundB = new MeshKit.Batch();
        Mesh cyl = MeshKit.Cylinder(6);
        Mesh cone = MeshKit.Cone(7);
        Mesh sphere = MeshKit.Sphere(4, 7, true);

        int placed = 0;
        for (int attempt = 0; attempt < 1500 && placed < 150; attempt++)
        {
            float angle = R(rng, 0f, 360f);
            float r = R(rng, 36f, 240f);
            if (!RingFree(r, angle, 36f)) continue;

            Vector3 basePos = RingPoint(ground, r, angle);
            float s = R(rng, 0.8f, 1.7f);
            bool pine = R(rng) < 0.65f;
            float trunkHeight = (pine ? 1.6f : 2.2f) * s;
            trunks.Add(cyl, basePos, Quaternion.identity, new Vector3(0.45f * s, trunkHeight, 0.45f * s));

            if (pine)
            {
                MeshKit.Batch leaves = R(rng) < 0.5f ? pineA : pineB;
                float y = trunkHeight * 0.7f;
                for (int k = 0; k < 3; k++)
                {
                    float w = (3.6f - k * 0.9f) * s;
                    float h = (2.6f - k * 0.2f) * s;
                    leaves.Add(cone, basePos + Vector3.up * y, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), new Vector3(w, h, w));
                    y += h * 0.55f;
                }
            }
            else
            {
                MeshKit.Batch leaves = R(rng) < 0.5f ? roundA : roundB;
                float crown = R(rng, 3f, 4.6f) * s;
                leaves.Add(sphere, basePos + Vector3.up * (trunkHeight + crown * 0.3f),
                    Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), new Vector3(crown, crown * 0.85f, crown));
                leaves.Add(sphere, basePos + new Vector3(R(rng, -0.6f, 0.6f) * s, trunkHeight + crown * 0.62f, R(rng, -0.6f, 0.6f) * s),
                    Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), new Vector3(crown * 0.7f, crown * 0.6f, crown * 0.7f));
            }
            placed++;
        }

        MeshKit.Spawn("TreeTrunks", trunks, TrunkColor, root, false);
        MeshKit.Spawn("PineDark", pineA, PineDark, root, false);
        MeshKit.Spawn("PineLight", pineB, PineLight, root, false);
        MeshKit.Spawn("LeafGreen", roundA, LeafGreen, root, false);
        MeshKit.Spawn("LeafGold", roundB, LeafGold, root, false);
    }

    static void BuildRingRocksAndBushes(Transform root, float ground, System.Random rng)
    {
        var rocks = new MeshKit.Batch();
        var mossRocks = new MeshKit.Batch();
        var bushesA = new MeshKit.Batch();
        var bushesB = new MeshKit.Batch();
        Mesh rock = MeshKit.Sphere(3, 6, true);
        Mesh bush = MeshKit.Sphere(4, 7, true);

        int placed = 0;
        for (int attempt = 0; attempt < 800 && placed < 45; attempt++)
        {
            float angle = R(rng, 0f, 360f);
            float r = R(rng, 24f, 120f);
            if (!RingFree(r, angle, 24f)) continue;

            Vector3 scale = new Vector3(R(rng, 0.7f, 2.0f), R(rng, 0.4f, 1.1f), R(rng, 0.6f, 1.8f));
            Quaternion rot = Quaternion.Euler(R(rng, -10f, 10f), R(rng, 0f, 360f), R(rng, -10f, 10f));
            Vector3 p = RingPoint(ground, r, angle) + Vector3.up * (scale.y * 0.25f);
            (R(rng) < 0.3f ? mossRocks : rocks).Add(rock, p, rot, scale);
            placed++;
        }

        placed = 0;
        for (int attempt = 0; attempt < 900 && placed < 50; attempt++)
        {
            float angle = R(rng, 0f, 360f);
            float r = R(rng, 24f, 120f);
            if (!RingFree(r, angle, 24f)) continue;

            Vector3 scale = new Vector3(R(rng, 0.9f, 1.6f), R(rng, 0.6f, 1.0f), R(rng, 0.9f, 1.6f));
            Vector3 p = RingPoint(ground, r, angle) + Vector3.up * (scale.y * 0.3f);
            (R(rng) < 0.5f ? bushesA : bushesB).Add(bush, p, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), scale);
            placed++;
        }

        MeshKit.Spawn("Rocks", rocks, RockGray, root, false);
        MeshKit.Spawn("MossRocks", mossRocks, RockMoss, root, false);
        MeshKit.Spawn("BushesDark", bushesA, BushDark, root, false);
        MeshKit.Spawn("BushesLight", bushesB, BushLight, root, false);
    }

    static void BuildRingGrassTufts(Transform root, float ground, System.Random rng)
    {
        var green = new MeshKit.Batch();
        var dry = new MeshKit.Batch();
        Mesh blade = MeshKit.Cone(3);

        int placed = 0;
        for (int attempt = 0; attempt < 1600 && placed < 380; attempt++)
        {
            float angle = R(rng, 0f, 360f);
            float r = R(rng, 22f, 75f);
            if (!RingFree(r, angle, 22f)) continue;

            MeshKit.Batch target = R(rng) < 0.28f ? dry : green;
            int blades = 3 + (int)(R(rng) * 2f);
            float height = R(rng, 0.35f, 0.8f);
            Vector3 center = RingPoint(ground, r, angle);
            for (int b = 0; b < blades; b++)
            {
                Vector3 pos = center + new Vector3(R(rng, -0.15f, 0.15f), 0f, R(rng, -0.15f, 0.15f));
                target.Add(blade, pos, Quaternion.Euler(R(rng, -14f, 14f), R(rng, 0f, 360f), R(rng, -14f, 14f)),
                    new Vector3(0.07f, height * R(rng, 0.7f, 1.2f), 0.07f));
            }
            placed++;
        }

        MeshKit.Spawn("GrassTufts", green, TuftGreen, root, false);
        MeshKit.Spawn("GrassTuftsDry", dry, TuftDry, root, false);
    }

    static void BuildRingCrystals(Transform root, float ground, System.Random rng)
    {
        var batch = new MeshKit.Batch();
        Mesh shard = MeshKit.Cone(5);

        int clusters = 0;
        for (int attempt = 0; attempt < 200 && clusters < 14; attempt++)
        {
            float angle = R(rng, 0f, 360f);
            float r = R(rng, 28f, 95f);
            if (!RingFree(r, angle, 28f)) continue;

            int shards = 3 + (int)(R(rng) * 3f);
            for (int s = 0; s < shards; s++)
            {
                float w = R(rng, 0.3f, 0.6f);
                float h = R(rng, 0.9f, 2.4f);
                Vector3 pos = RingPoint(ground, r, angle) + new Vector3(R(rng, -0.5f, 0.5f), -0.05f, R(rng, -0.5f, 0.5f));
                batch.Add(shard, pos, Quaternion.Euler(R(rng, -18f, 18f), R(rng, 0f, 360f), R(rng, -18f, 18f)),
                    new Vector3(w, h, w));
            }
            clusters++;
        }
        MeshKit.Spawn("Crystals", batch, CrystalColor, root, false);
    }
}

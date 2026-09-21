using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 起動時に、プログラムで作る模様(テクスチャ)。石のレンガ、板、草、土など。
/// 画像ファイルを使わないので、プロジェクトに素材を足す必要はない。
/// 模様は「つなげて敷き詰めても、つなぎ目が目立たない」ように作ってある。
/// 大きさは 128〜256 ピクセルと小さく、Quest 2 でも軽い。
///
/// 石・板・床の模様は、白っぽい灰色にしてあり、Paint.ApplyTextured で色を掛け合わせて使う。
/// 草と土の模様は、色そのものが入っている。
/// </summary>
public static class ProcTex
{
    /// <summary>石・床・板の模様が、1 回くり返すときの、実際の大きさ(m)。</summary>
    public const float TileMeters = 1.2f;

    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { cache.Clear(); }

    public static Texture2D Stone() { return Get("stone", () => MakeBlocks("Stone", 128, 4, 2, true, 2f, 0.14f, 11, true)); }
    public static Texture2D Flagstone() { return Get("flag", () => MakeBlocks("Flagstone", 128, 3, 3, false, 2f, 0.12f, 31, false)); }
    public static Texture2D Planks() { return Get("planks", MakePlanks); }
    public static Texture2D Grass() { return Get("grass", MakeGrass); }
    public static Texture2D Dirt() { return Get("dirt", MakeDirt); }

    static Texture2D Get(string key, System.Func<Texture2D> make)
    {
        Texture2D tex;
        if (cache.TryGetValue(key, out tex) && tex != null) return tex;
        tex = make();
        cache[key] = tex;
        return tex;
    }

    // ----------------------------------------------------------------- 模様

    /// <summary>積んだ石(レンガ)。rows 段、1 段に cols 個。stagger なら、1 段おきに半分ずらす。</summary>
    static Texture2D MakeBlocks(string name, int size, int rows, int cols, bool stagger, float mortar,
                                float variation, int seed, bool moss)
    {
        var px = new Color32[size * size];
        float rowH = size / (float)rows;
        float colW = size / (float)cols;

        for (int y = 0; y < size; y++)
        {
            int row = Mathf.Min(rows - 1, (int)(y / rowH));
            float ly = y - row * rowH;
            float shift = (stagger && (row & 1) == 1) ? colW * 0.5f : 0f;

            for (int x = 0; x < size; x++)
            {
                float xs = Mathf.Repeat(x + shift, size);
                int col = Mathf.Min(cols - 1, (int)(xs / colW));
                float lx = xs - col * colW;
                float edge = Mathf.Min(Mathf.Min(lx, colW - lx), Mathf.Min(ly, rowH - ly));

                float u = x / (float)size;
                float v = y / (float)size;
                float grain = Fbm(u, v, 16, 3, seed + 5);
                float tone = 1f + (Hash(col, row, seed) - 0.5f) * 2f * variation;
                tone *= 0.88f + 0.24f * grain;

                float r = 0.86f * tone;
                float g = 0.86f * tone;
                float b = 0.89f * tone;

                if (moss)
                {
                    // ところどころに、うっすらこけ
                    float m = Sstep(0.6f, 0.85f, Fbm(u, v, 6, 3, seed + 9));
                    r -= m * 0.10f;
                    g += m * 0.02f;
                    b -= m * 0.10f;
                }

                if (edge < mortar)
                {
                    // 目地(石と石のすき間)
                    float k = 0.34f * (0.85f + 0.3f * grain);
                    r = g = b = k;
                }
                else
                {
                    // 石の縁を、左上は明るく、右下は暗くして、盛り上がって見せる
                    float bevel = mortar + 2f;
                    if (lx < bevel || ly > rowH - bevel) { r *= 1.1f; g *= 1.1f; b *= 1.1f; }
                    else if (lx > colW - bevel || ly < bevel) { r *= 0.86f; g *= 0.86f; b *= 0.86f; }
                }

                px[y * size + x] = ToColor32(r, g, b);
            }
        }
        return Finish(px, size, name, 4);
    }

    /// <summary>縦の板を並べた模様(4 枚)。木目と、くぎの点つき。</summary>
    static Texture2D MakePlanks()
    {
        const int size = 128;
        const int seed = 51;
        const int plankCount = 4;
        float plankW = size / (float)plankCount;
        var px = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                int p = Mathf.Min(plankCount - 1, (int)(x / plankW));
                float lx = x - p * plankW;

                // 縦に長い木目
                float streak = Fbm(u, v, 24, 3, 3, seed);
                float tone = 0.92f + (Hash(p, 0, seed) - 0.5f) * 0.3f;
                tone *= 0.78f + 0.44f * streak;

                float r = 1.0f * tone;
                float g = 0.92f * tone;
                float b = 0.82f * tone;

                if (lx < 1.5f || lx > plankW - 1.5f)
                {
                    r = g = b = 0.16f;   // 板と板のすき間
                }
                else
                {
                    // 上と下に、くぎの点
                    float cx = p * plankW + plankW * 0.5f;
                    float d1 = Mathf.Abs(x - cx) + Mathf.Abs(y - 12f);
                    float d2 = Mathf.Abs(x - cx) + Mathf.Abs(y - (size - 12f));
                    if (d1 < 3.2f || d2 < 3.2f) { r = g = b = 0.22f; }
                }
                px[y * size + x] = ToColor32(r, g, b);
            }
        }
        return Finish(px, size, "Planks", 4);
    }

    /// <summary>草の地面。緑の濃淡と、ところどころ枯れた黄色、細かい点。色そのものが入っている。</summary>
    static Texture2D MakeGrass()
    {
        const int size = 128;
        const int seed = 71;
        var px = new Color32[size * size];
        Vector3 grassA = new Vector3(0.30f, 0.44f, 0.22f);
        Vector3 grassB = new Vector3(0.18f, 0.32f, 0.18f);
        Vector3 dry = new Vector3(0.46f, 0.44f, 0.24f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float big = Fbm(u, v, 4, 3, seed);
                float fine = Fbm(u, v, 32, 2, seed + 3);
                float dryAmount = Sstep(0.55f, 0.8f, Fbm(u, v, 8, 2, seed + 11)) * 0.6f;
                float speck = Hash(x, y, seed + 8);

                Vector3 c = Vector3.Lerp(grassA, grassB, big);
                c = Vector3.Lerp(c, dry, dryAmount);
                c *= 0.82f + fine * 0.36f;
                if (speck > 0.93f) c *= 1.25f;
                else if (speck < 0.07f) c *= 0.75f;

                px[y * size + x] = ToColor32(c.x, c.y, c.z);
            }
        }
        return Finish(px, size, "Grass", 4);
    }

    /// <summary>土の道。茶色の濃淡、小石の点、荷車の車輪のあと(縦の 2 本の筋)。</summary>
    static Texture2D MakeDirt()
    {
        const int size = 128;
        const int seed = 91;
        var px = new Color32[size * size];
        Vector3 baseColor = new Vector3(0.52f, 0.42f, 0.30f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float patch = Fbm(u, v, 6, 3, seed);
                float fine = Fbm(u, v, 32, 2, seed + 3);
                float speck = Hash(x / 2, y / 2, seed + 7);

                float rut = Mathf.Exp(-Mathf.Pow((u - 0.28f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((u - 0.72f) / 0.06f, 2f));

                Vector3 c = baseColor * (0.78f + patch * 0.44f);
                c *= 0.9f + fine * 0.2f;
                c *= 1f - 0.2f * rut;
                if (speck > 0.9f) c *= 1.35f;        // 明るい小石
                else if (speck < 0.05f) c *= 0.6f;   // 暗い小石

                px[y * size + x] = ToColor32(c.x, c.y, c.z);
            }
        }
        return Finish(px, size, "Dirt", 4);
    }

    // ----------------------------------------------------------------- 道具

    static Texture2D Finish(Color32[] px, int size, string name, int aniso)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = aniso;
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    static Color32 ToColor32(float r, float g, float b)
    {
        return new Color32(
            (byte)(Mathf.Clamp01(r) * 255f + 0.5f),
            (byte)(Mathf.Clamp01(g) * 255f + 0.5f),
            (byte)(Mathf.Clamp01(b) * 255f + 0.5f),
            255);
    }

    static float Sstep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    /// <summary>整数の座標から、0〜1 のでたらめな値を作る(同じ入力なら、いつも同じ値)。</summary>
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    static int Mod(int a, int m)
    {
        int r = a % m;
        return r < 0 ? r + m : r;
    }

    /// <summary>つなげて敷き詰めても切れ目が出ない、なめらかなでたらめ(値ノイズ)。</summary>
    static float ValueNoise(float x, float y, int periodX, int periodY, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        int xa = Mod(x0, periodX), xb = Mod(x0 + 1, periodX);
        int ya = Mod(y0, periodY), yb = Mod(y0 + 1, periodY);
        float a = Hash(xa, ya, seed);
        float b = Hash(xb, ya, seed);
        float c = Hash(xa, yb, seed);
        float d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int cellsX, int cellsY, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, total = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * ValueNoise(u * cellsX, v * cellsY, cellsX, cellsY, seed + o * 31);
            total += amp;
            amp *= 0.5f;
            cellsX *= 2;
            cellsY *= 2;
        }
        return sum / total;
    }

    static float Fbm(float u, float v, int cells, int octaves, int seed)
    {
        return Fbm(u, v, cells, cells, octaves, seed);
    }
}

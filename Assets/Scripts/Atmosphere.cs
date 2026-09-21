using UnityEngine;

/// <summary>
/// 空と光と空気の見た目を、起動時に整える(明るい真昼の、中世ファンタジー)。
///   ・空     :シーンの空(Skybox/Procedural)の複製を、青空の色にする(元の材質のファイルは書き換えない)
///   ・太陽   :高い位置から、プレイヤーの後ろ側から差す、ほぼ白い光。影つき(設定で切れる)。敵の顔が照らされて、見分けやすい
///   ・環境光 :上は空色、横は明るい灰色、下は明るめの土の色(3 色のグラデーション。日陰も暗くならない)
///   ・霧     :うすい青白い霧。遠くが少しかすんで、奥行きが出る
///   ・月と星 :星の数を 1 以上にしたときだけ(夜の見た目にしたいとき用)。既定では出さない
/// Quest 2 で軽く動くように、追加の光や透明な物は使わない。
/// 実行中の設定は、Play を止めると元に戻る(シーンや素材のファイルは変わらない)。
/// </summary>
public static class Atmosphere
{
    /// <summary>霧の色(遠くの山や地面も、この色にかすむ)。</summary>
    public static readonly Color FogColor = new Color(0.72f, 0.82f, 0.94f);

    static readonly Color SunColor = new Color(1f, 0.97f, 0.90f);
    static readonly Color AmbientSky = new Color(0.55f, 0.68f, 0.90f);
    static readonly Color AmbientEquator = new Color(0.62f, 0.66f, 0.72f);
    static readonly Color AmbientGround = new Color(0.36f, 0.34f, 0.30f);

    /// <summary>太陽の向き(太陽のある方向の、長さ 1 のベクトル)。</summary>
    public static Vector3 SunDirection { get; private set; }

    /// <summary>
    /// sunAzimuth = 太陽の方角(正面を 0 度、右が +)、sunElevation = 太陽の高さ(度)、
    /// starCount = 星の数(0 なら出さない)。PlayerView.Ensure() のあとに呼ぶ。
    /// </summary>
    public static void Build(float sunAzimuth, float sunElevation, bool sunShadows, float fogDensity, int starCount)
    {
        SunDirection = Direction(sunAzimuth, sunElevation);

        SetUpSun(sunShadows);
        SetUpSky();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.ambientIntensity = 1f;

        RenderSettings.fog = fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = FogColor;

        DynamicGI.UpdateEnvironment();   // 空と環境光を変えたので、光の計算を更新する

        var decor = new GameObject("SkyDecor");
        if (starCount > 0)
        {
            // 夜の見た目にしたいときだけ、月と星を出す
            BuildMoon(decor.transform);
            BuildStars(decor.transform, starCount);
        }
    }

    /// <summary>正面から見て、方角 azimuth(右が +)、高さ elevation(度)の向き。</summary>
    public static Vector3 Direction(float azimuth, float elevation)
    {
        Vector3 horizontal = Quaternion.AngleAxis(azimuth, Vector3.up) * PlayerView.FlatForward;
        float el = elevation * Mathf.Deg2Rad;
        return (horizontal * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el)).normalized;
    }

    static Vector3 EyePoint()
    {
        return new Vector3(PlayerView.Center.x, PlayerView.FloorY + 1.6f, PlayerView.Center.z);
    }

    static void SetUpSun(bool shadows)
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light l in Object.FindObjectsByType<Light>())
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }
        if (sun == null)
        {
            var go = new GameObject("Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
        sun.color = SunColor;
        sun.intensity = 1.05f;
        sun.shadows = shadows ? LightShadows.Hard : LightShadows.None;
        sun.shadowStrength = 0.65f;
        RenderSettings.sun = sun;
    }

    static void SetUpSky()
    {
        Material source = RenderSettings.skybox;
        if (source == null || source.shader == null || source.shader.name != "Skybox/Procedural") return;

        // 元の材質(ファイル)は書き換えずに、複製を夕焼けにする
        var sky = new Material(source);
        SetFloat(sky, "_SunSize", 0.04f);
        SetFloat(sky, "_SunSizeConvergence", 5f);
        SetFloat(sky, "_AtmosphereThickness", 1f);
        SetFloat(sky, "_Exposure", 1.3f);
        SetColor(sky, "_SkyTint", new Color(0.5f, 0.5f, 0.5f));
        SetColor(sky, "_GroundColor", new Color(0.55f, 0.6f, 0.65f));
        RenderSettings.skybox = sky;
    }

    static void SetFloat(Material m, string property, float value)
    {
        if (m.HasProperty(property)) m.SetFloat(property, value);
    }

    static void SetColor(Material m, string property, Color value)
    {
        if (m.HasProperty(property)) m.SetColor(property, value);
    }

    /// <summary>大きな月(白く光る球と、暗いクレーター 3 つ)。太陽とは反対側の、空の高いところ。</summary>
    static void BuildMoon(Transform parent)
    {
        Vector3 dir = Direction(-60f, 32f);
        Vector3 center = EyePoint() + dir * 150f;
        Vector3 toEye = -dir;

        Color moonColor = new Color(2f, 2f, 2.3f);
        var moon = Paint.Prim(PrimitiveType.Sphere, "Moon", moonColor);
        moon.transform.SetParent(parent, false);
        moon.transform.position = center;
        moon.transform.localScale = Vector3.one * 14f;

        Vector3 side = Vector3.Cross(Vector3.up, toEye).normalized;
        Vector3 up = Vector3.Cross(toEye, side).normalized;
        AddCrater(parent, center, toEye, side * 2.6f + up * 1.8f, 3.4f);
        AddCrater(parent, center, toEye, -side * 2.2f + up * 0.4f, 2.6f);
        AddCrater(parent, center, toEye, side * 0.4f - up * 3.0f, 3.0f);
    }

    static void AddCrater(Transform parent, Vector3 center, Vector3 toEye, Vector3 offset, float size)
    {
        var crater = Paint.Prim(PrimitiveType.Sphere, "Crater", new Color(1.3f, 1.3f, 1.6f));
        crater.transform.SetParent(parent, false);
        // 月の表面(半径 7 m)の上に、少しだけ食い込ませて置く
        float surface = Mathf.Sqrt(Mathf.Max(0.01f, 7f * 7f - offset.sqrMagnitude));
        crater.transform.position = center + offset + toEye * (surface - 0.05f);
        crater.transform.localScale = new Vector3(size, size, size * 0.3f);
        crater.transform.rotation = Quaternion.LookRotation(-toEye);
    }

    /// <summary>星(小さな白い点)。空の高いところに、ちりばめる。1 つの物体にまとめて、描画は 1 回。</summary>
    static void BuildStars(Transform parent, int count)
    {
        if (count <= 0) return;

        var rng = new System.Random(4242);
        var batch = new MeshKit.Batch();
        Mesh cube = MeshKit.Cube();
        Vector3 eye = EyePoint();
        for (int i = 0; i < count; i++)
        {
            float azimuth = (float)rng.NextDouble() * 360f;
            float elevation = 48f + (float)rng.NextDouble() * 40f;
            float size = 0.22f + (float)rng.NextDouble() * 0.2f;
            Vector3 pos = eye + Direction(azimuth, elevation) * 120f;
            batch.Add(cube, pos, Random.rotation, Vector3.one * size);
        }
        MeshKit.Spawn("Stars", batch, new Color(2.4f, 2.4f, 3f), parent, false);
    }
}

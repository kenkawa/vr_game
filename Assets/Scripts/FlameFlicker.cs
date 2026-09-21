using UnityEngine;

/// <summary>
/// たいまつの炎。ゆらゆら揺れる 2 重の炎(オレンジと黄色)と、暖色の点光源、上へ昇る火の粉。
/// 炎の色は、1 を超える明るい色にして、暗くなっても光って見えるようにしている。
/// </summary>
public class FlameFlicker : MonoBehaviour
{
    Light flameLight;
    float baseIntensity;
    float scale = 1f;
    float seed;
    Transform outer;
    Transform inner;

    Transform[] embers;
    Vector3[] velocity;
    float[] age;
    float[] life;
    float emberSize = 0.035f;

    /// <summary>炎を作る。position はワールド座標(炎の根元)。lightIntensity が 0 なら光源は付けない。</summary>
    public static FlameFlicker Create(Transform parent, Vector3 position, float scale, float lightIntensity, int emberCount)
    {
        var root = new GameObject("Flame");
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        var flame = root.AddComponent<FlameFlicker>();
        flame.scale = scale;
        flame.seed = Random.value * 100f;

        var outer = Paint.Prim(PrimitiveType.Sphere, "FlameOuter", new Color(2.6f, 1.0f, 0.18f));
        outer.transform.SetParent(root.transform, false);
        outer.transform.localPosition = new Vector3(0f, 0.13f * scale, 0f);
        flame.outer = outer.transform;

        var inner = Paint.Prim(PrimitiveType.Sphere, "FlameInner", new Color(2.8f, 2.3f, 0.9f));
        inner.transform.SetParent(root.transform, false);
        inner.transform.localPosition = new Vector3(0f, 0.08f * scale, 0f);
        flame.inner = inner.transform;

        if (lightIntensity > 0f)
        {
            var lightObject = new GameObject("FlameLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.25f * scale, 0f);
            var l = lightObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.58f, 0.24f);
            l.range = 9f;
            l.intensity = lightIntensity;
            l.shadows = LightShadows.None;
            flame.flameLight = l;
            flame.baseIntensity = lightIntensity;
        }

        if (emberCount > 0)
        {
            flame.embers = new Transform[emberCount];
            flame.velocity = new Vector3[emberCount];
            flame.age = new float[emberCount];
            flame.life = new float[emberCount];
            for (int i = 0; i < emberCount; i++)
            {
                var ember = Paint.Prim(PrimitiveType.Cube, "Ember", new Color(3f, 1.5f, 0.35f));
                ember.transform.SetParent(root.transform, false);
                flame.embers[i] = ember.transform;
                flame.Respawn(i, Random.value);   // 最初は、ばらばらのタイミングで
            }
        }

        flame.Update();
        return flame;
    }

    void Respawn(int i, float startAge01)
    {
        life[i] = Random.Range(0.8f, 1.6f);
        age[i] = life[i] * startAge01;
        velocity[i] = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1.1f), Random.Range(-0.15f, 0.15f));
        embers[i].position = transform.position + new Vector3(Random.Range(-0.06f, 0.06f), 0.2f * scale, Random.Range(-0.06f, 0.06f))
                             + velocity[i] * age[i];
    }

    void Update()
    {
        float t = Time.time;
        float n1 = Mathf.PerlinNoise(seed, t * 7f);
        float n2 = Mathf.PerlinNoise(seed + 10f, t * 11f);

        float height = 0.85f + 0.3f * n1;
        outer.localScale = new Vector3(0.16f * (0.9f + 0.2f * n2), 0.28f * height, 0.16f * (0.9f + 0.2f * n2)) * scale;
        outer.localRotation = Quaternion.Euler((n2 - 0.5f) * 16f, 0f, (n1 - 0.5f) * 16f);
        inner.localScale = new Vector3(0.09f, 0.17f * (0.8f + 0.4f * n2), 0.09f) * scale;

        if (flameLight != null) flameLight.intensity = baseIntensity * (0.7f + 0.6f * n1);

        if (embers != null)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < embers.Length; i++)
            {
                age[i] += dt;
                if (age[i] >= life[i]) Respawn(i, 0f);

                float k = age[i] / life[i];
                Vector3 drift = new Vector3(Mathf.Sin(t * 3f + i * 1.7f), 0f, Mathf.Cos(t * 2.4f + i)) * 0.12f;
                embers[i].position += (velocity[i] + drift) * dt;
                embers[i].localScale = Vector3.one * emberSize * scale * (1f - k);
            }
        }
    }
}

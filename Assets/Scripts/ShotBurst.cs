using UnityEngine;

/// <summary>
/// 火花や衝撃波の輪など、小さな立体をまとめて飛ばす演出。ため撃ちの見た目を派手にするのに使う。
/// 1 つの ShotBurst が、破片ぜんぶをまとめて動かし、時間が来たらまとめて消える。
///   Cone   = 軸の向きへ、扇状に飛ぶ(銃口の火花)
///   Ring   = 軸に垂直な輪になって、外へ広がる(衝撃波)
///   Sphere = 四方八方へ飛ぶ(着弾の火花)
/// delay を指定すると、その秒数がたってから動き出す(弾が通り過ぎるタイミングに合わせる)。
/// </summary>
public class ShotBurst : MonoBehaviour
{
    public enum Shape { Cone, Ring, Sphere }

    Transform[] pieces;
    Vector3[] velocity;
    float[] size;
    float delay;
    float life;
    float gravity;
    float drag;
    float age;
    bool started;

    public static void Spawn(Shape shape, Vector3 position, Vector3 axis, int count, float speed, float baseSize,
                             Color color, float life, float delay = 0f, float gravity = 0f, float drag = 2f)
    {
        if (count <= 0) return;
        axis = axis.sqrMagnitude < 0.0001f ? Vector3.forward : axis.normalized;

        var root = new GameObject("ShotBurst");
        root.transform.position = position;
        var burst = root.AddComponent<ShotBurst>();
        burst.pieces = new Transform[count];
        burst.velocity = new Vector3[count];
        burst.size = new float[count];
        burst.delay = delay;
        burst.life = Mathf.Max(0.05f, life);
        burst.gravity = gravity;
        burst.drag = drag;

        // 輪のための、軸に垂直な 2 方向
        Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(axis, up).normalized;
        Vector3 v = Vector3.Cross(axis, u);

        for (int i = 0; i < count; i++)
        {
            Vector3 dir;
            float pieceSpeed = speed;
            switch (shape)
            {
                case Shape.Ring:
                {
                    float angle = (i + Random.Range(-0.3f, 0.3f)) / count * 6.2831853f;
                    dir = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    break;
                }
                case Shape.Sphere:
                    dir = Random.onUnitSphere;
                    pieceSpeed *= Random.Range(0.4f, 1f);
                    break;
                default:
                    dir = (axis + Random.insideUnitSphere * 0.35f).normalized;
                    pieceSpeed *= Random.Range(0.5f, 1f);
                    break;
            }

            float s = baseSize * Random.Range(0.6f, 1.4f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = Vector3.one * s;
            go.transform.localRotation = Random.rotation;
            Paint.Apply(go.GetComponent<Renderer>(), Color.Lerp(color, Color.white, Random.Range(0f, 0.45f)));
            go.SetActive(delay <= 0f);

            burst.pieces[i] = go.transform;
            burst.velocity[i] = dir * pieceSpeed;
            burst.size[i] = s;
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age < delay) return;

        if (!started)
        {
            started = true;
            for (int i = 0; i < pieces.Length; i++) pieces[i].gameObject.SetActive(true);
        }

        float t = (age - delay) / life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        float damp = Mathf.Max(0f, 1f - drag * dt);
        float scale = 1f - t * t;
        for (int i = 0; i < pieces.Length; i++)
        {
            velocity[i] *= damp;
            velocity[i].y -= gravity * dt;
            pieces[i].position += velocity[i] * dt;
            pieces[i].localScale = Vector3.one * (size[i] * scale);
        }
    }
}

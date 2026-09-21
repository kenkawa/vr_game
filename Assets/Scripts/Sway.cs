using UnityEngine;

/// <summary>旗やバナー(垂れ幕)を、風でゆらゆら揺らす。付けた物体の、最初の向きを中心に、軸のまわりで揺れる。</summary>
public class Sway : MonoBehaviour
{
    public float degrees = 4f;
    public float speed = 1.3f;
    /// <summary>揺れる軸(この物体から見た向き)。</summary>
    public Vector3 axis = Vector3.right;

    Quaternion baseRotation;
    float seed;

    void Start()
    {
        baseRotation = transform.localRotation;
        seed = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        float angle = Mathf.Sin(t) * degrees + Mathf.Sin(t * 2.3f + 1.7f) * degrees * 0.3f;
        transform.localRotation = baseRotation * Quaternion.AngleAxis(angle, axis);
    }
}

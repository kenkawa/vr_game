using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 自分で作る、軽い立体(箱・円柱・円すい・球)と、たくさんの立体を 1 つにまとめる道具。
///
/// 木や岩や山のように数が多い飾りは、1 つずつ作ると描画の回数(ドロー コール)が増えて、Quest 2 が重くなる。
/// そこで、同じ色のものを 1 つの立体(Mesh)にまとめて、描画を 1 回にする。
/// 標準の立体(CreatePrimitive)の Mesh は、実機では読み取れないことがあるので、ここで自分で作る。
///   ・Cube     :中心が原点、一辺 1 m
///   ・Cylinder :底が原点、高さ 1 m、直径 1 m(上にふた付き、下は無し)
///   ・Cone     :底が原点、高さ 1 m、底の直径 1 m(面ごとにカクカクした見た目)
///   ・Sphere   :中心が原点、直径 1 m
/// </summary>
public static class MeshKit
{
    static Mesh cube;
    static readonly Dictionary<int, Mesh> cylinders = new Dictionary<int, Mesh>();
    static readonly Dictionary<int, Mesh> cones = new Dictionary<int, Mesh>();
    static readonly Dictionary<int, Mesh> spheres = new Dictionary<int, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        cube = null;
        cylinders.Clear();
        cones.Clear();
        spheres.Clear();
    }

    // ----------------------------------------------------------------- 形づくり

    class Data
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        public int Add(Vector3 position, Vector3 normal, Vector2 tex)
        {
            v.Add(position);
            n.Add(normal);
            uv.Add(tex);
            return v.Count - 1;
        }

        /// <summary>三角形を足す。外向きの向き(outward)に合わせて、表になる順番に自動でそろえる。</summary>
        public void Tri(int a, int b, int c, Vector3 outward)
        {
            Vector3 cross = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(cross, outward) >= 0f)
            {
                t.Add(a); t.Add(b); t.Add(c);
            }
            else
            {
                t.Add(a); t.Add(c); t.Add(b);
            }
        }

        public Mesh ToMesh(string meshName)
        {
            var m = new Mesh { name = meshName };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    public static Mesh Cube()
    {
        if (cube != null) return cube;

        var d = new Data();
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 nrm in normals)
        {
            Vector3 r = Mathf.Abs(nrm.y) > 0.5f ? Vector3.right : Vector3.Cross(nrm, Vector3.up);
            Vector3 up = Vector3.Cross(r, nrm);
            Vector3 c = nrm * 0.5f;
            int a = d.Add(c - r * 0.5f - up * 0.5f, nrm, new Vector2(0f, 0f));
            int b = d.Add(c + r * 0.5f - up * 0.5f, nrm, new Vector2(1f, 0f));
            int e = d.Add(c + r * 0.5f + up * 0.5f, nrm, new Vector2(1f, 1f));
            int f = d.Add(c - r * 0.5f + up * 0.5f, nrm, new Vector2(0f, 1f));
            d.Tri(a, b, e, nrm);
            d.Tri(a, e, f, nrm);
        }
        cube = d.ToMesh("KitCube");
        return cube;
    }

    public static Mesh Cylinder(int sides = 8)
    {
        Mesh cached;
        if (cylinders.TryGetValue(sides, out cached) && cached != null) return cached;

        var d = new Data();
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides;
            float a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            float u0 = (float)i / sides;
            float u1 = (float)(i + 1) / sides;

            int p0 = d.Add(d0 * 0.5f, d0, new Vector2(u0, 0f));
            int p1 = d.Add(d1 * 0.5f, d1, new Vector2(u1, 0f));
            int p2 = d.Add(d1 * 0.5f + Vector3.up, d1, new Vector2(u1, 1f));
            int p3 = d.Add(d0 * 0.5f + Vector3.up, d0, new Vector2(u0, 1f));
            Vector3 mid = (d0 + d1).normalized;
            d.Tri(p0, p1, p2, mid);
            d.Tri(p0, p2, p3, mid);

            // 上のふた
            int c0 = d.Add(Vector3.up, Vector3.up, new Vector2(0.5f, 0.5f));
            int c1 = d.Add(d0 * 0.5f + Vector3.up, Vector3.up, new Vector2(0.5f + d0.x * 0.5f, 0.5f + d0.z * 0.5f));
            int c2 = d.Add(d1 * 0.5f + Vector3.up, Vector3.up, new Vector2(0.5f + d1.x * 0.5f, 0.5f + d1.z * 0.5f));
            d.Tri(c0, c1, c2, Vector3.up);
        }
        Mesh m = d.ToMesh("KitCylinder" + sides);
        cylinders[sides] = m;
        return m;
    }

    public static Mesh Cone(int sides = 7)
    {
        Mesh cached;
        if (cones.TryGetValue(sides, out cached) && cached != null) return cached;

        var d = new Data();
        Vector3 apex = Vector3.up;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides;
            float a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            float am = (a0 + a1) * 0.5f;
            // 高さ 1、底の半径 0.5 の円すいの、面の向き
            Vector3 faceNormal = new Vector3(Mathf.Cos(am), 0.5f, Mathf.Sin(am)).normalized;

            int p0 = d.Add(b0, faceNormal, new Vector2(0f, 0f));
            int p1 = d.Add(b1, faceNormal, new Vector2(1f, 0f));
            int p2 = d.Add(apex, faceNormal, new Vector2(0.5f, 1f));
            d.Tri(p0, p1, p2, faceNormal);
        }
        Mesh m = d.ToMesh("KitCone" + sides);
        cones[sides] = m;
        return m;
    }

    /// <summary>球。flat が true なら、面ごとにカクカクした見た目(岩や、低ポリの木の葉向き)。</summary>
    public static Mesh Sphere(int rings = 5, int segments = 8, bool flat = true)
    {
        int key = rings * 100 + segments + (flat ? 100000 : 0);
        Mesh cached;
        if (spheres.TryGetValue(key, out cached) && cached != null) return cached;

        var d = new Data();
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                Vector3 p00 = SpherePoint(r, s, rings, segments);
                Vector3 p10 = SpherePoint(r, s + 1, rings, segments);
                Vector3 p01 = SpherePoint(r + 1, s, rings, segments);
                Vector3 p11 = SpherePoint(r + 1, s + 1, rings, segments);
                float u0 = (float)s / segments, u1 = (float)(s + 1) / segments;
                float v0 = 1f - (float)r / rings, v1 = 1f - (float)(r + 1) / rings;

                SphereTri(d, p00, p10, p11, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), flat);
                SphereTri(d, p00, p11, p01, new Vector2(u0, v0), new Vector2(u1, v1), new Vector2(u0, v1), flat);
            }
        }
        Mesh m = d.ToMesh("KitSphere" + rings + "x" + segments);
        spheres[key] = m;
        return m;
    }

    static Vector3 SpherePoint(int ring, int segment, int rings, int segments)
    {
        float phi = Mathf.PI * ring / rings;
        float theta = Mathf.PI * 2f * segment / segments;
        float radius = Mathf.Sin(phi) * 0.5f;
        return new Vector3(radius * Mathf.Cos(theta), Mathf.Cos(phi) * 0.5f, radius * Mathf.Sin(theta));
    }

    static void SphereTri(Data d, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, bool flat)
    {
        // 極のところにできる、つぶれた三角形は捨てる
        if ((a - b).sqrMagnitude < 1e-8f || (b - c).sqrMagnitude < 1e-8f || (a - c).sqrMagnitude < 1e-8f) return;

        Vector3 faceNormal = ((a + b + c) / 3f).normalized;
        int i0 = d.Add(a, flat ? faceNormal : a.normalized, ua);
        int i1 = d.Add(b, flat ? faceNormal : b.normalized, ub);
        int i2 = d.Add(c, flat ? faceNormal : c.normalized, uc);
        d.Tri(i0, i1, i2, faceNormal);
    }

    // ----------------------------------------------------------------- まとめる道具

    /// <summary>たくさんの立体を、位置・向き・大きさを付けて集めて、1 つの Mesh にまとめる。座標は、ワールド座標で渡す。</summary>
    public class Batch
    {
        readonly List<CombineInstance> items = new List<CombineInstance>();

        public int Count => items.Count;

        public void Add(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var item = new CombineInstance();
            item.mesh = mesh;
            item.transform = Matrix4x4.TRS(position, rotation, scale);
            items.Add(item);
        }

        /// <summary>まとめた Mesh を返す。1 つも足していなければ null。</summary>
        public Mesh Build(string meshName)
        {
            if (items.Count == 0) return null;

            var m = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(items.ToArray(), true, true);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>まとめた Mesh を、色付きで表示する物体にする(当たり判定なし)。</summary>
    public static GameObject Spawn(string objectName, Mesh mesh, Color color, Transform parent, bool castShadows,
                                   Texture2D texture = null, float tilingU = 1f, float tilingV = 1f)
    {
        var go = new GameObject(objectName);
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        if (texture != null) Paint.ApplyTextured(renderer, color, texture, tilingU, tilingV);
        else Paint.Apply(renderer, color);
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    /// <summary>Batch を、そのまま色付きの物体にする。何も入っていなければ null。</summary>
    public static GameObject Spawn(string objectName, Batch batch, Color color, Transform parent, bool castShadows,
                                   Texture2D texture = null, float tilingU = 1f, float tilingV = 1f)
    {
        Mesh mesh = batch.Build(objectName);
        if (mesh == null) return null;
        return Spawn(objectName, mesh, color, parent, castShadows, texture, tilingU, tilingV);
    }
}

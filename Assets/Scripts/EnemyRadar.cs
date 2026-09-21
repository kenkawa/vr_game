using UnityEngine;

/// <summary>
/// 敵の方向マーカー。360 度から敵が来るステージで、「見えていない側」に敵がいることを、視界の端の小さな矢印で知らせる。
///   ・左の端:左側(視界の外)にいる敵   ・右の端:右側(視界の外)にいる敵   ・下の中央:真後ろ(BACK)にいる敵
///   ・矢印のとなりの数字は、その方向にいる敵の数。色は、いちばん近い敵の距離(近いほど赤)
/// 頭(カメラ)の子として置くので、頭を動かしても、視界の同じ位置に出る。
/// 視界の中(正面から左右 48 度まで)の敵は、見えているので、数えない。塔から 45m 以内の敵だけを数える。
/// </summary>
public class EnemyRadar : MonoBehaviour
{
    /// <summary>見えているとみなす、正面から左右への角度(度)。これより外側の敵だけを、マーカーで知らせる。</summary>
    const float ViewHalfDegrees = 48f;
    /// <summary>これ以上、正面から離れている敵は、「真後ろ」として数える(度)。</summary>
    const float BackDegrees = 135f;
    /// <summary>この距離(m)より遠い敵は、数えない。</summary>
    const float Range = 45f;
    /// <summary>マーカーを置く、頭からの距離(m)。</summary>
    const float MarkerDistance = 2f;

    static readonly Color Near = new Color(2.2f, 0.3f, 0.2f);
    static readonly Color Middle = new Color(2.2f, 1.3f, 0.2f);
    static readonly Color Far = new Color(1.6f, 1.6f, 0.4f);

    class Marker
    {
        public Transform root;
        public Renderer arrow;
        public TextMesh label;
        public int shownCount = -1;
        public Color shownColor;
    }

    static EnemyRadar instance;

    readonly Marker[] markers = new Marker[3];   // 0 = 左、1 = 右、2 = 真後ろ
    readonly int[] counts = new int[3];
    readonly float[] nearest = new float[3];
    bool visible;
    Vector3 lastHeadFlat = Vector3.forward;

    /// <summary>方向マーカーを作る(すでにあれば、何もしない)。</summary>
    public static void Create()
    {
        if (instance != null) return;
        if (!PlayerView.Ensure()) return;

        var go = new GameObject("EnemyRadar");
        go.transform.SetParent(PlayerView.Eye, false);
        instance = go.AddComponent<EnemyRadar>();
        instance.Build();
    }

    /// <summary>マーカーを出す / 隠す。</summary>
    public static void SetVisible(bool on)
    {
        if (instance != null) instance.visible = on;
    }

    void Build()
    {
        float edge = 38f * Mathf.Deg2Rad;
        Vector3 leftPos = new Vector3(-Mathf.Sin(edge), -0.03f, Mathf.Cos(edge)) * MarkerDistance;
        Vector3 rightPos = new Vector3(Mathf.Sin(edge), -0.03f, Mathf.Cos(edge)) * MarkerDistance;
        Vector3 backPos = new Vector3(0f, -0.75f, 1.85f);

        // 矢印の先(円すいの先)が、敵のいる方向を向くようにする(左は左へ、右は右へ、後ろは下へ)
        markers[0] = BuildMarker("RadarLeft", leftPos, Quaternion.Euler(0f, 0f, 90f), false);
        markers[1] = BuildMarker("RadarRight", rightPos, Quaternion.Euler(0f, 0f, -90f), false);
        markers[2] = BuildMarker("RadarBack", backPos, Quaternion.Euler(0f, 0f, 180f), true);
    }

    Marker BuildMarker(string markerName, Vector3 localPosition, Quaternion arrowRotation, bool showBackWord)
    {
        var m = new Marker();
        var root = new GameObject(markerName).transform;
        root.SetParent(transform, false);
        root.localPosition = localPosition;
        m.root = root;

        var arrow = MeshKit.Spawn("Arrow", MeshKit.Cone(4), Near, root, false);
        arrow.transform.localRotation = arrowRotation;
        arrow.transform.localScale = new Vector3(0.09f, 0.17f, 0.09f);
        arrow.transform.localPosition = arrowRotation * new Vector3(0f, -0.085f, 0f);   // 矢印の真ん中が、マーカーの位置になるように
        m.arrow = arrow.GetComponent<Renderer>();

        m.label = HudText.Create("Count", root, 48, 0.03f, Color.white);
        m.label.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        m.label.transform.localRotation = Quaternion.LookRotation(localPosition.normalized);

        root.gameObject.SetActive(false);
        return m;
    }

    void Update()
    {
        if (!visible)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i].root.gameObject.activeSelf) markers[i].root.gameObject.SetActive(false);
            }
            return;
        }

        Transform eye = PlayerView.Eye;
        if (eye == null) return;

        Vector3 headFlat = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
        if (headFlat.sqrMagnitude > 0.09f) lastHeadFlat = headFlat.normalized;

        for (int i = 0; i < counts.Length; i++)
        {
            counts[i] = 0;
            nearest[i] = float.MaxValue;
        }

        var enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || !e.IsAlive) continue;

            Vector3 rel = e.transform.position - eye.position;
            rel.y = 0f;
            float distance = rel.magnitude;
            if (distance > Range || distance < 0.01f) continue;

            float angle = Vector3.SignedAngle(lastHeadFlat, rel, Vector3.up);   // 右が +
            float abs = Mathf.Abs(angle);
            if (abs <= ViewHalfDegrees) continue;   // 見えている

            int zone = abs >= BackDegrees ? 2 : (angle > 0f ? 1 : 0);
            counts[zone]++;
            if (distance < nearest[zone]) nearest[zone] = distance;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        for (int i = 0; i < markers.Length; i++)
        {
            Marker m = markers[i];
            bool show = counts[i] > 0;
            if (m.root.gameObject.activeSelf != show) m.root.gameObject.SetActive(show);
            if (!show)
            {
                m.shownCount = -1;
                continue;
            }

            // 近い敵ほど、赤く、大きく脈打つ
            float d = nearest[i];
            Color color = d < 20f ? Near : (d < 32f ? Middle : Far);
            float size = d < 20f ? 1f + 0.35f * pulse : 1f;
            m.root.localScale = Vector3.one * size;

            if (color != m.shownColor)
            {
                m.arrow.material.color = color;
                m.label.color = color;
                m.shownColor = color;
            }
            if (counts[i] != m.shownCount)
            {
                HudText.SetText(m.label, i == 2 ? "BACK x" + counts[i] : counts[i].ToString());
                m.shownCount = counts[i];
            }
        }
    }
}

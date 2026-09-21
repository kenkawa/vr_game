using UnityEngine;

/// <summary>
/// 文字(TextMesh)に、黒いふち取りを付ける。文字と同じ文字を、少しずらして 4 つ後ろに置く。
/// 明るい空や、暗い地面の上でも読みやすくなる。HudText.Create が自動で付ける。
/// 文字を書き換えるときは、HudText.SetText を使うと、ふち取りの文字も一緒に変わる。
/// 色の透明度(消えていく演出)は、毎フレーム、自動で合わせる。
/// </summary>
public class HudOutline : MonoBehaviour
{
    static readonly Vector2[] Offsets =
    {
        new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f),
    };

    TextMesh main;
    TextMesh[] copies;
    float lastAlpha = -1f;
    static readonly Color OutlineColor = new Color(0.02f, 0.02f, 0.05f, 0.9f);

    public void Init(TextMesh source)
    {
        main = source;
        copies = new TextMesh[Offsets.Length];

        // ずらす量は、文字の大きさに比例させる
        float d = main.characterSize * main.fontSize * 0.0055f;
        for (int i = 0; i < Offsets.Length; i++)
        {
            var go = new GameObject("Outline" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Offsets[i].x * d, Offsets[i].y * d, d * 1.5f);

            var tm = go.AddComponent<TextMesh>();
            tm.anchor = main.anchor;
            tm.alignment = main.alignment;
            tm.fontSize = main.fontSize;
            tm.characterSize = main.characterSize;
            tm.richText = false;
            tm.color = OutlineColor;
            tm.font = main.font;
            go.GetComponent<MeshRenderer>().sharedMaterial = main.font.material;
            copies[i] = tm;
        }
    }

    /// <summary>ふち取りの文字を、本体と同じ文字にする(色の指定 <color=...> などは取り除く)。</summary>
    public void SyncText(string text)
    {
        if (copies == null) return;

        string plain = StripTags(text);
        foreach (var c in copies) c.text = plain;
    }

    void LateUpdate()
    {
        if (main == null || copies == null) return;

        // 文字が消えていくとき(透明度が変わるとき)は、ふち取りも一緒に薄くする
        float alpha = main.color.a;
        if (Mathf.Approximately(alpha, lastAlpha)) return;
        lastAlpha = alpha;

        Color c = OutlineColor;
        c.a *= alpha;
        foreach (var tm in copies) tm.color = c;
    }

    static string StripTags(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        bool inTag = false;
        foreach (char ch in text)
        {
            if (ch == '<') inTag = true;
            else if (ch == '>' && inTag) inTag = false;
            else if (!inTag) sb.Append(ch);
        }
        return sb.ToString();
    }
}

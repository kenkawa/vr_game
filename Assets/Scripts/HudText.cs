using UnityEngine;

/// <summary>
/// 空間に浮かぶ文字(TextMesh)を作る小さな道具。
/// Unity 標準の古いフォントを使うので、英数字のみ表示できる。
/// 既定で、黒いふち取りを付ける(HudOutline)。文字を書き換えるときは SetText を使う。
/// </summary>
public static class HudText
{
    public static TextMesh Create(string objectName, Transform parent, int fontSize, float characterSize, Color color, bool outline = true)
    {
        var go = new GameObject(objectName);
        if (parent != null) go.transform.SetParent(parent, false);

        var tm = go.AddComponent<TextMesh>();
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.color = color;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        if (outline) go.AddComponent<HudOutline>().Init(tm);
        return tm;
    }

    /// <summary>文字を書き換える。ふち取りの文字も、一緒に変わる。</summary>
    public static void SetText(TextMesh tm, string text)
    {
        if (tm == null) return;
        tm.text = text;

        var outline = tm.GetComponent<HudOutline>();
        if (outline != null) outline.SyncText(text);
    }
}

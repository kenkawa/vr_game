using UnityEngine;

/// <summary>
/// 空間に浮かぶ文字(TextMesh)を作る小さな道具。
/// Unity 標準の古いフォントを使うので、英数字のみ表示できる。
/// </summary>
public static class HudText
{
    public static TextMesh Create(string objectName, Transform parent, int fontSize, float characterSize, Color color)
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
        return tm;
    }
}

using UnityEngine;

/// <summary>
/// ステージが切り替わる間だけ、視界を真っ黒にする(舞台を作り直す瞬間を、見せないため)。
/// 頭(カメラ)のすぐ前に、大きな黒い板を置くだけの、簡単な作り。
/// </summary>
public static class ScreenFade
{
    static GameObject cover;

    /// <summary>視界を黒くする。</summary>
    public static void Show()
    {
        if (!PlayerView.Ensure()) return;

        if (cover == null)
        {
            cover = Paint.Prim(PrimitiveType.Cube, "ScreenFade", Color.black);
            cover.transform.SetParent(PlayerView.Eye, false);
            cover.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            cover.transform.localRotation = Quaternion.identity;
            cover.transform.localScale = new Vector3(3f, 3f, 0.01f);
        }
        cover.SetActive(true);
    }

    /// <summary>視界を元に戻す。</summary>
    public static void Hide()
    {
        if (cover != null) cover.SetActive(false);
    }
}

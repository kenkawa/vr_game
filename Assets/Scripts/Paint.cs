using UnityEngine;

/// <summary>
/// スクリプトで作る標準の立体(箱、球など)に、色を付ける道具。
/// 実機(Quest)のビルドでは、CreatePrimitive が付ける標準の材質が使えず、ピンク一色になることがある。
/// そこで、プロジェクトの Resources フォルダにある材質(PrimitiveMat)を元にして、色だけ変えて使う。
/// 材質がビルドに含まれるので、そのシェーダー(Universal Render Pipeline/Simple Lit)も必ず含まれる。
/// </summary>
public static class Paint
{
    const string MaterialName = "PrimitiveMat";

    static Material template;
    static bool loaded;
    static bool warned;

    /// <summary>元にする材質。見つからなければ null。</summary>
    public static Material Template
    {
        get
        {
            if (!loaded)
            {
                template = Resources.Load<Material>(MaterialName);
                loaded = true;
            }
            return template;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        template = null;
        loaded = false;
        warned = false;
    }

    /// <summary>この描画部品に、元の材質を複製して、指定の色を付ける。</summary>
    public static void Apply(Renderer renderer, Color color)
    {
        Material source = Template;
        if (source == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[Paint] Assets/Resources/PrimitiveMat が見つかりません。標準の材質を使います(実機でピンクになることがあります)。");
            }
            renderer.material.color = color;
            return;
        }

        // 元の材質を割り当ててから、renderer.material で複製を作る(以後、renderer.material はその複製を指す)
        renderer.sharedMaterial = source;
        renderer.material.color = color;
    }
}

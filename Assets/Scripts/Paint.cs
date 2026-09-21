using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// スクリプトで作る標準の立体(箱、球など)に、色を付ける道具。
/// 実機(Quest)のビルドでは、CreatePrimitive が付ける標準の材質が使えず、ピンク一色になることがある。
/// そこで、プロジェクトの Resources フォルダにある材質(PrimitiveMat)を元にして、色だけ変えて使う。
/// 材質がビルドに含まれるので、そのシェーダー(Universal Render Pipeline/Simple Lit)も必ず含まれる。
/// </summary>
public static class Paint
{
    const string MaterialName = "PrimitiveMat";
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

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

    /// <summary>
    /// この描画部品に、元の材質を複製して、指定の色を付ける。
    /// 影は、既定で落とさない(Quest 2 の負担を減らすため)。影を落としたい物は、呼んだあとに
    /// renderer.shadowCastingMode を On にする(砦や柵がそうしている)。
    /// </summary>
    public static void Apply(Renderer renderer, Color color)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;

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

    /// <summary>
    /// 色に加えて、模様(テクスチャ)を付ける。tilingU/V は、面に模様を何回くり返すか。
    /// 模様は、色と掛け合わされる(白っぽい模様に色を付ける使い方)。
    /// </summary>
    public static void ApplyTextured(Renderer renderer, Color color, Texture2D texture, float tilingU, float tilingV)
    {
        Apply(renderer, color);
        if (texture == null || Template == null) return;

        Material m = renderer.material;   // Apply が作った複製
        if (m.HasProperty(BaseMapId))
        {
            m.SetTexture(BaseMapId, texture);
            m.SetTextureScale(BaseMapId, new Vector2(tilingU, tilingV));
        }
    }

    /// <summary>当たり判定のない標準の立体を 1 つ作って、色を付ける(位置や親は、呼んだ側で決める)。</summary>
    public static GameObject Prim(PrimitiveType type, string objectName, Color color, bool castShadows = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        Apply(renderer, color);
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }
}

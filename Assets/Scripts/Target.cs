using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 撃たれると体力が減り、0 になると消える、当たり判定つきの敵。
/// 見た目(子オブジェクト)を作り終えたあとに Configure を呼んで、体力などを設定する。
/// 撃った弾の判定は、この Target が付いたオブジェクト(またはその子)のコライダーに当たると成立する。
/// </summary>
public class Target : MonoBehaviour
{
    /// <summary>敵が倒されたときに通知される。</summary>
    public static event Action<Target> Killed;

    [SerializeField] float maxHealth = 3f;
    [SerializeField] Color hitColor = Color.white;
    [Tooltip("倒したときの得点。")]
    [SerializeField] int scoreValue = 10;
    [Tooltip("倒したとき、アイテムが出る位置の高さ(足元から、m)。")]
    [SerializeField] float dropHeight = 1f;

    float health;
    bool dying;
    Renderer[] renderers;
    Color[] baseColors;

    public bool IsDead => dying;
    public int ScoreValue => scoreValue;
    public Vector3 DropPoint => transform.position + Vector3.up * dropHeight;
    /// <summary>倒したときに、追加で落とすアイテムの数(頑丈な敵・巨大な敵)。</summary>
    public int BonusDrops { get; private set; }
    /// <summary>残りの体力の割合(0〜1)。</summary>
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

    void Awake()
    {
        health = maxHealth;
    }

    /// <summary>生成した側から、体力・得点・アイテムの出る高さを渡す。見た目を作り終えたあとに呼ぶ。</summary>
    public void Configure(float hp, int score, float dropHeightMeters, int bonusDrops = 0)
    {
        BonusDrops = bonusDrops;
        maxHealth = hp;
        health = hp;
        scoreValue = score;
        dropHeight = dropHeightMeters;
        CacheRenderers();
    }

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].material.color;
    }

    public void TakeDamage(float amount)
    {
        if (dying) return;

        health -= amount;
        if (health <= 0f)
        {
            StartCoroutine(Die());
        }
        else
        {
            GameAudio.PlayHit(transform.position);
            StartCoroutine(HitFlash());
        }
    }

    IEnumerator HitFlash()
    {
        SetAllColors(hitColor);
        yield return new WaitForSeconds(0.06f);
        if (!dying) RestoreColors();
    }

    IEnumerator Die()
    {
        dying = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        KillEffects.Play(DropPoint, baseColors, transform.lossyScale.x, scoreValue);
        GameAudio.PlayKill(DropPoint, transform.lossyScale.x >= 2f);
        Killed?.Invoke(this);

        // 0.2 秒で小さくなって消える
        Vector3 startScale = transform.localScale;
        const float duration = 0.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }

    void SetAllColors(Color c)
    {
        if (renderers == null) return;
        foreach (var r in renderers) if (r != null) r.material.color = c;
    }

    void RestoreColors()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].material.color = baseColors[i];
        }
    }
}

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 撃たれると体力が減り、0 になると消える的。
/// approachSpeed を 0 より大きくすると、プレイヤーの頭に向かって近づいてくる敵になる。
/// </summary>
[RequireComponent(typeof(Collider))]
public class Target : MonoBehaviour
{
    /// <summary>的が倒されたときに通知される。</summary>
    public static event Action<Target> Killed;

    [SerializeField] int maxHealth = 3;
    [SerializeField] Color normalColor = new Color(1f, 0.35f, 0.1f);
    [SerializeField] Color hitColor = Color.white;
    [Tooltip("近づいてくる速さ(m/秒)。0 なら動かない。")]
    [SerializeField] float approachSpeed = 0f;
    [Tooltip("頭からこの距離まで近づいたら止まる。")]
    [SerializeField] float stopDistance = 1.5f;

    int health;
    bool dying;
    Renderer rend;
    Transform head;

    void Awake()
    {
        health = maxHealth;
        rend = GetComponent<Renderer>();
        ApplyColor(normalColor);
    }

    void Start()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null) head = rig.centerEyeAnchor;
    }

    void Update()
    {
        if (dying || approachSpeed <= 0f || head == null) return;

        Vector3 toHead = head.position - transform.position;
        if (toHead.magnitude <= stopDistance) return;
        transform.position += toHead.normalized * (approachSpeed * Time.deltaTime);
    }

    /// <summary>生成した側から設定を渡すためのメソッド。</summary>
    public void Configure(int hp, float speed, Color color)
    {
        maxHealth = hp;
        health = hp;
        approachSpeed = speed;
        normalColor = color;
        ApplyColor(normalColor);
    }

    public void TakeDamage(int amount)
    {
        if (dying) return;

        health -= amount;
        if (health <= 0)
        {
            StartCoroutine(Die());
        }
        else
        {
            StartCoroutine(HitFlash());
        }
    }

    IEnumerator HitFlash()
    {
        ApplyColor(hitColor);
        yield return new WaitForSeconds(0.06f);
        if (!dying) ApplyColor(normalColor);
    }

    IEnumerator Die()
    {
        dying = true;
        GetComponent<Collider>().enabled = false;
        Killed?.Invoke(this);

        // 0.15 秒で小さくなって消える
        Vector3 startScale = transform.localScale;
        const float duration = 0.15f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }

    void ApplyColor(Color c)
    {
        if (rend != null) rend.material.color = c;
    }
}

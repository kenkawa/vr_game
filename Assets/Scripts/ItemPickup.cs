using UnityEngine;

public enum ItemKind
{
    /// <summary>銃のグレードが上がる。</summary>
    Grade,
    /// <summary>残弾が回復する。</summary>
    Ammo,
}

/// <summary>
/// 敵を倒したときに出るアイテム。
/// 少し浮かんだあと、プレイヤーに向かって自動で飛んできて、近づくと取得される。
/// </summary>
public class ItemPickup : MonoBehaviour
{
    const float PopSeconds = 0.4f;
    const float Acceleration = 12f;
    const float MaxSpeed = 14f;
    const float CollectDistance = 0.4f;
    const float LifetimeSeconds = 30f;

    ItemKind kind;
    float age;
    float speed;

    public static ItemPickup Spawn(ItemKind kind, Vector3 position)
    {
        bool isGrade = kind == ItemKind.Grade;

        var go = GameObject.CreatePrimitive(isGrade ? PrimitiveType.Cube : PrimitiveType.Capsule);
        go.name = isGrade ? "Item_GradeUp" : "Item_Ammo";
        Destroy(go.GetComponent<Collider>());

        go.transform.position = position;
        go.transform.localScale = isGrade ? Vector3.one * 0.2f : new Vector3(0.12f, 0.1f, 0.12f);
        go.GetComponent<Renderer>().material.color = isGrade
            ? new Color(1f, 0.8f, 0.1f)      // 金色: グレードアップ
            : new Color(0.2f, 1f, 0.4f);     // 緑: 残弾回復

        var pickup = go.AddComponent<ItemPickup>();
        pickup.kind = kind;
        return pickup;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);

        if (age > LifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (age < PopSeconds)
        {
            // まず少し浮かぶ
            transform.position += Vector3.up * (0.6f * Time.deltaTime);
            return;
        }

        if (!PlayerView.Ensure()) return;

        // プレイヤーの胸のあたりへ向かって、だんだん速く飛んでいく
        Vector3 target = PlayerView.Eye.position + Vector3.down * 0.25f;
        speed = Mathf.Min(MaxSpeed, speed + Acceleration * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) <= CollectDistance)
        {
            if (GunSystem.Instance != null) GunSystem.Instance.CollectItem(kind);
            Destroy(gameObject);
        }
    }
}

using System;
using UnityEngine;

/// <summary>
/// プレイヤーが立って守る建物(ステージ 1 の砦、ステージ 2 の塔)に共通の窓口。
/// 敵は、今の建物(Stronghold.Current)に向かって進み、到着したら攻撃して、その耐久値を減らす。
/// WaveSpawner は、耐久値の表示とゲームオーバーの判定に使う。
/// </summary>
public interface IStronghold
{
    /// <summary>画面に出す名前("FORT" や "TOWER")。</summary>
    string Label { get; }
    float Health { get; }
    float MaxHealth { get; }
    bool IsDestroyed { get; }
    /// <summary>地面の高さ。</summary>
    float GroundY { get; }
    /// <summary>プレイヤーが立つ床の高さ。</summary>
    float TopY { get; }

    /// <summary>ダメージを受けたとき(残りの耐久値)に通知される。</summary>
    event Action<float> Damaged;
    /// <summary>耐久値が 0 になったときに通知される。</summary>
    event Action Broken;

    void TakeDamage(float amount);
    void Repair(float amount);
}

/// <summary>今、プレイヤーが守っている建物。ステージが変わると、次の建物に入れ替わる。</summary>
public static class Stronghold
{
    public static IStronghold Current { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Current = null;
    }
}

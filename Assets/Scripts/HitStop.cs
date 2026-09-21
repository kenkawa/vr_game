using UnityEngine;

/// <summary>
/// ごく短い時間だけ、ゲームの進みをほとんど止める(ヘッドショットなどの、決定的な瞬間を強調する)。
/// 頭を動かしたときの見え方は、時間の進みに関係しないので、VR 酔いにはなりにくい。
/// </summary>
public class HitStop : MonoBehaviour
{
    static HitStop instance;
    float until;

    /// <summary>realSeconds(実際の秒数)のあいだ、時間の進みを timeScale 倍(0.2 なら 5 分の 1)にする。</summary>
    public static void Trigger(float realSeconds, float timeScale)
    {
        if (instance == null)
        {
            var go = new GameObject("HitStop");
            instance = go.AddComponent<HitStop>();
        }

        instance.until = Mathf.Max(instance.until, Time.unscaledTime + realSeconds);
        Time.timeScale = timeScale;
    }

    void Update()
    {
        if (Time.timeScale != 1f && Time.unscaledTime >= until) Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        // シーンの読み込み直しなどで消えるときに、時間の進みが止まったままにならないようにする
        if (instance == this) instance = null;
        Time.timeScale = 1f;
    }
}

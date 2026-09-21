using UnityEngine;

/// <summary>
/// 片手ぶんの「銃口を頭上に向ける」動作を見つける(武器の切り替えの合図)。
///
///   1. 銃口の向きが、水平から MinPitchDegrees(度)以上、上を向く
///   2. その状態を HoldSeconds(秒)続ける  →  成立(true を 1 回だけ返す)
///
/// 成立したあとは、銃口が ResetPitchDegrees(度)より下へ下がるまで、次の合図を受け付けない
/// (上へ向けたままで、銃 ⇄ バズーカが行ったり来たりしないように)。
/// トリガーを引いているあいだは、成立させない(空の敵を狙って撃つときに、切り替わらないように)。
/// 時間は、スローモーション(HitStop)の影響を受けない実時間で測る。
/// </summary>
public class SkyPointDetector
{
    /// <summary>この角度(度)以上、上を向けたら、合図の始まり。90 が真上。</summary>
    public float MinPitchDegrees = 60f;
    /// <summary>合図の状態を、この秒数続けると成立。</summary>
    public float HoldSeconds = 0.1f;
    /// <summary>成立したあと、この角度(度)より下へ下がったら、次の合図を受け付ける。</summary>
    public float ResetPitchDegrees = 40f;

    bool armed = true;
    float since = -1f;

    /// <summary>今の銃口の上向きの角度(度)。水平が 0、真上が 90。動作確認用。</summary>
    public float PitchDegrees { get; private set; }

    /// <summary>状態を最初に戻す。切り替えた直後は、銃口が下がるまで受け付けない。</summary>
    public void Reset()
    {
        armed = false;
        since = -1f;
    }

    /// <summary>毎フレーム呼ぶ。forward = 銃口の向き。成立したフレームだけ true を返す。</summary>
    public bool Update(Vector3 forward, bool triggerHeld)
    {
        PitchDegrees = Mathf.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

        if (PitchDegrees < ResetPitchDegrees) armed = true;

        if (triggerHeld || !armed || PitchDegrees < MinPitchDegrees)
        {
            since = -1f;
            return false;
        }

        float now = Time.unscaledTime;
        if (since < 0f) since = now;
        if (now - since < HoldSeconds) return false;

        armed = false;
        since = -1f;
        return true;
    }
}

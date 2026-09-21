using System.Collections;
using UnityEngine;

/// <summary>
/// 頭の高さと向きの補正。座ってプレイしても、立ってプレイしても、
/// 「砦の床から目まで 1.6m」で、同じ見え方になるように、プレイヤーの位置(リグ)を上下にずらす。
/// また、起動したときにプレイヤーが向いていた方向が「砦の正面(敵が来る方向)」になるように、リグを水平に回す。
/// (Quest は、起動時の頭の向きが、システムの基準の向きとずれていることがあり、そのままだと、砦の後ろ側が正面に見えて、
///  敵が背後から来てしまう。)
///   ・起動の約 2 秒後に、自動で 1 回補正する(高さと向き)
///   ・あとで姿勢や向きを変えたときは、両手のグリップ(横のボタン)を 1 秒押し続けると、補正し直す
///     (そのとき向いている方向が、新しい正面になる)
/// 実機だけで使う(Editor の固定カメラでは使わない)。
/// </summary>
public class HeightCalibrator : MonoBehaviour
{
    public const float TargetEyeHeight = 1.6f;

    /// <summary>これまでに、リグを上下に動かした合計(m)。</summary>
    public static float LastOffset { get; private set; }
    public static bool Calibrated { get; private set; }
    /// <summary>これまでに、リグを水平に回した合計(度)。</summary>
    public static float LastYawOffset { get; private set; }

    float bothGripsHeld;
    bool retriggered;

    void Awake()
    {
        LastOffset = 0f;
        LastYawOffset = 0f;
        Calibrated = false;
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        // ヘッドセットの追跡が始まっていないことがあるので、数回まで、やり直す
        for (int attempt = 0; attempt < 5 && !Calibrated; attempt++)
        {
            yield return Calibrate(false);
            if (!Calibrated) yield return new WaitForSeconds(1f);
        }
    }

    void Update()
    {
        bool both = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch)
                    && OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

        if (!both)
        {
            bothGripsHeld = 0f;
            retriggered = false;
            return;
        }

        bothGripsHeld += Time.deltaTime;
        if (!retriggered && bothGripsHeld >= 1f)
        {
            retriggered = true;
            StartCoroutine(Calibrate(true));
        }
    }

    IEnumerator Calibrate(bool manual)
    {
        if (PlayerView.Eye == null || PlayerView.Rig == null) yield break;

        // 0.5 秒ぶんの目の高さを平均する
        float sum = 0f;
        int count = 0;
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            // 追跡が始まっていないと、頭の位置が (0,0,0) のまま
            if (PlayerView.Eye.localPosition.sqrMagnitude > 0.000001f)
            {
                sum += PlayerView.Eye.position.y - PlayerView.FloorY;
                count++;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (count == 0)
        {
            Debug.LogWarning("[HeightCalibrator] 頭の位置がまだ取れていません。");
            yield break;
        }

        float eye = sum / count;
        float delta = TargetEyeHeight - eye;

        // 向き:いま向いている方向が、砦の正面になるように、頭の位置を軸にして、リグを水平に回す
        float yaw = 0f;
        Vector3 headForward = Vector3.ProjectOnPlane(PlayerView.Eye.forward, Vector3.up);
        if (headForward.sqrMagnitude > 0.09f)   // 真下や真上を向いているときは、向きが決められないので、やらない
        {
            yaw = Vector3.SignedAngle(headForward, PlayerView.FlatForward, Vector3.up);
            PlayerView.Rig.transform.RotateAround(PlayerView.Eye.position, Vector3.up, yaw);
            LastYawOffset += yaw;
        }

        PlayerView.Rig.transform.position += Vector3.up * delta;
        LastOffset += delta;
        Calibrated = true;

        Debug.Log($"[HeightCalibrator] 目の高さ {eye:0.00} m → {TargetEyeHeight:0.00} m(リグを {delta:+0.00;-0.00} m 動かしました)、向き {yaw:+0;-0} 度回しました。{(manual ? "手動" : "自動")}");
        string message = Mathf.Abs(yaw) > 5f ? "VIEW ADJUSTED" : "HEIGHT ADJUSTED";
        if (GunSystem.Instance != null) GunSystem.Instance.Announce(message, new Color(0.5f, 1f, 1f), 1.5f);
    }
}

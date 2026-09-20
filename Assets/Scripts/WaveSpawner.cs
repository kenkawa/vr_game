using System.Collections;
using UnityEngine;

/// <summary>
/// ウェーブごとに的を出し、全部倒したら次のウェーブへ進める。
/// 空の GameObject に付けるだけで動く(的や表示は起動時に自動で作る)。
/// 的の出る位置は、OVRCameraRig の正面を中心にした扇形の範囲。
/// </summary>
[DisallowMultipleComponent]
public class WaveSpawner : MonoBehaviour
{
    [Header("ウェーブ")]
    [SerializeField] int firstWaveCount = 3;
    [SerializeField] int countIncreasePerWave = 2;
    [SerializeField] float startDelay = 3f;
    [SerializeField] float timeBetweenWaves = 3f;

    [Header("的の出る範囲(プレイヤーの足元・正面が基準)")]
    [SerializeField] float minRadius = 3f;
    [SerializeField] float maxRadius = 5f;
    [Tooltip("正面から左右にこの角度(度)まで")]
    [SerializeField] float arcHalfAngle = 60f;
    [SerializeField] float minHeight = 0.8f;
    [SerializeField] float maxHeight = 2.2f;

    [Header("的の性能")]
    [Tooltip("最初のウェーブでの的の直径(m)。1 = 1m。")]
    [SerializeField] float startTargetSize = 0.6f;
    [Tooltip("ウェーブが進むごとに小さくなる量(m)。0 なら小さくならない。")]
    [SerializeField] float sizeDecreasePerWave = 0.05f;
    [Tooltip("これより小さくはならない(m)。")]
    [SerializeField] float minTargetSize = 0.15f;
    [SerializeField] int targetHealth = 3;
    [Tooltip("0 なら動かない的。0 より大きいと近づいてくる。")]
    [SerializeField] float targetApproachSpeed = 0f;
    [SerializeField] Color targetColor = new Color(1f, 0.35f, 0.1f);

    Transform origin;
    Transform eye;
    float floorY;
    Vector3 spawnCenter;
    Vector3 flatForward;
    TextMesh hud;
    int wave;
    int aliveCount;
    int score;

    void OnEnable() { Target.Killed += OnTargetKilled; }
    void OnDisable() { Target.Killed -= OnTargetKilled; }

    void Start()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("WaveSpawner: シーンに OVRCameraRig が見つかりません。Camera Rig の Building Block を追加してください。");
            enabled = false;
            return;
        }

        origin = rig.transform;
        eye = rig.centerEyeAnchor;

#if UNITY_EDITOR
        // Editor でヘッドセットなしに動作確認するときは、カメラが床の高さになってしまうので、
        // 立ったときの目の高さ(1.6m)まで持ち上げる。実機のビルドには影響しない。
        if (!OVRManager.isHmdPresent)
        {
            origin.position += Vector3.up * 1.6f;
            // 画面の外に的が出ないよう、左右の範囲も狭くする(頭を動かせないため)
            arcHalfAngle = Mathf.Min(arcHalfAngle, 30f);
        }
#endif

        UpdateSpawnFrame();
        Debug.Log($"[WaveSpawner] 開始: 床の高さ={floorY:0.00}, 基準位置={spawnCenter}, HMDあり={OVRManager.isHmdPresent}");

        CreateHud();
        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        SetHud("GET READY");
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            wave++;
            int count = firstWaveCount + (wave - 1) * countIncreasePerWave;
            UpdateSpawnFrame();
            PlaceHud();
            SpawnWave(count);
            SetHud($"WAVE {wave}\nSCORE {score}\nSIZE {CurrentTargetSize() * 100f:0} cm");

            // 全部倒すまで待つ
            while (aliveCount > 0) yield return null;

            SetHud($"WAVE {wave} CLEAR!\nSCORE {score}");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnWave(int count)
    {
        aliveCount = count;
        Debug.Log($"[WaveSpawner] WAVE {wave}: 的を {count} 個出します(直径 {CurrentTargetSize():0.00} m)。目の位置={eye.position}, 向き={eye.forward}");
        for (int i = 0; i < count; i++) SpawnTarget();
    }

    /// <summary>
    /// 的を出す基準(中心・床の高さ・正面)を決める。
    /// 実機ではカメラ台(リグ)が基準。
    /// Editor でヘッドセットがないときは、カメラの実際の位置と向きが基準になる
    /// (ヘッドセットなしだと、カメラの高さや向きが安定しないため)。
    /// </summary>
    void UpdateSpawnFrame()
    {
        Vector3 center = origin.position;
        float floor = origin.position.y;
        Vector3 forward = origin.forward;

#if UNITY_EDITOR
        if (!OVRManager.isHmdPresent && eye != null)
        {
            center = eye.position;
            floor = eye.position.y - 1.6f;   // 目の高さが 1.6m になる床を仮定
            forward = eye.forward;
        }
#endif

        spawnCenter = center;
        floorY = floor;
        flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (flatForward == Vector3.zero) flatForward = Vector3.forward;
    }

    /// <summary>今のウェーブでの的の直径(m)。ウェーブが進むほど小さくなる。</summary>
    float CurrentTargetSize()
    {
        float size = startTargetSize - (wave - 1) * sizeDecreasePerWave;
        return Mathf.Max(minTargetSize, size);
    }

    void SpawnTarget()
    {
        float angle = Random.Range(-arcHalfAngle, arcHalfAngle);
        float radius = Random.Range(minRadius, maxRadius);
        float height = Random.Range(minHeight, maxHeight);

        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * flatForward;
        Vector3 pos = spawnCenter + dir * radius;
        pos.y = floorY + height;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Target";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * CurrentTargetSize();

        Debug.Log($"[WaveSpawner] 的を生成: 位置={pos}");
        var target = go.AddComponent<Target>();
        target.Configure(targetHealth, targetApproachSpeed, targetColor);
    }

    void OnTargetKilled(Target t)
    {
        aliveCount--;
        score += 10;
        SetHud($"WAVE {wave}\nSCORE {score}\nSIZE {CurrentTargetSize() * 100f:0} cm");
    }

    void CreateHud()
    {
        var go = new GameObject("WaveHud");

        hud = go.AddComponent<TextMesh>();
        hud.anchor = TextAnchor.MiddleCenter;
        hud.alignment = TextAlignment.Center;
        hud.fontSize = 64;
        hud.characterSize = 0.05f;
        hud.color = Color.white;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hud.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        PlaceHud();
    }

    /// <summary>得点表示を、基準の正面 4m・高さ 2.6m の位置に置く。</summary>
    void PlaceHud()
    {
        if (hud == null) return;
        hud.transform.position = new Vector3(spawnCenter.x, floorY + 2.6f, spawnCenter.z) + flatForward * 4f;
        hud.transform.rotation = Quaternion.LookRotation(flatForward);
    }

    void SetHud(string text)
    {
        if (hud != null) hud.text = text;
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// ウェーブごとに的を出し、全部倒したら次のウェーブへ進める。
/// 空の GameObject に付けるだけで動く(的や表示は起動時に自動で作る)。
/// 的の出る位置は、OVRCameraRig の正面を中心にした扇形の範囲。
/// </summary>
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
    [SerializeField] float targetSize = 0.35f;
    [SerializeField] int targetHealth = 3;
    [Tooltip("0 なら動かない的。0 より大きいと近づいてくる。")]
    [SerializeField] float targetApproachSpeed = 0f;
    [SerializeField] Color targetColor = new Color(1f, 0.35f, 0.1f);

    Transform origin;
    Vector3 flatForward;
    TextMesh hud;
    int wave;
    int aliveCount;
    int score;

    void OnEnable() { Target.Killed += OnTargetKilled; }
    void OnDisable() { Target.Killed -= OnTargetKilled; }

    void Start()
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("WaveSpawner: シーンに OVRCameraRig が見つかりません。Camera Rig の Building Block を追加してください。");
            enabled = false;
            return;
        }

        origin = rig.transform;
        flatForward = Vector3.ProjectOnPlane(origin.forward, Vector3.up).normalized;
        if (flatForward == Vector3.zero) flatForward = Vector3.forward;

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
            SpawnWave(count);
            SetHud($"WAVE {wave}\nSCORE {score}");

            // 全部倒すまで待つ
            while (aliveCount > 0) yield return null;

            SetHud($"WAVE {wave} CLEAR!\nSCORE {score}");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnWave(int count)
    {
        aliveCount = count;
        for (int i = 0; i < count; i++) SpawnTarget();
    }

    void SpawnTarget()
    {
        float angle = Random.Range(-arcHalfAngle, arcHalfAngle);
        float radius = Random.Range(minRadius, maxRadius);
        float height = Random.Range(minHeight, maxHeight);

        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * flatForward;
        Vector3 pos = origin.position + dir * radius;
        pos.y = origin.position.y + height;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Target";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * targetSize;

        var target = go.AddComponent<Target>();
        target.Configure(targetHealth, targetApproachSpeed, targetColor);
    }

    void OnTargetKilled(Target t)
    {
        aliveCount--;
        score += 10;
        SetHud($"WAVE {wave}\nSCORE {score}");
    }

    void CreateHud()
    {
        var go = new GameObject("WaveHud");
        go.transform.position = origin.position + flatForward * 4f + Vector3.up * 2.6f;
        go.transform.rotation = Quaternion.LookRotation(flatForward);

        hud = go.AddComponent<TextMesh>();
        hud.anchor = TextAnchor.MiddleCenter;
        hud.alignment = TextAlignment.Center;
        hud.fontSize = 64;
        hud.characterSize = 0.05f;
        hud.color = Color.white;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hud.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    void SetHud(string text)
    {
        if (hud != null) hud.text = text;
    }
}

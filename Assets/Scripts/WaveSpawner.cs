using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム全体の流れを管理する。空の GameObject に付けるだけで動く(砦・敵・表示は起動時に自動で作る)。
///   ・プレイヤーは砦の上に立ち、正面から押し寄せる敵(地上のゴブリン、空のコウモリ)を撃つ
///   ・敵が砦に到達すると攻撃され、砦の耐久値が減る。0 になるとゲームオーバー
///   ・ウェーブが進むほど、敵が増え、小さく(=当てにくく)、速く、頑丈になる
/// 難易度の調整は、この Inspector で行う。
/// </summary>
[DisallowMultipleComponent]
public class WaveSpawner : MonoBehaviour
{
    [Header("砦")]
    [SerializeField] float fortMaxHealth = 100f;
    [Tooltip("砦の高さ(m)。プレイヤーは、この高さの上に立つ。")]
    [SerializeField] float fortHeight = 3f;
    [Tooltip("ウェーブをクリアするたびに、砦の耐久値が回復する量。")]
    [SerializeField] float repairPerWave = 10f;

    [Header("ウェーブ")]
    [SerializeField] int firstWaveCount = 3;
    [SerializeField] int countIncreasePerWave = 2;
    [SerializeField] float startDelay = 3f;
    [SerializeField] float timeBetweenWaves = 3f;
    [Tooltip("1 つのウェーブの敵を、この秒数かけて順番に出す。")]
    [SerializeField] float spawnDuration = 8f;
    [Tooltip("この番号のウェーブから、空を飛ぶ敵が出る。")]
    [SerializeField] int flyingStartWave = 2;
    [Tooltip("空を飛ぶ敵が、敵全体に占める割合(0〜1)。")]
    [Range(0f, 1f)]
    [SerializeField] float flyingShare = 0.4f;

    [Header("敵の出る位置(プレイヤーの足元・正面が基準)")]
    [Tooltip("地上の敵が出る、正面からの距離(m)")]
    [SerializeField] float groundSpawnMinDistance = 30f;
    [SerializeField] float groundSpawnMaxDistance = 45f;
    [Tooltip("空の敵が出る、正面からの距離(m)")]
    [SerializeField] float flyingSpawnMinDistance = 35f;
    [SerializeField] float flyingSpawnMaxDistance = 55f;
    [Tooltip("正面から左右にこの角度(度)まで")]
    [SerializeField] float arcHalfAngle = 45f;
    [Tooltip("地上の敵が、砦の壁からどれだけ手前で止まるか(m)")]
    [SerializeField] float groundStopDistance = 1.8f;

    [Header("難易度(ウェーブが進むごとの変化)")]
    [Tooltip("最初のウェーブでの敵の大きさ(1 = 標準)。")]
    [SerializeField] float startEnemyScale = 1.2f;
    [Tooltip("ウェーブごとに小さくなる量。0 なら小さくならない。")]
    [SerializeField] float scaleDecreasePerWave = 0.05f;
    [Tooltip("これより小さくはならない。")]
    [SerializeField] float minEnemyScale = 0.6f;
    [Tooltip("ウェーブごとに、速さが増える割合(0.05 = 5%)。")]
    [SerializeField] float speedIncreasePerWave = 0.05f;
    [Tooltip("ウェーブごとに、体力が増える割合(0.08 = 8%)。")]
    [SerializeField] float healthIncreasePerWave = 0.08f;

    [Header("敵の性能")]
    [SerializeField] EnemyStats groundStats = new EnemyStats(3f, 2.5f, 3f, 1.6f, 10);
    [SerializeField] EnemyStats flyingStats = new EnemyStats(2f, 5f, 2f, 2f, 15);

    Fort fort;
    TextMesh hud;
    int wave;
    int aliveCount;
    int score;
    bool gameOver;

    void OnEnable() { Target.Killed += OnTargetKilled; }
    void OnDisable() { Target.Killed -= OnTargetKilled; }

    void OnDestroy()
    {
        if (fort != null)
        {
            fort.Damaged -= OnFortDamaged;
            fort.Broken -= OnFortBroken;
        }
    }

    void Start()
    {
        if (!PlayerView.Ensure())
        {
            enabled = false;
            return;
        }

        // 銃の管理(GunSystem)がシーンになければ、自動で追加する
        if (GunSystem.Instance == null)
        {
            Debug.LogWarning("[WaveSpawner] GunSystem が見つからなかったので、自動で追加しました。"
                             + "Inspector で銃の設定を調整したいときは、Game オブジェクトに GunSystem コンポーネントを追加して、シーンを保存してください。");
            gameObject.AddComponent<GunSystem>();
        }

        // Editor でヘッドセットなしに確認するときは、頭を動かせないので、左右の範囲を狭くする
        if (PlayerView.IsEditorSimulation) arcHalfAngle = Mathf.Min(arcHalfAngle, 30f);

        var fortObject = new GameObject("Fort");
        fort = fortObject.AddComponent<Fort>();
        fort.Build(fortMaxHealth, fortHeight);
        fort.Damaged += OnFortDamaged;
        fort.Broken += OnFortBroken;

        CreateHud();
        StartCoroutine(RunWaves());
    }

    // ----------------------------------------------------------------- ウェーブの進行

    IEnumerator RunWaves()
    {
        RefreshHud("GET READY");
        yield return new WaitForSeconds(startDelay);

        while (!gameOver)
        {
            wave++;
            int count = firstWaveCount + (wave - 1) * countIncreasePerWave;
            int flyers = wave >= flyingStartWave ? Mathf.RoundToInt(count * flyingShare) : 0;

            if (GunSystem.Instance != null) GunSystem.Instance.OnWaveStart();
            aliveCount = count;
            Debug.Log($"[WaveSpawner] WAVE {wave}: 敵 {count} 体(空 {flyers} 体)、大きさ {CurrentScale() * 100f:0}%、速さ x{CurrentSpeedMultiplier():0.00}");
            RefreshHud();

            yield return SpawnWave(count, flyers);

            // 全部倒すまで待つ
            while (aliveCount > 0 && !gameOver) yield return null;
            if (gameOver) yield break;

            fort.Repair(repairPerWave);
            RefreshHud($"WAVE {wave} CLEAR!");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    IEnumerator SpawnWave(int count, int flyers)
    {
        // 地上と空の順番を、ばらばらにする
        var kinds = new EnemyKind[count];
        for (int i = 0; i < count; i++) kinds[i] = i < flyers ? EnemyKind.Flying : EnemyKind.Ground;
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }

        // 地上の敵が壁の前に並ぶ位置(横に 8 か所。あふれたら 2 列目)
        const int slotCount = 8;
        int[] slotOrder = new int[slotCount];
        for (int i = 0; i < slotCount; i++) slotOrder[i] = i;
        for (int i = slotCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slotOrder[i], slotOrder[j]) = (slotOrder[j], slotOrder[i]);
        }

        float interval = spawnDuration / Mathf.Max(1, count);
        int groundIndex = 0;

        for (int i = 0; i < count; i++)
        {
            if (gameOver) yield break;

            if (kinds[i] == EnemyKind.Ground)
            {
                int slot = slotOrder[groundIndex % slotCount];
                int row = groundIndex / slotCount;
                SpawnGround(slot, slotCount, row);
                groundIndex++;
            }
            else
            {
                SpawnFlying();
            }

            yield return new WaitForSeconds(interval * Random.Range(0.6f, 1.4f));
        }
    }

    void SpawnGround(int slot, int slotCount, int row)
    {
        float span = Fort.HalfWidth - 0.8f;
        float x = Mathf.Lerp(-span, span, slot / (float)(slotCount - 1));

        float stopForward = Fort.FaceForward + groundStopDistance + row * 1.2f;
        Vector3 stand = fort.Point(x, fort.GroundY, stopForward);
        Vector3 look = fort.Point(x, fort.GroundY + 1f, Fort.FaceForward);

        float angle = Random.Range(-arcHalfAngle, arcHalfAngle);
        float distance = Random.Range(groundSpawnMinDistance, groundSpawnMaxDistance);
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * PlayerView.FlatForward;
        Vector3 flat = new Vector3(PlayerView.Center.x, 0f, PlayerView.Center.z) + dir * distance;
        Vector3 spawn = new Vector3(flat.x, fort.GroundY, flat.z);

        Enemy.Spawn(EnemyKind.Ground, spawn, stand, look, CurrentScale(), CurrentSpeedMultiplier(), CurrentHealthMultiplier(), groundStats);
    }

    void SpawnFlying()
    {
        float x = Random.Range(-(Fort.HalfWidth - 1f), Fort.HalfWidth - 1f);
        float hoverHeight = fort.TopY + Random.Range(0.5f, 3f);
        Vector3 stand = fort.Point(x, hoverHeight, Fort.FaceForward + 2.2f);
        Vector3 look = fort.Point(x, fort.TopY - 0.5f, Fort.FaceForward);

        float angle = Random.Range(-arcHalfAngle, arcHalfAngle);
        float distance = Random.Range(flyingSpawnMinDistance, flyingSpawnMaxDistance);
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * PlayerView.FlatForward;
        Vector3 flat = new Vector3(PlayerView.Center.x, 0f, PlayerView.Center.z) + dir * distance;
        Vector3 spawn = new Vector3(flat.x, fort.TopY + Random.Range(4f, 14f), flat.z);

        Enemy.Spawn(EnemyKind.Flying, spawn, stand, look, CurrentScale(), CurrentSpeedMultiplier(), CurrentHealthMultiplier(), flyingStats);
    }

    // ----------------------------------------------------------------- 難易度

    /// <summary>今のウェーブでの敵の大きさ。ウェーブが進むほど小さくなる。</summary>
    float CurrentScale()
    {
        return Mathf.Max(minEnemyScale, startEnemyScale - (wave - 1) * scaleDecreasePerWave);
    }

    float CurrentSpeedMultiplier() { return 1f + (wave - 1) * speedIncreasePerWave; }

    float CurrentHealthMultiplier() { return 1f + (wave - 1) * healthIncreasePerWave; }

    // ----------------------------------------------------------------- イベント

    void OnTargetKilled(Target t)
    {
        if (gameOver) return;
        aliveCount--;
        score += t.ScoreValue;
        RefreshHud();
    }

    void OnFortDamaged(float health)
    {
        RefreshHud();
    }

    void OnFortBroken()
    {
        if (gameOver) return;
        gameOver = true;
        StartCoroutine(GameOver());
    }

    IEnumerator GameOver()
    {
        Debug.Log($"[WaveSpawner] GAME OVER: WAVE {wave}, SCORE {score}");

        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(enemy.gameObject);

        hud.color = new Color(1f, 0.35f, 0.3f);
        hud.text = $"GAME OVER\nWAVE {wave}   SCORE {score}\n\nHOLD BOTH TRIGGERS\nTO RETRY";

        yield return new WaitForSeconds(1.5f);

        // 両方のトリガーを 1 秒引き続けたら、最初からやり直す
        float held = 0f;
        while (held < 1f)
        {
            bool both = GunSystem.Instance != null && GunSystem.Instance.BothTriggersHeld;
            held = both ? held + Time.deltaTime : 0f;
            yield return null;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ----------------------------------------------------------------- 表示

    void CreateHud()
    {
        hud = HudText.Create("WaveHud", null, 64, 0.05f, Color.white);
        hud.transform.position = new Vector3(PlayerView.Center.x, PlayerView.FloorY + 2.2f, PlayerView.Center.z)
                                 + PlayerView.FlatForward * 5f;
        hud.transform.rotation = Quaternion.LookRotation(PlayerView.FlatForward);
    }

    void RefreshHud(string title = null)
    {
        if (hud == null || gameOver) return;

        if (title == null) title = $"WAVE {wave}";
        float ratio = fort.MaxHealth > 0f ? fort.Health / fort.MaxHealth : 0f;
        hud.color = ratio <= 0.3f ? new Color(1f, 0.4f, 0.35f) : Color.white;
        hud.text = $"{title}   SCORE {score}\nFORT {Mathf.CeilToInt(fort.Health)}/{Mathf.CeilToInt(fort.MaxHealth)}\nENEMY SIZE {CurrentScale() * 100f:0}%";
    }
}

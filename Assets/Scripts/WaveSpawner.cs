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
    [SerializeField] float fortHeightMeters = 6f;
    [Tooltip("ウェーブをクリアするたびに、砦の耐久値が回復する量。")]
    [SerializeField] float repairPerWave = 10f;

    [Header("音")]
    [Tooltip("BGM を流すか。")]
    [SerializeField] bool playBgm = true;
    [Range(0f, 1f)]
    [SerializeField] float bgmVolume = 0.35f;
    [Tooltip("効果音の大きさ(0〜1)。")]
    [Range(0f, 1f)]
    [SerializeField] float sfxVolume = 1f;

    [Header("見た目(明るい真昼の、中世ファンタジー)")]
    [Tooltip("太陽の方角(度)。正面が 0、右が +、左が -。150 は、プレイヤーの後ろの右側から照らす(敵の顔が明るく見える)。正面に近づけると、敵が逆光で暗く見える。")]
    [SerializeField] float sunAngleFromFront = 150f;
    [Tooltip("太陽の高さ(度)。大きいほど真昼に近く、影が短くなる。小さいほど夕方に近い。")]
    [SerializeField] float sunHeightAngle = 55f;
    [Tooltip("太陽の影を出すか(柵と敵の影)。Quest 2 で重いときは、まずこれを切る。")]
    [SerializeField] bool castSunShadows = true;
    [Tooltip("たいまつの暖かい光(点光源)を出すか。真昼は目立たないので、既定は切ってある。炎の見た目は残る。")]
    [SerializeField] bool torchGlowLights = false;
    [Tooltip("霧の濃さ。0 で霧なし。大きいほど、遠くが白っぽく青くかすむ。")]
    [SerializeField] float hazeDensity = 0.0025f;
    [Tooltip("空の星の数。0 なら、月も星も出ない(昼)。1 以上で、夜の空になる。")]
    [SerializeField] int starTotal = 0;

    [Header("文字の表示(ウェーブ・スコア・砦の耐久値)")]
    [Tooltip("表示の方角(度)。正面が 0、左が -、右が +。絶対値を大きくするほど、視界の端に寄る。")]
    [SerializeField] float hudAngleFromFront = -28f;
    [Tooltip("表示の高さ(床から、m)。目の高さは 1.6。大きいほど、上に寄る。")]
    [SerializeField] float hudHeightAboveFloor = 2.45f;
    [Tooltip("表示までの距離(m)。")]
    [SerializeField] float hudDistance = 4.5f;

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

    [Header("敵の出る位置(プレイヤーの足元・正面が基準。遠くから近づいてくる)")]
    [Tooltip("地上の敵が出る、正面からの距離(m)。この範囲のどこかから、歩いて近づいてくる。")]
    [SerializeField] float groundSpawnNearMeters = 90f;
    [SerializeField] float groundSpawnFarMeters = 120f;
    [Tooltip("空の敵が出る、正面からの距離(m)")]
    [SerializeField] float flyingSpawnNearMeters = 100f;
    [SerializeField] float flyingSpawnFarMeters = 140f;
    [Tooltip("遠くにいるあいだの、進む速さの倍率。到着までが長くなりすぎないように、遠いほど速く進む。1 なら、ずっと同じ速さ。")]
    [SerializeField] float farSpeedBoost = 2.5f;
    [Tooltip("遠くにいるあいだの、進む速さの下限(m/秒)。遅い敵(巨大な敵など)も、遠くでは、少なくともこの速さで進む。")]
    [SerializeField] float farMinSpeed = 6f;
    [Tooltip("砦の手前のこの距離(m)より遠いところから、速く進み始める。")]
    [SerializeField] float boostStartMeters = 30f;
    [Tooltip("砦の手前のこの距離(m)より遠いところでは、最大の速さで進む。")]
    [SerializeField] float boostFullMeters = 70f;
    [Tooltip("正面から左右にこの角度(度)まで")]
    [SerializeField] float arcHalfAngle = 45f;
    [Tooltip("地上の敵が、外柵(砦の壁の 3.5m 手前)からどれだけ手前で止まるか(m)")]
    [SerializeField] float groundStopFromFence = 0.9f;

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

    [Header("敵の性能(標準)")]
    [SerializeField] EnemyStats groundStats = new EnemyStats(3f, 2.5f, 3f, 1.6f, 10);
    [SerializeField] EnemyStats flyingStats = new EnemyStats(2f, 5f, 2f, 2f, 15);

    [Header("地上の敵の種類")]
    [Tooltip("頑丈な敵(鎧のゴブリン)が出始めるウェーブ。")]
    [SerializeField] int armoredStartWave = 3;
    [Tooltip("地上の敵のうち、頑丈な敵になる割合(0〜1)。")]
    [Range(0f, 1f)]
    [SerializeField] float armoredShare = 0.25f;
    [Tooltip("俊敏な敵(小さくて速いゴブリン)が出始めるウェーブ。")]
    [SerializeField] int runnerStartWave = 2;
    [Range(0f, 1f)]
    [SerializeField] float runnerShare = 0.25f;
    [Tooltip("巨大な敵(オーガ)が最初に出るウェーブ。")]
    [SerializeField] int giantStartWave = 4;
    [Tooltip("巨大な敵が、このウェーブ数ごとに 1 体出る。0 なら出ない。")]
    [SerializeField] int giantEveryNWaves = 2;
    [SerializeField] EnemyStats armoredStats = new EnemyStats(9f, 1.6f, 4f, 1.8f, 30, 1.15f, 1);
    [SerializeField] EnemyStats giantStats = new EnemyStats(30f, 1f, 12f, 2.5f, 100, 2.4f, 2);
    [SerializeField] EnemyStats runnerStats = new EnemyStats(1.5f, 5.5f, 2f, 1f, 20, 0.7f, 0);

    Fort fort;
    TextMesh hud;
    Transform fortBarBack;
    Transform fortBarFill;
    Renderer fortBarFillRenderer;
    string diagnostics = "";
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

        // 遠くの敵ほど速く進む設定を、敵に渡す
        Enemy.FarSpeedBoost = Mathf.Max(1f, farSpeedBoost);
        Enemy.FarMinSpeed = Mathf.Max(0f, farMinSpeed);
        Enemy.BoostStartMeters = boostStartMeters;
        Enemy.BoostFullMeters = Mathf.Max(boostStartMeters + 1f, boostFullMeters);

        // 空・光・霧(砦より先に整える)
        Atmosphere.Build(sunAngleFromFront, sunHeightAngle, castSunShadows, hazeDensity, starTotal);
        Enemy.ProxyShadows = castSunShadows;

        var fortObject = new GameObject("Fort");
        fort = fortObject.AddComponent<Fort>();
        fort.TorchLights = torchGlowLights;
        fort.CastShadows = castSunShadows;
        fort.Build(fortMaxHealth, fortHeightMeters);
        fort.Damaged += OnFortDamaged;
        fort.Broken += OnFortBroken;

        // 音(効果音と BGM)
        GameAudio.Ensure(sfxVolume, bgmVolume);
        if (playBgm) GameAudio.StartBgm();

        // 実機では、座っていても立っていても、目の高さが砦の上で一定になるように補正する
        if (!PlayerView.IsEditorSimulation && GetComponent<HeightCalibrator>() == null)
        {
            gameObject.AddComponent<HeightCalibrator>();
        }

        CreateHud();
        if (!PlayerView.IsEditorSimulation) StartCoroutine(ShowDiagnostics());
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
            GameAudio.PlayWaveStart();
            aliveCount = count;
            Debug.Log($"[WaveSpawner] WAVE {wave}: 敵 {count} 体(空 {flyers} 体)、大きさ {CurrentScale() * 100f:0}%、速さ x{CurrentSpeedMultiplier():0.00}");
            RefreshHud();

            yield return SpawnWave(count, flyers);

            // 全部倒すまで待つ
            while (aliveCount > 0 && !gameOver) yield return null;
            if (gameOver) yield break;

            fort.Repair(repairPerWave);
            GameAudio.PlayWaveClear();
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

        // 地上の敵の種類を決める(巨大な敵は、該当のウェーブに 1 体)
        var variants = new EnemyVariant[count];
        bool giantWave = giantEveryNWaves > 0 && wave >= giantStartWave && (wave - giantStartWave) % giantEveryNWaves == 0;
        bool giantPlaced = false;
        for (int i = 0; i < count; i++)
        {
            if (kinds[i] != EnemyKind.Ground) continue;

            if (giantWave && !giantPlaced)
            {
                variants[i] = EnemyVariant.Giant;
                giantPlaced = true;
                continue;
            }

            float armored = wave >= armoredStartWave ? armoredShare : 0f;
            float runner = wave >= runnerStartWave ? runnerShare : 0f;
            float roll = Random.value;
            if (roll < armored) variants[i] = EnemyVariant.Armored;
            else if (roll < armored + runner) variants[i] = EnemyVariant.Runner;
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
                SpawnGround(slot, slotCount, row, variants[i]);
                groundIndex++;
            }
            else
            {
                SpawnFlying();
            }

            yield return new WaitForSeconds(interval * Random.Range(0.6f, 1.4f));
        }
    }

    void SpawnGround(int slot, int slotCount, int row, EnemyVariant variant)
    {
        EnemyStats stats = groundStats;
        if (variant == EnemyVariant.Armored) stats = armoredStats;
        else if (variant == EnemyVariant.Giant) stats = giantStats;
        else if (variant == EnemyVariant.Runner) stats = runnerStats;

        float span = Fort.HalfWidth - 0.8f;
        float x = Mathf.Lerp(-span, span, slot / (float)(slotCount - 1));

        float stopForward = Fort.PalisadeForward + groundStopFromFence * Mathf.Max(1f, stats.sizeMultiplier) + row * 1.2f;
        Vector3 stand = fort.Point(x, fort.GroundY, stopForward);
        Vector3 look = fort.Point(x, fort.GroundY + 1f, Fort.PalisadeForward);

        float angle = Random.Range(-arcHalfAngle, arcHalfAngle);
        float distance = Random.Range(groundSpawnNearMeters, groundSpawnFarMeters);
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * PlayerView.FlatForward;
        Vector3 flat = new Vector3(PlayerView.Center.x, 0f, PlayerView.Center.z) + dir * distance;
        Vector3 spawn = new Vector3(flat.x, fort.GroundY, flat.z);

        float size = CurrentScale() * stats.sizeMultiplier;
        Enemy.Spawn(EnemyKind.Ground, spawn, stand, look, size, CurrentSpeedMultiplier(), CurrentHealthMultiplier(), stats, variant);
    }

    void SpawnFlying()
    {
        float x = Random.Range(-(Fort.HalfWidth - 1f), Fort.HalfWidth - 1f);
        float hoverHeight = fort.TopY + Random.Range(0.5f, 3f);
        Vector3 stand = fort.Point(x, hoverHeight, Fort.FaceForward + 2.2f);
        Vector3 look = fort.Point(x, fort.TopY - 0.5f, Fort.FaceForward);

        float angle = Random.Range(-arcHalfAngle, arcHalfAngle);
        float distance = Random.Range(flyingSpawnNearMeters, flyingSpawnFarMeters);
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
        GameAudio.StopBgm();
        GameAudio.PlayGameOver();

        foreach (var enemy in FindObjectsByType<Enemy>()) Destroy(enemy.gameObject);

        hud.color = new Color(1f, 0.35f, 0.3f);
        HudText.SetText(hud, $"GAME OVER\nWAVE {wave}   SCORE {score}\n\nHOLD BOTH TRIGGERS\nTO RETRY");
        if (fortBarBack != null) fortBarBack.gameObject.SetActive(false);

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

    /// <summary>実機での確認用。高さ補正のあと(起動の約 4.5 秒後)から約 15 秒間、頭の高さなどを表示の下に出す。</summary>
    IEnumerator ShowDiagnostics()
    {
        yield return new WaitForSeconds(4.5f);

        float eyeHeight = PlayerView.Eye.position.y - PlayerView.FloorY;
        string origin = OVRManager.instance != null ? OVRManager.instance.trackingOriginType.ToString() : "?";
        string material = Paint.Template != null ? "OK" : "NONE";
        string adjusted = HeightCalibrator.Calibrated ? $"{HeightCalibrator.LastOffset:+0.00;-0.00}m" : "NONE";
        diagnostics = $"\nEYE {eyeHeight:0.00}m  ADJ {adjusted}  YAW {HeightCalibrator.LastYawOffset:+0;-0}deg  ORIGIN {origin}  MAT {material}\nHOLD BOTH GRIPS 1s TO RESET HEIGHT AND FACING";
        Debug.Log($"[WaveSpawner] 頭の高さ(砦の床から)={eyeHeight:0.00} m、補正量={adjusted}、向きの補正={HeightCalibrator.LastYawOffset:+0;-0} 度、トラッキング原点={origin}、材質(PrimitiveMat)={material}");
        RefreshHud();

        yield return new WaitForSeconds(15f);
        diagnostics = "";
        RefreshHud();
    }

    void CreateHud()
    {
        hud = HudText.Create("WaveHud", null, 64, 0.05f, new Color(0.93f, 0.95f, 1f));
        // 視界の端(既定は、左上)に置き、プレイヤーのほうを向ける
        Vector3 hudDirection = Quaternion.AngleAxis(hudAngleFromFront, Vector3.up) * PlayerView.FlatForward;
        hud.transform.position = new Vector3(PlayerView.Center.x, PlayerView.FloorY + hudHeightAboveFloor, PlayerView.Center.z)
                                 + hudDirection * hudDistance;
        hud.transform.rotation = Quaternion.LookRotation(hudDirection);

        // 砦の耐久値を示すバー(文字の上)。金の枠の中で、緑から赤へ変わる
        var back = Paint.Prim(PrimitiveType.Cube, "FortBarFrame", new Color(0.9f, 0.7f, 0.25f));
        back.transform.SetParent(hud.transform, false);
        back.transform.localPosition = new Vector3(0f, 0.85f, 0.03f);
        back.transform.localScale = new Vector3(3.3f, 0.22f, 0.02f);
        fortBarBack = back.transform;

        var inner = Paint.Prim(PrimitiveType.Cube, "FortBarBack", new Color(0.05f, 0.05f, 0.08f));
        inner.transform.SetParent(back.transform, false);
        inner.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        inner.transform.localScale = new Vector3(0.96f, 0.7f, 1f);

        var fill = Paint.Prim(PrimitiveType.Cube, "FortBarFill", Color.green);
        fill.transform.SetParent(back.transform, false);
        fortBarFill = fill.transform;
        fortBarFillRenderer = fill.GetComponent<Renderer>();
    }

    void RefreshHud(string title = null)
    {
        if (hud == null || gameOver) return;

        if (title == null) title = $"WAVE {wave}";
        float ratio = fort.MaxHealth > 0f ? Mathf.Clamp01(fort.Health / fort.MaxHealth) : 0f;
        string fortColor = ratio <= 0.3f ? "#FF6659" : "#FFFFFF";
        HudText.SetText(hud,
            $"<color=#FFD24A>{title}</color>   SCORE <color=#FFFFFF>{score}</color>\n" +
            $"<color={fortColor}>FORT {Mathf.CeilToInt(fort.Health)}/{Mathf.CeilToInt(fort.MaxHealth)}</color>\n" +
            $"ENEMY SIZE {CurrentScale() * 100f:0}%{diagnostics}");

        // バー(親の枠の大きさに対する割合で、左から伸び縮みする)
        if (fortBarFill != null)
        {
            const float innerWidth = 0.94f;
            float width = Mathf.Max(0.001f, innerWidth * ratio);
            fortBarFill.localScale = new Vector3(width, 0.58f, 1f);
            fortBarFill.localPosition = new Vector3(-(innerWidth - width) * 0.5f, 0f, -1.2f);
            fortBarFillRenderer.material.color = Color.Lerp(new Color(0.95f, 0.15f, 0.1f), new Color(0.2f, 0.9f, 0.3f), ratio);
        }
    }
}

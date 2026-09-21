using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム全体の流れを管理する。空の GameObject に付けるだけで動く(砦・敵・表示は起動時に自動で作る)。
///   ・ステージ 1:プレイヤーは砦の上に立ち、正面から押し寄せる敵(地上のゴブリン、空のコウモリ)を撃つ
///   ・ステージ 2:プレイヤーは高い塔の頂上に立ち、全方向(360 度)から囲んでくる敵を撃つ
///   ・各ステージは、決まった数のウェーブをすべて全滅させるとクリア。クリアすると、次のステージへ進む(残弾とグレードは持ち越し)
///   ・敵が建物(砦・塔)に到達すると攻撃され、耐久値が減る。0 になるとゲームオーバー
///   ・ウェーブが進むほど、敵が増え、小さく(=当てにくく)、速く、頑丈になる(ステージをまたいで、ウェーブの通し番号で強くなる)
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

    [Header("ステージ(すべてのウェーブを全滅させると、次のステージへ進む)")]
    [Tooltip("各ステージの、ウェーブの数。この数のウェーブをすべて全滅させると、そのステージをクリア。")]
    [SerializeField] int wavesToClear = 5;
    [Tooltip("オフにすると、ステージ 2(塔)を作らず、砦のステージをクリアしたらゲームクリアになる。")]
    [SerializeField] bool includeTowerStage = true;
    [Tooltip("ステージをクリアしてから、次のステージへ進むまでの秒数。")]
    [SerializeField] float stageClearSeconds = 4f;
    [Tooltip("確認用。2 にすると、ステージ 2(塔)から始める(ステージ 1 を遊ばなくても、塔を試せる)。ふだんは 1。")]
    [SerializeField] int startAtStage = 1;

    [Header("塔(ステージ 2。全方向から敵が来る)")]
    [Tooltip("塔の耐久値。0 でゲームオーバー。")]
    [SerializeField] float towerMaxHealth = 120f;
    [Tooltip("塔の高さ(m)。プレイヤーは、この高さの頂上に立つ。")]
    [SerializeField] float towerHeightMeters = 12f;
    [Tooltip("外柵(塔をぐるりと囲む木の柵)の半径(m)。地上の敵は、この外側で止まって、柵と塔を攻撃する。"
             + "頂上の真ん中からは、塔から約 13m 以内の地面は、足場に隠れて見えないので、それより外に置いてある。")]
    [SerializeField] float towerFenceRadius = 15.5f;
    [Tooltip("空の敵が、頂上のまわりを飛ぶ半径(m)の範囲(塔の中心軸から)。")]
    [SerializeField] float towerFlyMinRadius = 3.2f;
    [SerializeField] float towerFlyMaxRadius = 5f;
    [Tooltip("視界の外にいる敵の方向を、視界の端の矢印で知らせる。")]
    [SerializeField] bool showEnemyMarkers = true;
    [Tooltip("塔のステージの最初のウェーブで、敵が来る範囲(正面から、左右へ何度までか)。いきなり囲まないように、最初は正面寄りにして、ウェーブごとに広げる。")]
    [Range(0f, 180f)]
    [SerializeField] float towerOpeningHalfAngle = 45f;
    [Tooltip("塔のステージの中で、この番号のウェーブになると、全方向(360 度)から来る。それまでは、ウェーブごとに、来る範囲が少しずつ広がる。1 にすると、最初から全方向。")]
    [SerializeField] int towerSurroundWave = 5;

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
    Tower tower;
    IStronghold stronghold;
    int stage = 1;
    int stageWave;
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
        Unsubscribe();
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

        // 最初の舞台(確認用に startAtStage を 2 にすると、塔から始まる)
        if (startAtStage >= 2 && includeTowerStage) BuildTower();
        else BuildFort();

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
        StartCoroutine(RunGame());
    }

#if UNITY_EDITOR
    /// <summary>Editor でヘッドセットなしに確認するとき、左右の矢印キーで、視点を水平に回せる(360 度のステージを、見回して確認するため)。</summary>
    void Update()
    {
        if (!PlayerView.IsEditorSimulation || PlayerView.Eye == null) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        float turn = 0f;
        if (keyboard.leftArrowKey.isPressed) turn -= 1f;
        if (keyboard.rightArrowKey.isPressed) turn += 1f;
        if (turn != 0f) PlayerView.Eye.Rotate(0f, turn * 90f * Time.unscaledDeltaTime, 0f, Space.World);
    }
#endif

    // ----------------------------------------------------------------- 舞台(砦・塔)

    void BuildFort()
    {
        var fortObject = new GameObject("Fort");
        fort = fortObject.AddComponent<Fort>();
        fort.TorchLights = torchGlowLights;
        fort.CastShadows = castSunShadows;
        fort.Build(fortMaxHealth, fortHeightMeters);

        stage = 1;
        stronghold = fort;
        Subscribe();
    }

    void BuildTower()
    {
        var towerObject = new GameObject("Tower");
        tower = towerObject.AddComponent<Tower>();
        tower.Build(towerMaxHealth, towerHeightMeters, towerFenceRadius);

        stage = 2;
        stronghold = tower;
        Subscribe();

        // 全方向から敵が来るので、視界の外の敵を、矢印で知らせる
        if (showEnemyMarkers)
        {
            EnemyRadar.Create();
            EnemyRadar.SetVisible(true);
        }
    }

    void Subscribe()
    {
        if (stronghold == null) return;
        stronghold.Damaged += OnFortDamaged;
        stronghold.Broken += OnFortBroken;
    }

    void Unsubscribe()
    {
        if (stronghold == null) return;
        stronghold.Damaged -= OnFortDamaged;
        stronghold.Broken -= OnFortBroken;
    }

    /// <summary>塔のステージの間は true。地上の敵は、塔をぐるりと囲む柵に向かって、全方向から来る。</summary>
    bool InTower => stage == 2 && tower != null;

    // ----------------------------------------------------------------- ウェーブとステージの進行

    IEnumerator RunGame()
    {
        RefreshHud("GET READY");
        yield return new WaitForSeconds(startDelay);

        while (!gameOver)
        {
            yield return RunStage();
            if (gameOver) yield break;

            bool lastStage = stage >= (includeTowerStage ? 2 : 1);
            if (lastStage)
            {
                yield return GameClear();
                yield break;
            }

            yield return GoToTower();
        }
    }

    /// <summary>今のステージの、すべてのウェーブを進める。全部全滅させたら戻る(ゲームオーバーでも戻る)。</summary>
    IEnumerator RunStage()
    {
        stageWave = 0;
        while (stageWave < wavesToClear && !gameOver)
        {
            stageWave++;
            wave++;
            int count = firstWaveCount + (wave - 1) * countIncreasePerWave;
            int flyers = wave >= flyingStartWave ? Mathf.RoundToInt(count * flyingShare) : 0;

            if (GunSystem.Instance != null) GunSystem.Instance.OnWaveStart();
            GameAudio.PlayWaveStart();
            aliveCount = count;
            Debug.Log($"[WaveSpawner] STAGE {stage} WAVE {stageWave}/{wavesToClear}(通し番号 {wave}): 敵 {count} 体(空 {flyers} 体)、大きさ {CurrentScale() * 100f:0}%、速さ x{CurrentSpeedMultiplier():0.00}");
            RefreshHud();

            yield return SpawnWave(count, flyers);

            // 全部倒すまで待つ
            while (aliveCount > 0 && !gameOver) yield return null;
            if (gameOver) yield break;

            stronghold.Repair(repairPerWave);

            // 最後のウェーブのあとは、ステージクリアの演出(RunGame 側)に任せる
            if (stageWave < wavesToClear)
            {
                GameAudio.PlayWaveClear();
                string next = null;
                if (InTower)
                {
                    stageWave++;   // 次のウェーブの範囲を計算するため、一時的に進める
                    float nextArc = TowerArcHalfAngle();
                    stageWave--;
                    next = nextArc >= 179.5f ? "NEXT: ENEMIES FROM ALL SIDES!" : "NEXT: ENEMIES SPREAD WIDER";
                }
                RefreshHud($"WAVE {stageWave} CLEAR!", next);
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }
    }

    /// <summary>ステージ 1 をクリアしたあと、砦を片付けて、塔のステージ(ステージ 2)を始める。</summary>
    IEnumerator GoToTower()
    {
        Debug.Log($"[WaveSpawner] STAGE {stage} CLEAR! SCORE {score}。塔のステージへ進みます");
        GameAudio.PlayStageClear();
        RefreshHud($"STAGE {stage} CLEAR!", "NEXT: THE TOWER\nENEMIES ATTACK FROM ALL SIDES");
        yield return new WaitForSeconds(stageClearSeconds);
        if (gameOver) yield break;

        // 視界を黒くして、そのあいだに、砦を片付けて、塔を作る(作るあいだの、一瞬のもたつきを見せない)
        ScreenFade.Show();
        yield return new WaitForSeconds(0.4f);

        Unsubscribe();
        stronghold = null;
        if (fort != null) Destroy(fort.gameObject);
        fort = null;
        yield return null;

        BuildTower();
        stageWave = 0;
        RefreshHud("STAGE 2");
        yield return new WaitForSeconds(0.6f);

        ScreenFade.Hide();
        GameAudio.PlayWaveStart();
        RefreshHud("STAGE 2  GET READY", "THE TOWER");
        yield return new WaitForSeconds(startDelay);
    }

    IEnumerator GameClear()
    {
        Debug.Log($"[WaveSpawner] GAME CLEAR! STAGE {stage}、SCORE {score}");
        GameAudio.PlayStageClear();
        EnemyRadar.SetVisible(false);

        hud.color = new Color(1f, 0.85f, 0.3f);
        HudText.SetText(hud, $"GAME CLEAR!\nSCORE {score}\n\nHOLD BOTH TRIGGERS\nTO PLAY AGAIN");
        if (fortBarBack != null) fortBarBack.gameObject.SetActive(false);

        yield return new WaitForSeconds(1.5f);
        yield return WaitForRetry();
    }

    /// <summary>両方のトリガーを 1 秒引き続けたら、最初(ステージ 1)からやり直す。</summary>
    IEnumerator WaitForRetry()
    {
        float held = 0f;
        while (held < 1f)
        {
            bool both = GunSystem.Instance != null && GunSystem.Instance.BothTriggersHeld;
            held = both ? held + Time.deltaTime : 0f;
            yield return null;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

        // 地上の敵が並ぶ位置。砦:壁の前に横に 8 か所。塔:全方向の 24 本の道(15 度おき)。あふれたら 2 列目
        // 塔では、今のウェーブで敵が来る範囲(正面寄りから、ウェーブごとに広がる)の中の道だけを使う
        int slotCount = InTower ? Scenery.RingLanes : 8;
        float arc = InTower ? TowerArcHalfAngle() : 180f;
        var usable = new List<int>();
        for (int i = 0; i < slotCount; i++)
        {
            if (InTower && Mathf.Abs(Mathf.DeltaAngle(0f, i * 360f / slotCount)) > arc + 0.01f) continue;
            usable.Add(i);
        }
        int[] slotOrder = usable.ToArray();
        for (int i = slotOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slotOrder[i], slotOrder[j]) = (slotOrder[j], slotOrder[i]);
        }
        if (InTower) Debug.Log($"[WaveSpawner] 塔 WAVE {stageWave}: 敵が来る範囲 = 正面から左右 {arc:0} 度(使う道 {slotOrder.Length}/{slotCount} 本)");

        float interval = spawnDuration / Mathf.Max(1, count);
        int groundIndex = 0;

        for (int i = 0; i < count; i++)
        {
            if (gameOver) yield break;

            if (kinds[i] == EnemyKind.Ground)
            {
                int slot = slotOrder[groundIndex % slotOrder.Length];
                int row = groundIndex / slotOrder.Length;
                if (InTower) SpawnGroundTower(slot, slotCount, row, variants[i]);
                else SpawnGround(slot, slotCount, row, variants[i]);
                groundIndex++;
            }
            else
            {
                if (InTower) SpawnFlyingTower(arc);
                else SpawnFlying();
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

    /// <summary>塔のステージの地上の敵。slot 番目の道(全方向)を通って、外柵の外側まで歩いてくる。</summary>
    void SpawnGroundTower(int slot, int slotCount, int row, EnemyVariant variant)
    {
        EnemyStats stats = groundStats;
        if (variant == EnemyVariant.Armored) stats = armoredStats;
        else if (variant == EnemyVariant.Giant) stats = giantStats;
        else if (variant == EnemyVariant.Runner) stats = runnerStats;

        // 道の向き(正面 0 度、右が +)。少しだけばらつかせる
        float angle = slot * (360f / slotCount) + Random.Range(-2f, 2f);
        Vector3 direction = Tower.Direction(angle);
        Vector3 axis = Tower.Axis(tower.GroundY);

        float stopRadius = tower.FenceRadius + groundStopFromFence * Mathf.Max(1f, stats.sizeMultiplier) + row * 1.2f;
        Vector3 stand = axis + direction * stopRadius;
        Vector3 look = axis + direction * tower.FenceRadius + Vector3.up * 1f;
        Vector3 spawn = axis + direction * Random.Range(groundSpawnNearMeters, groundSpawnFarMeters);

        float size = CurrentScale() * stats.sizeMultiplier;
        Enemy.Spawn(EnemyKind.Ground, spawn, stand, look, size, CurrentSpeedMultiplier(), CurrentHealthMultiplier(), stats, variant);
    }

    /// <summary>塔のステージの空の敵。頂上のまわりの、今のウェーブの範囲(arcHalf は、正面から左右へ、度)のどこかへ飛んできて、羽ばたきながら襲う。</summary>
    void SpawnFlyingTower(float arcHalf)
    {
        float angle = Random.Range(-arcHalf, arcHalf);
        Vector3 direction = Tower.Direction(angle);

        float hoverRadius = Random.Range(towerFlyMinRadius, towerFlyMaxRadius);
        float hoverHeight = tower.TopY + Random.Range(0.3f, 3f);
        Vector3 stand = Tower.Axis(hoverHeight) + direction * hoverRadius;
        Vector3 look = Tower.Axis(tower.TopY - 0.3f) + direction * 0.6f;

        float distance = Random.Range(flyingSpawnNearMeters, flyingSpawnFarMeters);
        Vector3 spawn = Tower.Axis(tower.TopY + Random.Range(4f, 14f)) + direction * distance;

        Enemy.Spawn(EnemyKind.Flying, spawn, stand, look, CurrentScale(), CurrentSpeedMultiplier(), CurrentHealthMultiplier(), flyingStats);
    }

    // ----------------------------------------------------------------- 難易度

    /// <summary>
    /// 塔のステージで、今のウェーブの敵が来る範囲(正面から左右へ、度)。最初は towerOpeningHalfAngle で、
    /// ウェーブごとに均等に広がり、towerSurroundWave 番目のウェーブで 180(=全方向)になる。
    /// </summary>
    float TowerArcHalfAngle()
    {
        if (towerSurroundWave <= 1) return 180f;
        float t = Mathf.Clamp01((stageWave - 1) / (float)(towerSurroundWave - 1));
        return Mathf.Lerp(Mathf.Clamp(towerOpeningHalfAngle, 0f, 180f), 180f, t);
    }

    /// <summary>敵が来る範囲の表示用の文字("ARC 90deg" や "ALL SIDES")。</summary>
    static string ArcLabel(float halfAngle)
    {
        return halfAngle >= 179.5f ? "ALL SIDES" : $"ARC {Mathf.RoundToInt(halfAngle * 2f)}deg";
    }

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
        Debug.Log($"[WaveSpawner] GAME OVER: STAGE {stage} WAVE {stageWave}(通し番号 {wave})、SCORE {score}");
        GameAudio.StopBgm();
        GameAudio.PlayGameOver();

        foreach (var enemy in FindObjectsByType<Enemy>()) Destroy(enemy.gameObject);
        EnemyRadar.SetVisible(false);

        hud.color = new Color(1f, 0.35f, 0.3f);
        HudText.SetText(hud, $"GAME OVER\nSTAGE {stage}  WAVE {stageWave}   SCORE {score}\n\nHOLD BOTH TRIGGERS\nTO RETRY");
        if (fortBarBack != null) fortBarBack.gameObject.SetActive(false);

        yield return new WaitForSeconds(1.5f);
        yield return WaitForRetry();
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

    void RefreshHud(string title = null, string extra = null)
    {
        if (hud == null || gameOver || stronghold == null) return;

        if (title == null) title = $"STAGE {stage}  WAVE {stageWave}/{wavesToClear}";
        float ratio = stronghold.MaxHealth > 0f ? Mathf.Clamp01(stronghold.Health / stronghold.MaxHealth) : 0f;
        string fortColor = ratio <= 0.3f ? "#FF6659" : "#FFFFFF";
        string sizeText = $"ENEMY SIZE {CurrentScale() * 100f:0}%";
        if (InTower && stageWave >= 1) sizeText += $"   <color=#FFB05A>{ArcLabel(TowerArcHalfAngle())}</color>";
        string third = extra != null ? $"<color=#9FE8FF>{extra}</color>" : sizeText;
        HudText.SetText(hud,
            $"<color=#FFD24A>{title}</color>   SCORE <color=#FFFFFF>{score}</color>\n" +
            $"<color={fortColor}>{stronghold.Label} {Mathf.CeilToInt(stronghold.Health)}/{Mathf.CeilToInt(stronghold.MaxHealth)}</color>\n" +
            $"{third}{diagnostics}");

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

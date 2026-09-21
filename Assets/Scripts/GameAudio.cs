using System.Collections;
using UnityEngine;

/// <summary>
/// 効果音と BGM。音のデータ(ファイル)は使わず、起動時に、プログラムで合成して作る。
///   ・効果音:銃声(チャージで変わる)、両手撃ち、弾切れ、命中、撃破、アイテム、砦への攻撃、ウェーブの開始・クリア、ゲームオーバー
///   ・BGM:勇ましい行進曲風(ニ短調、128 BPM)。戦太鼓、小太鼓、力強い低音、ブラスのファンファーレを重ねた、約 30 秒のループ
/// 空間の音(3D)にしてあるので、敵や砦の位置から聞こえる。
/// 呼ぶ側は、GameAudio.PlayXxx(...) を呼ぶだけでよい(GameAudio がなければ、何もしない)。
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const int SourceCount = 16;

    float sfxVolume = 1f;
    float bgmVolume = 0.35f;
    bool bgmRequested;

    AudioSource[] sources;
    int nextSource;
    AudioSource bgmSource;
    AudioClip bgmClip;
    float lastFortHitTime = -10f;
    float lastHitTime = -10f;

    AudioClip shotLight, shotHeavy, shotCharged, shotZap, emptyClick, comboBlast, enemyHit, enemyKill, enemyKillBig;
    AudioClip ammoChime, gradeChime, fortHit, waveHorn, waveJingle, overDirge;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Instance = null;
    }

    /// <summary>準備する。PlayerView.Ensure() のあとに呼ぶ。</summary>
    public static void Ensure(float sfxVol, float bgmVol)
    {
        if (Instance != null)
        {
            Instance.sfxVolume = sfxVol;
            Instance.bgmVolume = bgmVol;
            return;
        }

        var go = new GameObject("GameAudio");
        Instance = go.AddComponent<GameAudio>();
        Instance.sfxVolume = sfxVol;
        Instance.bgmVolume = bgmVol;
        Instance.Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        // 音を聞く耳(AudioListener)は、頭のカメラに 1 つだけにする(Editor の固定カメラのときも、そこで聞こえるように)
        if (PlayerView.Eye != null)
        {
            foreach (var listener in FindObjectsByType<AudioListener>())
            {
                if (listener.gameObject != PlayerView.Eye.gameObject) listener.enabled = false;
            }
            if (PlayerView.Eye.GetComponent<AudioListener>() == null)
            {
                PlayerView.Eye.gameObject.AddComponent<AudioListener>();
            }
        }

        sources = new AudioSource[SourceCount];
        for (int i = 0; i < SourceCount; i++)
        {
            var child = new GameObject("Sfx" + i);
            child.transform.SetParent(transform, false);
            var s = child.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 120f;
            s.dopplerLevel = 0f;
            sources[i] = s;
        }

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;

        shotLight = Synth.Make("ShotLight", Synth.Shot(0.22f, 260f, 90f, 0.9f, 45f));
        shotHeavy = Synth.Make("ShotHeavy", Synth.Shot(0.45f, 170f, 45f, 1f, 22f));
        shotCharged = Synth.Make("ShotCharged", Synth.Charged());
        shotZap = Synth.Make("ShotZap", Synth.Zap(0.3f));
        emptyClick = Synth.Make("Empty", Synth.Click());
        comboBlast = Synth.Make("Combo", Synth.Combo());
        enemyHit = Synth.Make("Hit", Synth.Shot(0.14f, 320f, 130f, 0.7f, 60f));
        enemyKill = Synth.Make("Kill", Synth.Pop(0.35f, 720f, 120f));
        enemyKillBig = Synth.Make("KillBig", Synth.Shot(0.9f, 120f, 30f, 1f, 8f));
        ammoChime = Synth.Make("Ammo", Synth.Chime(new[] { 1318.5f, 1760f }, 0.08f, 0.3f));
        gradeChime = Synth.Make("Grade", Synth.Chime(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, 0.55f));
        fortHit = Synth.Make("FortHit", Synth.Shot(0.5f, 100f, 30f, 1.1f, 9f));
        waveHorn = Synth.Make("Horn", Synth.Horn(new[] { 146.83f, 220f, 293.66f }, new[] { 0f, 0.35f, 0.7f }, new[] { 0.35f, 0.35f, 0.9f }, 0.5f));
        waveJingle = Synth.Make("Jingle", Synth.Chime(new[] { 587.33f, 739.99f, 880f, 1174.66f }, 0.11f, 0.7f));
        overDirge = Synth.Make("Over", Synth.Horn(new[] { 293.66f, 220f, 174.61f, 146.83f }, new[] { 0f, 0.5f, 1f, 1.6f }, new[] { 0.55f, 0.55f, 0.65f, 1.4f }, 0.4f));

        StartCoroutine(BuildBgm());
    }

    /// <summary>BGM は長いので、1 フレームに 1 小節ずつ、少しずつ合成する。</summary>
    IEnumerator BuildBgm()
    {
        float[] data = Synth.BgmBuffer();
        for (int bar = 0; bar < Synth.BgmBars; bar++)
        {
            Synth.RenderBgmBar(data, bar);
            yield return null;
        }

        Synth.Compress(data);
        bgmClip = Synth.Make("Bgm", data);
        bgmSource.clip = bgmClip;
        if (bgmRequested) PlayBgm();
    }

    void PlayBgm()
    {
        if (bgmSource == null || bgmClip == null) return;
        bgmSource.volume = bgmVolume;
        if (!bgmSource.isPlaying) bgmSource.Play();
    }

    void Play(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        if (clip == null || sources == null) return;

        AudioSource s = sources[nextSource];
        nextSource = (nextSource + 1) % SourceCount;
        s.transform.position = position;
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume * sfxVolume);
        s.pitch = pitch;
        s.Play();
    }

    static Vector3 EyePosition()
    {
        return PlayerView.Eye != null ? PlayerView.Eye.position : Vector3.zero;
    }

    // ---- BGM ----

    public static void StartBgm()
    {
        if (Instance == null) return;
        Instance.bgmRequested = true;
        Instance.PlayBgm();
    }

    public static void StopBgm()
    {
        if (Instance == null) return;
        Instance.bgmRequested = false;
        if (Instance.bgmSource != null) Instance.bgmSource.Stop();
    }

    // ---- 効果音 ----

    /// <summary>銃声。チャージが大きいほど、低く、重く、派手に鳴る(満タン近くでは、爆発・光の走る音・きらめき・反響を重ねた音)。</summary>
    public static void PlayShot(Vector3 position, float charge01)
    {
        if (Instance == null) return;

        // 満タンに近い(約 1 秒以上のため撃ち):専用の派手な音
        if (charge01 >= 0.85f)
        {
            Instance.Play(Instance.shotCharged, position, 1f, Random.Range(0.97f, 1.03f));
            Instance.Play(Instance.shotZap, position, 0.5f, Random.Range(0.95f, 1.05f));
            return;
        }

        bool heavy = charge01 > 0.5f;
        float pitch = heavy
            ? Mathf.Lerp(1.05f, 0.85f, (charge01 - 0.5f) * 2f)
            : Mathf.Lerp(1.3f, 1.05f, charge01 * 2f);
        Instance.Play(heavy ? Instance.shotHeavy : Instance.shotLight, position,
            Mathf.Lerp(0.55f, 1f, charge01), pitch * Random.Range(0.96f, 1.04f));

        // 中くらいのため撃ちから、光の走る音を少しずつ足す
        if (heavy)
        {
            float amount = Mathf.InverseLerp(0.5f, 0.85f, charge01);
            Instance.Play(Instance.shotZap, position, 0.5f * amount, 1.1f);
        }
    }

    /// <summary>ため撃ちが着弾したときの、重い衝撃音。弱いため撃ちでは鳴らさない。</summary>
    public static void PlayImpact(Vector3 position, float big01)
    {
        if (Instance == null || big01 < 0.3f) return;
        Instance.Play(Instance.fortHit, position, 0.35f + 0.5f * big01, Random.Range(1.4f, 1.7f));
    }

    public static void PlayCombo(Vector3 position)
    {
        if (Instance == null) return;
        Instance.Play(Instance.comboBlast, position, 1f, 1f);
    }

    public static void PlayEmpty(Vector3 position)
    {
        if (Instance == null) return;
        Instance.Play(Instance.emptyClick, position, 0.6f, 1f);
    }

    /// <summary>敵に当たったが、まだ倒れていないとき。</summary>
    public static void PlayHit(Vector3 position)
    {
        if (Instance == null) return;
        if (Time.time - Instance.lastHitTime < 0.04f) return;
        Instance.lastHitTime = Time.time;
        Instance.Play(Instance.enemyHit, position, 0.6f, Random.Range(0.9f, 1.15f));
    }

    public static void PlayKill(Vector3 position, bool big)
    {
        if (Instance == null) return;
        Instance.Play(big ? Instance.enemyKillBig : Instance.enemyKill, position, big ? 1f : 0.9f, Random.Range(0.95f, 1.08f));
    }

    public static void PlayPickup(bool gradeUp)
    {
        if (Instance == null) return;
        Instance.Play(gradeUp ? Instance.gradeChime : Instance.ammoChime, EyePosition(), gradeUp ? 0.8f : 0.7f, 1f);
    }

    public static void PlayFortHit(Vector3 position)
    {
        if (Instance == null) return;
        if (Time.time - Instance.lastFortHitTime < 0.15f) return;
        Instance.lastFortHitTime = Time.time;
        Instance.Play(Instance.fortHit, position, 0.9f, Random.Range(0.9f, 1.1f));
    }

    public static void PlayWaveStart()
    {
        if (Instance == null) return;
        Instance.Play(Instance.waveHorn, EyePosition(), 0.8f, 1f);
    }

    public static void PlayWaveClear()
    {
        if (Instance == null) return;
        Instance.Play(Instance.waveJingle, EyePosition(), 0.8f, 1f);
    }

    public static void PlayGameOver()
    {
        if (Instance == null) return;
        Instance.Play(Instance.overDirge, EyePosition(), 0.9f, 1f);
    }
}

/// <summary>音を、数式で合成する道具。1 チャンネル、22050Hz。</summary>
static class Synth
{
    const int Rate = 22050;
    const float TwoPi = 6.2831853f;

    static readonly System.Random Rng = new System.Random(20260921);

    static float Noise()
    {
        return (float)(Rng.NextDouble() * 2.0 - 1.0);
    }

    static float[] Buffer(float seconds)
    {
        return new float[Mathf.CeilToInt(seconds * Rate)];
    }

    /// <summary>最大の大きさを peak にそろえて、AudioClip にする。</summary>
    public static AudioClip Make(string clipName, float[] data, float peak = 0.9f)
    {
        float max = 0.0001f;
        for (int i = 0; i < data.Length; i++)
        {
            float a = Mathf.Abs(data[i]);
            if (a > max) max = a;
        }
        float scale = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= scale;

        var clip = AudioClip.Create(clipName, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>末尾の 8ms で音を絞って、ぷつっという雑音を防ぐ。</summary>
    static void FadeTail(float[] d)
    {
        int n = Mathf.Min(d.Length, Mathf.RoundToInt(0.008f * Rate));
        for (int i = 0; i < n; i++) d[d.Length - 1 - i] *= i / (float)n;
    }

    // ---- 効果音の部品 ----

    /// <summary>低い「ドン」(音程が下がる)と、破裂の雑音を合わせた音。銃声、命中、砦への攻撃に使う。</summary>
    public static float[] Shot(float seconds, float freqStart, float freqEnd, float noiseAmount, float noiseDecay)
    {
        float[] d = Buffer(seconds);
        float phase = 0f;
        float lowpass = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / seconds;
            float freq = Mathf.Lerp(freqStart, freqEnd, Mathf.Sqrt(k));
            phase += TwoPi * freq / Rate;
            float body = Mathf.Sin(phase) * Mathf.Exp(-t * (6f + 8f / Mathf.Max(0.1f, seconds)));
            lowpass += (Noise() - lowpass) * 0.35f;
            float crack = lowpass * Mathf.Exp(-t * noiseDecay) * noiseAmount;
            d[i] = body * 0.8f + crack;
        }
        FadeTail(d);
        return d;
    }

    /// <summary>敵を倒した「ポン」。音程が下がる音と、短い雑音。</summary>
    public static float[] Pop(float seconds, float freqStart, float freqEnd)
    {
        float[] d = Buffer(seconds);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float freq = Mathf.Lerp(freqStart, freqEnd, Mathf.Sqrt(t / seconds));
            phase += TwoPi * freq / Rate;
            float tone = Mathf.Sin(phase) * Mathf.Exp(-t * 10f);
            float burst = Noise() * Mathf.Exp(-t * 60f) * 0.5f;
            d[i] = tone * 0.8f + burst;
        }
        FadeTail(d);
        return d;
    }

    /// <summary>弾切れの「カチッ」。</summary>
    public static float[] Click()
    {
        float[] d = Buffer(0.09f);
        float prev = 0f;
        foreach (float start in new[] { 0f, 0.035f })
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            for (int i = 0; s0 + i < d.Length && i < Mathf.RoundToInt(0.012f * Rate); i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                d[s0 + i] += (n - prev) * Mathf.Exp(-t * 400f);
                prev = n;
            }
        }
        FadeTail(d);
        return d;
    }

    /// <summary>両手撃ち。重い銃声に、上から下へ走る光の音を重ねる。</summary>
    public static float[] Combo()
    {
        float[] d = Shot(0.7f, 240f, 40f, 1f, 18f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            if (t > 0.3f) break;
            float freq = Mathf.Lerp(1400f, 180f, t / 0.3f);
            phase += TwoPi * freq / Rate;
            d[i] += Mathf.Sin(phase) * 0.45f * Mathf.Exp(-t * 9f);
        }
        FadeTail(d);
        return d;
    }

    /// <summary>鈴のような音を、gap 秒ずらして順に鳴らす(アイテム、クリアの音)。</summary>
    public static float[] Chime(float[] freqs, float gap, float tail)
    {
        float[] d = Buffer(gap * (freqs.Length - 1) + tail);
        for (int n = 0; n < freqs.Length; n++)
        {
            int s0 = Mathf.RoundToInt(n * gap * Rate);
            float f = freqs[n];
            for (int i = 0; s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 6f);
                float v = Mathf.Sin(TwoPi * f * t)
                          + 0.4f * Mathf.Sin(TwoPi * f * 2f * t) * Mathf.Exp(-t * 6f)
                          + 0.2f * Mathf.Sin(TwoPi * f * 3.01f * t) * Mathf.Exp(-t * 10f);
                d[s0 + i] += v * env * 0.5f;
            }
        }
        FadeTail(d);
        return d;
    }

    /// <summary>角笛(ホルン)のような音を、順に鳴らす(ウェーブ開始、ゲームオーバー)。</summary>
    public static float[] Horn(float[] freqs, float[] starts, float[] durations, float amp)
    {
        float total = 0f;
        for (int n = 0; n < freqs.Length; n++) total = Mathf.Max(total, starts[n] + durations[n]);
        float[] d = Buffer(total + 0.2f);

        for (int n = 0; n < freqs.Length; n++)
        {
            int s0 = Mathf.RoundToInt(starts[n] * Rate);
            int len = Mathf.RoundToInt(durations[n] * Rate);
            float f = freqs[n];
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float attack = Mathf.Min(1f, t / 0.08f);
                float release = Mathf.Min(1f, (durations[n] - t) / 0.15f);
                float vib = 1f + 0.004f * Mathf.Sin(TwoPi * 5.5f * t);
                float v = 0f;
                for (int h = 1; h <= 6; h++) v += Mathf.Sin(TwoPi * f * vib * h * t) / h;
                d[s0 + i] += v * attack * Mathf.Max(0f, release) * amp;
            }
        }
        FadeTail(d);
        return d;
    }

    // ---- 効果音の部品(ため撃ち用) ----

    /// <summary>光が走る「ビュン」。音程が一気に下がる。</summary>
    public static float[] Zap(float seconds)
    {
        float[] d = Buffer(seconds);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / seconds;
            float freq = Mathf.Lerp(3200f, 240f, Mathf.Pow(k, 0.35f));
            phase += TwoPi * freq / Rate;
            float v = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2.01f);
            d[i] = v * Mathf.Exp(-t * 7f) * Mathf.Min(1f, t * 400f);
        }
        FadeTail(d);
        return d;
    }

    /// <summary>満タンのため撃ち。重い爆発、光の走る音、きらめき、反響を重ねる。</summary>
    public static float[] Charged()
    {
        float[] shot = Shot(1.1f, 200f, 26f, 1f, 10f);
        float[] d = new float[shot.Length + Mathf.RoundToInt(0.5f * Rate)];
        System.Array.Copy(shot, d, shot.Length);

        // 光の走る音
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            if (t > 0.35f) break;
            float freq = Mathf.Lerp(3400f, 180f, Mathf.Sqrt(t / 0.35f));
            phase += TwoPi * freq / Rate;
            d[i] += Mathf.Sin(phase) * 0.5f * Mathf.Exp(-t * 8f);
        }

        // きらめき(高い鈴の音を、少しずつずらして)
        float[] sparkle = { 1568f, 2093f, 2637f, 3136f, 3951f };
        for (int n = 0; n < sparkle.Length; n++)
        {
            int s0 = Mathf.RoundToInt((0.05f + 0.06f * n) * Rate);
            for (int i = 0; s0 + i < d.Length && i < Mathf.RoundToInt(0.5f * Rate); i++)
            {
                float t = i / (float)Rate;
                d[s0 + i] += Mathf.Sin(TwoPi * sparkle[n] * t) * Mathf.Exp(-t * 9f) * 0.16f;
            }
        }

        // 反響(3 回)
        float[] lags = { 0.11f, 0.23f, 0.37f };
        float[] gains = { 0.45f, 0.28f, 0.16f };
        for (int e = 0; e < lags.Length; e++)
        {
            int lag = Mathf.RoundToInt(lags[e] * Rate);
            for (int i = d.Length - 1; i >= lag; i--) d[i] += d[i - lag] * gains[e];
        }

        FadeTail(d);
        return d;
    }

    // ---- BGM(勇ましい行進曲風。ニ短調、128 BPM、16 小節 = 約 30 秒) ----

    const float Bpm = 128f;
    const float Beat = 60f / Bpm;
    const float Step = Beat / 4f;   // 16 分音符
    const float BarSeconds = Beat * 4f;
    public const int BgmBars = 16;

    // 和音の根音。4 小節ごとに繰り返す:Dm → B♭ → C → Dm
    static readonly float[] BassHz = { 73.42f, 58.27f, 65.41f, 73.42f };

    // ブラスのファンファーレ。1 小節ぶんを { 開始(16 分音符の何個目か), 音の高さ(MIDI 番号), 長さ(16 分音符いくつ分か) } の並びで書く。
    // 「タタタ・ターン」と跳ね上がる、勇ましい動き。8 小節で 1 周し、後半の 8 小節は同じ旋律に和音を重ねる。
    static readonly int[][] Fanfare =
    {
        new[] { 0, 62, 3, 3, 62, 1, 4, 69, 6, 10, 67, 2, 12, 65, 2, 14, 67, 2 },
        new[] { 0, 70, 3, 3, 70, 1, 4, 74, 6, 10, 72, 2, 12, 70, 2, 14, 72, 2 },
        new[] { 0, 72, 3, 3, 72, 1, 4, 76, 6, 10, 74, 2, 12, 72, 2, 14, 74, 2 },
        new[] { 0, 74, 4, 4, 69, 4, 8, 65, 4, 12, 62, 4 },
        new[] { 0, 69, 3, 3, 69, 1, 4, 77, 6, 10, 76, 2, 12, 74, 2, 14, 72, 2 },
        new[] { 0, 74, 3, 3, 74, 1, 4, 77, 6, 10, 74, 2, 12, 72, 2, 14, 70, 2 },
        new[] { 0, 72, 3, 3, 72, 1, 4, 76, 4, 8, 79, 4, 12, 76, 4 },
        new[] { 0, 74, 6, 6, 72, 2, 8, 74, 4, 12, 77, 4 },
    };

    public static float[] BgmBuffer()
    {
        return new float[Mathf.RoundToInt(BarSeconds * BgmBars * Rate)];
    }

    /// <summary>全部の小節を作り終えたあと、軽くつぶして(コンプレッサー)、音の迫力を上げる。</summary>
    public static void Compress(float[] d)
    {
        float max = 0.0001f;
        for (int i = 0; i < d.Length; i++)
        {
            float a = Mathf.Abs(d[i]);
            if (a > max) max = a;
        }

        const float drive = 1.8f;
        float norm = (float)System.Math.Tanh(drive);
        for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] / max * drive) / norm;
    }

    /// <summary>1 小節ぶんの BGM を、d に足し込む。曲の終わりを越える音は、先頭へ回り込ませる(つなぎ目のない繰り返しのため)。</summary>
    public static void RenderBgmBar(float[] d, int bar)
    {
        bool second = bar >= 8;
        float t0 = bar * BarSeconds;
        float root = BassHz[bar % 4];

        // 低音:8 分音符で、ぐいぐい押す(1 拍目と 3 拍目を強く、間でオクターブ上へ跳ねる)
        for (int e = 0; e < 8; e++)
        {
            float hz = (e == 2 || e == 6) ? root * 2f : root;
            float amp = (e == 0 || e == 4) ? 0.42f : 0.30f;
            AddBass(d, t0 + e * Step * 2f, hz, Step * 3.2f, amp);
        }

        // 和音の下地(力強い 5 度)
        AddSine(d, t0, root * 2f, BarSeconds * 1.02f, 0.06f, true);
        AddSine(d, t0, root * 3f, BarSeconds * 1.02f, 0.05f, true);
        AddSine(d, t0, root * 4f, BarSeconds * 1.02f, 0.04f, true);

        // 太鼓(戦太鼓):1 拍目と 3 拍目にドーン、間に小さく
        AddTaiko(d, t0, 0.9f);
        AddTaiko(d, t0 + Step * 3f, 0.45f);
        AddTaiko(d, t0 + Step * 8f, 0.8f);
        AddTaiko(d, t0 + Step * 11f, 0.45f);
        AddTaiko(d, t0 + Step * 14f, 0.5f);
        if (second) AddTaiko(d, t0 + Step * 6f, 0.4f);

        // 小太鼓:2 拍目と 4 拍目。4 小節ごとの最後は、連打で盛り上げる
        if (bar % 4 == 3)
        {
            AddSnare(d, t0 + Step * 4f, 0.5f);
            for (int k = 0; k < 4; k++) AddSnare(d, t0 + Step * (12 + k), 0.25f + 0.1f * k);
        }
        else
        {
            AddSnare(d, t0 + Step * 4f, 0.5f);
            AddSnare(d, t0 + Step * 12f, 0.5f);
        }

        // シンバル:4 小節の頭
        if (bar % 4 == 0) AddCrash(d, t0, second ? 0.22f : 0.15f);

        // 細かい刻み(後半は 8 分音符ぜんぶ)
        for (int e = second ? 0 : 1; e < 8; e += second ? 1 : 2) AddTick(d, t0 + e * Step * 2f, 0.05f);

        // ブラスのファンファーレ(1 オクターブ下を重ねて厚くする。後半は、5 度下も重ねる)
        int[] notes = Fanfare[bar % 8];
        for (int i = 0; i + 2 < notes.Length; i += 3)
        {
            float ts = t0 + notes[i] * Step;
            float len = notes[i + 2] * Step;
            int midi = notes[i + 1];
            AddBrass(d, ts, midi, len, 0.20f);
            AddBrass(d, ts, midi - 12, len, 0.09f);
            if (second) AddBrass(d, ts, midi - 7, len, 0.12f);
        }
    }

    static void AddSine(float[] d, float startSec, float freq, float dur, float amp, bool pad)
    {
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.RoundToInt(dur * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = pad
                ? Mathf.Min(1f, t / 0.4f) * Mathf.Clamp01((dur - t) / 0.4f)
                : Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 2.2f);
            d[(s0 + i) % d.Length] += Mathf.Sin(TwoPi * freq * t) * env * amp;
        }
    }

    /// <summary>低音。基音に倍音を重ねて、太く、短く切る。</summary>
    static void AddBass(float[] d, float startSec, float freq, float dur, float amp)
    {
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.RoundToInt(dur * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01((dur - t) / 0.03f);
            float w = TwoPi * freq * t;
            float v = Mathf.Sin(w) + 0.5f * Mathf.Sin(w * 2f) + 0.3f * Mathf.Sin(w * 3f) + 0.15f * Mathf.Sin(w * 4f);
            d[(s0 + i) % d.Length] += v * env * amp * 0.6f;
        }
    }

    /// <summary>戦太鼓。低く、長く響く。</summary>
    static void AddTaiko(float[] d, float startSec, float amp)
    {
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.RoundToInt(0.6f * Rate);
        float phase = 0f;
        float lowpass = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float freq = 48f + 90f * Mathf.Exp(-t * 22f);
            phase += TwoPi * freq / Rate;
            lowpass += (Noise() - lowpass) * 0.25f;
            float v = Mathf.Sin(phase) * Mathf.Exp(-t * 7f) + lowpass * 0.4f * Mathf.Exp(-t * 80f);
            d[(s0 + i) % d.Length] += v * amp;
        }
    }

    /// <summary>小太鼓。雑音と、短い胴の音。</summary>
    static void AddSnare(float[] d, float startSec, float amp)
    {
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.RoundToInt(0.2f * Rate);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = Noise();
            float hp = x - prev;
            prev = x;
            float v = hp * 0.5f * Mathf.Exp(-t * 20f) + Mathf.Sin(TwoPi * 185f * t) * 0.4f * Mathf.Exp(-t * 28f);
            d[(s0 + i) % d.Length] += v * amp;
        }
    }

    /// <summary>シンバル。高い雑音が、ゆっくり消える。</summary>
    static void AddCrash(float[] d, float startSec, float amp)
    {
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.RoundToInt(1.4f * Rate);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = Noise();
            float hp = x - prev;
            prev = x;
            d[(s0 + i) % d.Length] += hp * Mathf.Exp(-t * 3.2f) * Mathf.Min(1f, t / 0.004f) * amp;
        }
    }

    static void AddTick(float[] d, float startSec, float amp)
    {
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.RoundToInt(0.03f * Rate);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = Noise();
            d[(s0 + i) % d.Length] += (x - prev) * Mathf.Exp(-t * 150f) * amp;
            prev = x;
        }
    }

    // ブラス(金管)の音。音の高さごとに、1 度だけ作って使い回す(合成が重いため)
    static readonly System.Collections.Generic.Dictionary<int, float[]> BrassCache =
        new System.Collections.Generic.Dictionary<int, float[]>();

    /// <summary>ブラスの音色。倍音をたくさん重ね、出だしは少し暗く、すぐ明るくなる。少し遅れて、ゆれ(ビブラート)が入る。</summary>
    static float[] BrassTone(int midi)
    {
        float[] tone;
        if (BrassCache.TryGetValue(midi, out tone)) return tone;

        float freq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        tone = Buffer(0.8f);
        float phase = 0f;
        for (int i = 0; i < tone.Length; i++)
        {
            float t = i / (float)Rate;
            float vib = 1f + 0.006f * Mathf.Sin(TwoPi * 5.2f * t) * Mathf.Clamp01((t - 0.12f) / 0.2f);
            phase += TwoPi * freq * vib / Rate;
            float bright = 0.35f + 0.65f * Mathf.Clamp01(t / 0.08f);
            float sb = Mathf.Sqrt(bright);
            float w = 1f;
            float v = 0f;
            for (int h = 1; h <= 7; h++)
            {
                w *= sb;
                v += Mathf.Sin(phase * h) / h * (h == 1 ? 1f : w);
            }
            tone[i] = v * Mathf.Min(1f, t / 0.02f);
        }

        BrassCache[midi] = tone;
        return tone;
    }

    static void AddBrass(float[] d, float startSec, int midi, float lenSec, float amp)
    {
        float[] tone = BrassTone(midi);
        int s0 = Mathf.RoundToInt(startSec * Rate);
        int n = Mathf.Min(tone.Length, Mathf.RoundToInt(lenSec * Rate));
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float release = Mathf.Clamp01((lenSec - t) / 0.04f);
            d[(s0 + i) % d.Length] += tone[i] * release * amp;
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatAudio : MonoBehaviour
{
    private const string ResourceRoot = "Audio/SFX/Combat/";
    private const string MusicResourceRoot = "Audio/Music/";

    private static CombatAudio instance;

    private AudioSource impactSource;
    private AudioSource actionSource;
    private AudioSource interfaceSource;
    private AudioSource musicSource;

    private AudioClip parryImpact;
    private AudioClip warningYellow;
    private AudioClip warningRed;
    private AudioClip heavyHit;
    private AudioClip switchIn;
    private AudioClip energyReady;
    private AudioClip[] lightHits;
    private AudioClip[] lightSwings;
    private AudioClip heavySwing;
    private AudioClip[] enemySwings;
    private AudioClip battleMusic;

    private int nextLightHitIndex;
    private int nextLightSwingIndex;
    private int nextEnemySwingIndex;
    private float lastHitTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static void EnsureInitialized()
    {
        Resolve();
    }

    public static void PlayHit(float intensity)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        // 여러 콜라이더가 같은 프레임에 겹쳐도 타격음이 과도하게 중첩되지 않게 막는다.
        if (Time.unscaledTime - audio.lastHitTime < 0.025f)
            return;

        audio.lastHitTime = Time.unscaledTime;

        if (intensity >= 1.25f && audio.heavyHit != null)
        {
            audio.impactSource.PlayOneShot(audio.heavyHit, 0.58f);
            return;
        }

        AudioClip clip = NextClip(audio.lightHits, ref audio.nextLightHitIndex);
        if (clip != null)
            audio.impactSource.PlayOneShot(clip, 0.40f);
    }

    public static void PlayAttackSwing(float intensity = 1f)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        if (intensity >= 1.25f && audio.heavySwing != null)
        {
            audio.actionSource.PlayOneShot(audio.heavySwing, 0.52f);
            return;
        }

        AudioClip clip = NextClip(audio.lightSwings, ref audio.nextLightSwingIndex);
        if (clip != null)
            audio.actionSource.PlayOneShot(clip, 0.42f);
    }

    public static void PlayEnemyAttackSwing()
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        AudioClip clip = NextClip(audio.enemySwings, ref audio.nextEnemySwingIndex);
        if (clip != null)
            audio.actionSource.PlayOneShot(clip, 0.50f);
    }

    public static void PlayParry()
    {
        CombatAudio audio = Resolve();
        if (audio?.parryImpact != null)
            audio.impactSource.PlayOneShot(audio.parryImpact, 0.88f);
    }

    public static void PlayWarning(WarningType warningType)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        AudioClip clip = warningType == WarningType.Yellow
            ? audio.warningYellow
            : audio.warningRed;

        if (clip != null)
            audio.interfaceSource.PlayOneShot(clip, warningType == WarningType.Yellow ? 0.48f : 0.42f);
    }

    public static void PlaySwitch()
    {
        CombatAudio audio = Resolve();
        if (audio?.switchIn != null)
            audio.interfaceSource.PlayOneShot(audio.switchIn, 0.38f);
    }

    public static void PlayEnergyReady()
    {
        CombatAudio audio = Resolve();
        if (audio?.energyReady != null)
            audio.interfaceSource.PlayOneShot(audio.energyReady, 0.36f);
    }

    private static CombatAudio Resolve()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<CombatAudio>();
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Combat Audio (Runtime)");
        instance = root.AddComponent<CombatAudio>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        impactSource = CreateSource("Impact Source", 0);
        actionSource = CreateSource("Action Source", 16);
        interfaceSource = CreateSource("Interface Source", 32);
        musicSource = CreateSource("Music Source", 128);
        musicSource.ignoreListenerPause = false;
        musicSource.loop = true;
        musicSource.volume = 0.28f;

        LoadClips();
        StartBattleMusic();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private AudioSource CreateSource(string objectName, int priority)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = priority;
        source.ignoreListenerPause = true;
        return source;
    }

    private void LoadClips()
    {
        parryImpact = Load("parry_impact_01");
        warningYellow = Load("warning_yellow_01");
        warningRed = Load("warning_red_01");
        heavyHit = Load("hit_heavy_01");
        heavySwing = Load("swing_heavy_01");
        switchIn = Load("switch_in_01");
        energyReady = Load("energy_ready_01");

        lightHits = new[]
        {
            Load("hit_light_01"),
            Load("hit_light_02"),
            Load("hit_light_03")
        };

        lightSwings = new[]
        {
            Load("swing_light_01"),
            Load("swing_light_02"),
            Load("swing_light_03")
        };

        enemySwings = new[]
        {
            Load("enemy_swing_01"),
            Load("enemy_swing_02")
        };

        battleMusic = Resources.Load<AudioClip>(MusicResourceRoot + "battle_01");
        if (battleMusic == null)
            Debug.LogWarning($"CombatAudio: Resources/{MusicResourceRoot}battle_01 음원을 찾을 수 없습니다.");
    }

    private void StartBattleMusic()
    {
        if (battleMusic == null || musicSource.isPlaying)
            return;

        musicSource.clip = battleMusic;
        musicSource.Play();
    }

    private static AudioClip NextClip(AudioClip[] clips, ref int index)
    {
        if (clips == null || clips.Length == 0)
            return null;

        AudioClip clip = clips[index];
        index = (index + 1) % clips.Length;
        return clip;
    }

    private static AudioClip Load(string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>(ResourceRoot + clipName);
        if (clip == null)
            Debug.LogWarning($"CombatAudio: Resources/{ResourceRoot}{clipName} 음원을 찾을 수 없습니다.");
        return clip;
    }
}
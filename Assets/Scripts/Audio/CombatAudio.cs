using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 SFX와 배경 음악을 런타임 AudioSource로 구성하고 중복 적중음을 제한한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatAudio : MonoBehaviour
{
    private const string ResourceRoot = "Audio/SFX/Combat/";
    private const string MusicResourceRoot = "Audio/Music/";

    [SerializeField, Tooltip("포트폴리오 영상 촬영용 전체 음소거. 음원과 호출 구조는 유지한다.")]
    private bool masterMuted;

    private sealed class CharacterClipSet
    {
        public AudioClip[] normalAttacks;
        public AudioClip[] skills;
        public AudioClip skillEnd;
        public AudioClip ultimateSequence;
        public AudioClip[] ultimates;
    }

    private static CombatAudio instance;

    private AudioSource impactSource;
    private AudioSource actionSource;
    private AudioSource interfaceSource;
    private AudioSource musicIntroSource;
    private AudioSource musicSource;

    private AudioClip parryImpact;
    private AudioClip warningYellow;
    private AudioClip warningRed;
    private AudioClip perfectDodge;
    private AudioClip heavyHit;
    private AudioClip[] playerHits;
    private AudioClip[] lightHits;
    private AudioClip[] lightSwings;
    private AudioClip heavySwing;
    private AudioClip[] enemySwings;
    private AudioClip battleIntro;
    private AudioClip battleLoop;
    private readonly Dictionary<string, CharacterClipSet> characterClipSets = new();
    private readonly Dictionary<string, AudioClip> enemyAttackClips = new();

    private int nextLightHitIndex;
    private int nextLightSwingIndex;
    private int nextEnemySwingIndex;
    private int nextPlayerHitIndex;
    private float lastHitTime = float.NegativeInfinity;

    private bool cinematicPlaybackActive;
    private bool previousImpactMute;
    private bool previousActionMute;
    private bool previousInterfaceMute;
    private bool previousMusicMute;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static void EnsureInitialized()
    {
        Resolve();
    }

    public static bool IsMasterMuted => Resolve()?.masterMuted ?? true;

    public static void SetMasterMuted(bool muted)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        audio.masterMuted = muted;
        audio.ApplyMasterMuteState();

        if (!muted)
            audio.StartBattleMusic();
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

    public static void PlayNormalAttack(
        PlayerController player,
        int comboIndex,
        float intensity = 1f)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        CharacterClipSet set = audio.ResolveCharacterClips(player);
        AudioClip clip = ClipAt(set?.normalAttacks, comboIndex);
        if (clip != null)
        {
            audio.actionSource.PlayOneShot(clip, intensity >= 1.25f ? 0.64f : 0.54f);
            return;
        }

        PlayAttackSwing(intensity);
    }

    public static void PlaySkill(
        PlayerController player,
        bool enhanced,
        float intensity = 1f)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        CharacterClipSet set = audio.ResolveCharacterClips(player);
        AudioClip clip = ClipAt(set?.skills, enhanced ? 1 : 0);
        if (clip != null)
        {
            audio.actionSource.PlayOneShot(clip, enhanced ? 0.68f : 0.58f);
            return;
        }

        PlayAttackSwing(Mathf.Max(intensity, enhanced ? 1.3f : 1f));
    }

    /// <summary>
    /// 캐릭터 전용 특수 스킬의 마지막 타격음을 재생한다.
    /// 전용 음원이 없는 캐릭터는 아무 소리도 추가하지 않는다.
    /// </summary>
    public static void PlaySkillEnd(PlayerController player)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        AudioClip clip = audio.ResolveCharacterClips(player)?.skillEnd;
        if (clip != null)
            audio.actionSource.PlayOneShot(clip, 0.68f);
    }

    public static void PlayUltimate(
        PlayerController player,
        int hitWindowIndex,
        bool isChainSkill)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        CharacterClipSet set = audio.ResolveCharacterClips(player);

        // 엘렌처럼 완성된 궁극기 시퀀스음이 있으면 첫 타격에서 한 번만 재생한다.
        // 이후 윈도우의 충돌감은 공용 타격음이 담당하므로 긴 클립이 중첩되지 않는다.
        if (!isChainSkill && set?.ultimateSequence != null)
        {
            if (hitWindowIndex == 0)
                audio.actionSource.PlayOneShot(set.ultimateSequence, 0.72f);

            return;
        }

        // 콤보 스킬은 캐릭터의 평타음을 재사용하고 실제 궁극기만 전용 음원을 사용한다.
        AudioClip[] clips = isChainSkill ? set?.normalAttacks : set?.ultimates;
        AudioClip clip = ClipAt(clips, hitWindowIndex);
        if (clip != null)
        {
            audio.actionSource.PlayOneShot(clip, 0.72f);
            return;
        }

        PlayAttackSwing(1.35f);
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

    /// <summary>
    /// EnemyAttackData에 기록된 Resources 음원을 이름으로 찾아 재생한다.
    /// 한 번 찾은 음원은 캐시에 보관해 다단 공격 중 반복 로드를 피한다.
    /// </summary>
    public static void PlayEnemyAttackSound(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        if (!audio.enemyAttackClips.TryGetValue(clipName, out AudioClip clip))
        {
            clip = Load(clipName);
            audio.enemyAttackClips[clipName] = clip;
        }

        if (clip != null)
            audio.actionSource.PlayOneShot(clip, 0.70f);
    }

    public static void PlayParry()
    {
        CombatAudio audio = Resolve();
        if (audio?.parryImpact != null)
            audio.impactSource.PlayOneShot(audio.parryImpact, 0.88f);
    }

    public static void PlayPerfectDodge()
    {
        CombatAudio audio = Resolve();
        if (audio?.perfectDodge != null)
            audio.interfaceSource.PlayOneShot(audio.perfectDodge, 1f);
    }

    public static void PlayPlayerHit(float intensity)
    {
        CombatAudio audio = Resolve();
        if (audio == null)
            return;

        AudioClip clip = NextClip(audio.playerHits, ref audio.nextPlayerHitIndex);
        if (clip != null)
            audio.impactSource.PlayOneShot(clip, intensity >= 1.25f ? 0.76f : 0.64f);
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
            audio.interfaceSource.PlayOneShot(clip, warningType == WarningType.Yellow ? 0.62f : 0.58f);
    }

    public static void SetCinematicPlaybackActive(bool active)
    {
        CombatAudio audio = Resolve();
        if (audio != null)
            audio.ApplyCinematicMute(active);
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
        musicIntroSource = CreateSource("Music Intro Source", 128);
        musicSource = CreateSource("Music Loop Source", 128);
        musicIntroSource.ignoreListenerPause = false;
        musicIntroSource.loop = false;
        musicIntroSource.volume = 0.24f;
        musicSource.ignoreListenerPause = false;
        musicSource.loop = true;
        musicSource.volume = 0.24f;

        ApplyMasterMuteState();

        LoadClips();
        StartBattleMusic();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void ApplyCinematicMute(bool active)
    {
        if (impactSource == null ||
            actionSource == null ||
            interfaceSource == null ||
            musicIntroSource == null ||
            musicSource == null ||
            cinematicPlaybackActive == active)
        {
            return;
        }

        cinematicPlaybackActive = active;

        if (active)
        {
            previousImpactMute = impactSource.mute;
            previousActionMute = actionSource.mute;
            previousInterfaceMute = interfaceSource.mute;
            previousMusicMute = musicSource.mute;

            impactSource.mute = true;
            actionSource.mute = true;
            interfaceSource.mute = true;
            musicIntroSource.mute = true;
            musicSource.mute = true;
            return;
        }

        impactSource.mute = previousImpactMute;
        actionSource.mute = previousActionMute;
        interfaceSource.mute = previousInterfaceMute;
        musicIntroSource.mute = previousMusicMute;
        musicSource.mute = previousMusicMute;
    }

    private void ApplyMasterMuteState()
    {
        if (impactSource == null ||
            actionSource == null ||
            interfaceSource == null ||
            musicIntroSource == null ||
            musicSource == null)
        {
            return;
        }

        bool shouldMute = masterMuted || cinematicPlaybackActive;
        impactSource.mute = shouldMute;
        actionSource.mute = shouldMute;
        interfaceSource.mute = shouldMute;
        musicIntroSource.mute = shouldMute;
        musicSource.mute = shouldMute;
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
        perfectDodge = Load("perfect_dodge_01");
        heavyHit = Load("hit_heavy_01");
        heavySwing = Load("swing_heavy_01");

        playerHits = LoadMany(
            "player_hit_01",
            "player_hit_02",
            "player_hit_03");

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

        PreloadEnemyAttackClips(
            "butcher_attack_01",
            "butcher_attack_02",
            "butcher_attack_04",
            "butcher_attack_06",
            "butcher_attack_07",
            "butcher_attack_08");

        characterClipSets["Ellen_Joe"] = new CharacterClipSet
        {
            normalAttacks = LoadMany("ellen_normal_01", "ellen_normal_02", "ellen_normal_03"),
            skills = LoadMany("ellen_skill_01", "ellen_skill_02"),
            skillEnd = Load("ellen_skill_end_01"),
            ultimateSequence = Load("ellen_ultimate_sequence_01"),
            ultimates = LoadMany("ellen_ultimate_01", "ellen_ultimate_02", "ellen_ultimate_03")
        };

        characterClipSets["Jane_Doe"] = new CharacterClipSet
        {
            normalAttacks = LoadMany("jane_normal_01", "jane_normal_02", "jane_normal_03"),
            skills = LoadMany("jane_skill_01", "jane_skill_02"),
            ultimateSequence = Load("jane_ultimate_sequence_01"),
            ultimates = LoadMany("jane_ultimate_01", "jane_ultimate_02", "jane_ultimate_03")
        };

        characterClipSets["Corin"] = new CharacterClipSet
        {
            normalAttacks = LoadMany("corin_normal_01", "corin_normal_02", "corin_normal_03"),
            skills = LoadMany("corin_skill_01", "corin_skill_02"),
            ultimates = LoadMany("corin_ultimate_01", "corin_ultimate_02", "corin_ultimate_03")
        };

        battleIntro = Resources.Load<AudioClip>(MusicResourceRoot + "battle_intro_01");
        battleLoop = Resources.Load<AudioClip>(MusicResourceRoot + "battle_loop_01");

        if (battleLoop == null)
        {
            battleLoop = Resources.Load<AudioClip>(MusicResourceRoot + "battle_01");
            Debug.LogWarning(
                $"CombatAudio: Resources/{MusicResourceRoot}battle_loop_01 음원을 찾지 못해 battle_01을 사용합니다.");
        }
    }

    private void StartBattleMusic()
    {
        if (masterMuted || battleLoop == null ||
            musicIntroSource.isPlaying || musicSource.isPlaying)
            return;

        if (battleIntro == null)
        {
            PlayBattleLoop();
            return;
        }

        // 원본 Wwise Music Sequence처럼 인트로와 루프를 DSP 시간에 미리 예약해 프레임 틈 없이 잇는다.
        double sequenceStartTime = AudioSettings.dspTime + 0.05d;
        double introDuration = (double)battleIntro.samples / battleIntro.frequency;

        musicIntroSource.clip = battleIntro;
        musicIntroSource.PlayScheduled(sequenceStartTime);

        musicSource.loop = true;
        musicSource.clip = battleLoop;
        musicSource.PlayScheduled(sequenceStartTime + introDuration);
    }

    private void PlayBattleLoop()
    {
        if (battleLoop == null)
            return;

        musicIntroSource.Stop();
        musicSource.loop = true;
        musicSource.clip = battleLoop;
        musicSource.Play();
    }

    private CharacterClipSet ResolveCharacterClips(PlayerController player)
    {
        string characterName = player?.CharacterData?.characterName;
        if (string.IsNullOrEmpty(characterName))
            return null;

        characterClipSets.TryGetValue(characterName, out CharacterClipSet set);
        return set;
    }

    private static AudioClip ClipAt(AudioClip[] clips, int index)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int wrappedIndex = Mathf.Abs(index) % clips.Length;
        return clips[wrappedIndex];
    }

    private static AudioClip[] LoadMany(params string[] clipNames)
    {
        AudioClip[] clips = new AudioClip[clipNames.Length];
        for (int i = 0; i < clipNames.Length; i++)
            clips[i] = Load(clipNames[i]);

        return clips;
    }

    private void PreloadEnemyAttackClips(params string[] clipNames)
    {
        foreach (string clipName in clipNames)
        {
            AudioClip clip = Load(clipName);
            enemyAttackClips[clipName] = clip;

            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }
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

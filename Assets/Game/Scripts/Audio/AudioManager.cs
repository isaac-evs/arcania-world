using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The sounds and music of the game. self.audio in main.py played one sound per name; this keeps that for the sound effects
// (Play / Stop) and adds the music: map music that follows the map, fight music, victory music and short jingles.
// One per scene: add it to the Game object. The other scripts use the static methods.
public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioClip[] clips;   // every file of Assets/Game/audio

    static AudioManager instance;
    readonly Dictionary<string, AudioClip> byName = new Dictionary<string, AudioClip>();
    // songs that share a name apart from the number (overworld1, overworld2...) form a group that is shuffled
    readonly Dictionary<string, List<AudioClip>> groups = new Dictionary<string, List<AudioClip>>();
    readonly Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>(); // sound effects, one source per sound

    // music
    AudioSource music, jingle;
    string musicName;          // the song or group playing now, null when there is none
    AudioClip lastClip;
    bool musicPaused;          // paused while a jingle plays over it
    bool jingleActive;
    string mapCategory = "overworld";
    bool isNight;              // the overworld has its own songs for the night
    bool mapMusicWanted;       // the map music should be playing as soon as nothing else is

    // the victory music is not played to the end: a few seconds, then it fades out while the game takes over
    const float VictoryPlayTime = 3f, VictoryFadeTime = 1.2f, MusicFadeInTime = 1.5f;
    bool victoryPlaying;
    float victoryTime;
    bool fadeInNextMusic;      // the next song starts silent and rises

    // when day turns into night (or the reverse) the song that is playing fades out and a song of the other set fades in
    const float SwitchFadeTime = 2.5f;
    bool switching;
    float switchTime;

    void Awake()
    {
        instance = this;
        foreach (var clip in clips)
        {
            if (clip == null) continue; // a sound that was deleted from the project leaves an empty slot

            byName[clip.name] = clip;

            string baseName = clip.name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (baseName.Length == 0) baseName = clip.name;
            if (!groups.TryGetValue(baseName, out var group)) groups[baseName] = group = new List<AudioClip>();
            group.Add(clip);
        }

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        jingle = gameObject.AddComponent<AudioSource>();
        jingle.playOnAwake = false;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // victory music: it plays for 3 seconds and then fades out; once it has gone the code below starts the map music
        if (jingleActive && victoryPlaying)
        {
            victoryTime += dt;
            if (victoryTime > VictoryPlayTime)
            {
                float t = (victoryTime - VictoryPlayTime) / VictoryFadeTime;
                jingle.volume = Mathf.Clamp01(1f - t);
                if (t >= 1f) jingle.Stop();
            }
        }

        // day turns into night: the song fades out, then a song of the other set rises
        if (switching)
        {
            switchTime += dt;
            music.volume = Mathf.Clamp01(1f - switchTime / SwitchFadeTime);
            if (switchTime >= SwitchFadeTime)
            {
                switching = false;
                if (mapMusicWanted && !jingleActive && mapCategory == "overworld")
                {
                    StopMusicNow();
                    fadeInNextMusic = true;
                    StartMusic(MapMusicName());
                }
                else
                {
                    music.volume = 1f;
                }
            }
        }

        // the map music rises smoothly after the victory music or a change of the time of day
        if (!switching && music.isPlaying && music.volume < 1f) music.volume = Mathf.MoveTowards(music.volume, 1f, dt / MusicFadeInTime);

        // a group plays one song after the other, never the same one twice in a row
        if (musicName != null && !musicPaused && !music.isPlaying && groups[musicName].Count > 1) PlayNextSong();

        // when a jingle ends: the music underneath carries on, or the map music starts if it was waiting
        if (jingleActive && !jingle.isPlaying)
        {
            jingleActive = false;
            if (victoryPlaying)
            {
                victoryPlaying = false;
                jingle.volume = 1f;
                fadeInNextMusic = true; // the map music comes in softly after the victory music
            }

            if (musicPaused)
            {
                music.UnPause();
                musicPaused = false;
            }
            else if (mapMusicWanted)
            {
                StartMusic(MapMusicName());
            }
        }
    }

    // ---- sound effects: Sound.play() / Sound.stop() ----

    AudioSource SourceFor(string name, AudioClip clip)
    {
        if (!sources.TryGetValue(name, out var source))
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            sources[name] = source;
        }
        return source;
    }

    // A sound plays once and can overlap itself (the attack sounds, the notice sound, the evolution jingle)
    public static void Play(string name)
    {
        if (instance == null || !instance.byName.TryGetValue(name, out var clip)) return;
        instance.SourceFor(name, clip).PlayOneShot(clip);
    }

    // Stops every copy of the sound that is playing
    public static void Stop(string name)
    {
        if (instance != null && instance.sources.TryGetValue(name, out var source)) source.Stop();
    }

    // ---- music ----

    // Which music a map has: the world and the house share the overworld songs
    static string CategoryOf(string mapName)
    {
        switch (mapName)
        {
            case "hospital":
            case "hospital2": return "hospital";
            case "arena":
            case "fire":
            case "plant":
            case "water": return "gym";
            default: return "overworld";
        }
    }

    // The map has changed (the screen is black): the music changes with it when the kind of place changes
    public static void SetMap(string mapName)
    {
        if (instance == null) return;
        string category = CategoryOf(mapName);
        bool changed = category != instance.mapCategory;
        instance.mapCategory = category;
        if (changed && instance.mapMusicWanted && !instance.jingleActive) instance.StartMusic(instance.MapMusicName());
    }

    // The music of the place the player is in: the overworld songs change to their night versions when it is night
    string MapMusicName()
    {
        return mapCategory == "overworld" && isNight && groups.ContainsKey("overworld_night") ? "overworld_night" : mapCategory;
    }

    // Day turns into night or night into day. The overworld song that is playing fades out and a song of the other set fades in,
    // so a night song cannot play on into the morning (the songs are minutes long and a whole day lasts ten minutes).
    public static void SetNight(bool night)
    {
        if (instance == null || instance.isNight == night) return;
        instance.isNight = night;

        bool overworldPlaying = instance.musicName == "overworld" || instance.musicName == "overworld_night";
        if (instance.mapMusicWanted && !instance.jingleActive && instance.mapCategory == "overworld" && overworldPlaying)
        {
            instance.switching = true;
            instance.switchTime = 0f;
        }
    }

    // The place of the player has its music: what self.audio['overworld'].play(-1) did in main.py.
    // If a jingle (victory music) is still playing, the map music waits for it to end.
    public static void StartMapMusic()
    {
        if (instance == null) return;
        instance.mapMusicWanted = true;
        if (!instance.jingleActive) instance.StartMusic(instance.MapMusicName());
    }

    // Everything else goes quiet and this music begins (the title music, the fight music...). The map music stays off until it is asked for again.
    public static void PlayMusic(string name)
    {
        if (instance == null) return;
        instance.mapMusicWanted = false;
        instance.StopJingleNow();
        instance.StopMusicNow();
        instance.StartMusic(name);
    }

    // A fight starts. name: battle_wild, battle_trainer or battle_gym_boss
    public static void PlayBattleMusic(string name)
    {
        PlayMusic(name);
    }

    // A trainer has seen the player and walks up
    public static void PlayApproachMusic()
    {
        PlayBattleMusic("trainer_approachs");
    }

    // The fight is won: the victory music plays once (wild_defeated, trainer_defeated or gym_boss_defeated)
    public static void PlayVictory(string name)
    {
        if (instance == null || !instance.byName.TryGetValue(name, out var clip)) return;
        instance.StopMusicNow();
        instance.victoryPlaying = true;
        instance.victoryTime = 0f;
        instance.PlayJingle(clip);
    }

    // The nurse heals the monsters: a short jingle over the music, which carries on afterwards
    public static void PlayHealUp()
    {
        if (instance == null || !instance.byName.TryGetValue("heal_up", out var clip)) return;
        if (instance.music.isPlaying)
        {
            instance.music.Pause();
            instance.musicPaused = true;
        }
        instance.PlayJingle(clip);
    }

    public static void StopJingle()
    {
        if (instance != null) instance.StopJingleNow();
    }

    void PlayJingle(AudioClip clip)
    {
        jingle.volume = 1f;
        jingle.clip = clip;
        jingle.loop = false;
        jingle.Play();
        jingleActive = true;
    }

    void StopJingleNow()
    {
        jingle.Stop();
        jingle.volume = 1f;
        jingleActive = false;
        victoryPlaying = false;
        if (musicPaused)
        {
            music.UnPause();
            musicPaused = false;
        }
    }

    void StopMusicNow()
    {
        switching = false;
        music.Stop();
        musicName = null;
        musicPaused = false;
    }

    // Starts a song, or a group of songs; does nothing if it is already playing
    void StartMusic(string name)
    {
        if (!groups.ContainsKey(name)) return;
        if (musicName == name && (music.isPlaying || musicPaused)) return;

        musicName = name;
        PlayNextSong();
    }

    void PlayNextSong()
    {
        var group = groups[musicName];
        AudioClip clip = group[0];
        if (group.Count > 1)
        {
            do { clip = group[Random.Range(0, group.Count)]; } while (clip == lastClip);
        }

        lastClip = clip;
        music.clip = clip;
        switching = false;
        music.volume = fadeInNextMusic ? 0f : 1f;
        fadeInNextMusic = false;
        music.loop = group.Count == 1; // a single song repeats, a group moves on to another song when one ends
        music.Play();
        musicPaused = false;
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the sounds
    void Reset()
    {
        var found = new List<AudioClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/audio" }))
            found.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
        clips = found.ToArray();
    }
#endif
}

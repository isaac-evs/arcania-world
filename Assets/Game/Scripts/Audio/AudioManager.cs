using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The sounds of the game: self.audio in main.py (audio_importer loads every file of the audio folder by name).
// One per scene: add it to the Game object. The other scripts use the static Play and Stop.
public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioClip[] clips;   // battle, evolution, explosion, fire, green, ice, notice, overworld, scratch, splash

    static AudioManager instance;
    readonly Dictionary<string, AudioClip> byName = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>(); // one per sound, like pygame's Sound.stop()

    void Awake()
    {
        instance = this;
        foreach (var clip in clips) byName[clip.name] = clip;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

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

    // Sound.play(): a sound plays once and can overlap itself; with loop = true it is Sound.play(-1)
    public static void Play(string name, bool loop = false)
    {
        if (instance == null || !instance.byName.TryGetValue(name, out var clip)) return;

        var source = instance.SourceFor(name, clip);
        if (loop)
        {
            // starting a music that is already playing does nothing (the Python game would layer a second copy over the first)
            source.loop = true;
            if (!source.isPlaying) source.Play();
        }
        else
        {
            source.PlayOneShot(clip);
        }
    }

    // Sound.stop(): stops every copy of the sound that is playing
    public static void Stop(string name)
    {
        if (instance != null && instance.sources.TryGetValue(name, out var source)) source.Stop();
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

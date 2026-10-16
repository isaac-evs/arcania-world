using System;
using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Port of the 'Entities' part of Game.setup in main.py: places the trainers from the map.
// Attach to the imported map root (the "world" object).
public class OverworldCharacters : MonoBehaviour
{
    const float TileSize = 64f;

    [SerializeField] Texture2D[] characterSheets;
    [SerializeField] Texture2D shadowTexture;

    PlayerController player;
    DialogSystem dialogSystem;
    readonly List<Character> spawned = new List<Character>();

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        dialogSystem = FindFirstObjectByType<DialogSystem>();
        if (dialogSystem == null) Debug.LogWarning("No Dialog System in the scene: add one to the world object");

        foreach (var layer in GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name != "Entities") continue;

            foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
            {
                if (obj.m_TiledName == "Character") SpawnCharacter(obj);
            }
        }
    }

    void SpawnCharacter(SuperObject obj)
    {
        var props = obj.GetComponent<SuperCustomProperties>();
        string graphic = Property(props, "graphic");
        string id = Property(props, "character_id");

        if (!TrainerDatabase.Trainers.TryGetValue(id, out var data))
        {
            Debug.LogWarning($"No trainer data for character_id '{id}'");
            return;
        }

        var sheet = Array.Find(characterSheets, t => t != null && t.name == graphic);
        if (sheet == null)
        {
            Debug.LogWarning($"No sprite sheet named '{graphic}' in Character Sheets");
            return;
        }

        // inactive until the textures are assigned, so Entity.Awake can use them
        var go = new GameObject($"Character {id} ({graphic})");
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        // Tiled: origin top-left, y down; the point is the centre of the sprite
        go.transform.position = (Vector2)transform.position + new Vector2(obj.m_X / TileSize, -obj.m_Y / TileSize);

        go.AddComponent<SpriteRenderer>();
        var character = go.AddComponent<Character>();
        var facing = (Direction)Enum.Parse(typeof(Direction), Property(props, "direction"), true);
        character.Setup(sheet, shadowTexture, facing);
        character.characterId = id;
        character.radius = float.Parse(Property(props, "radius"), System.Globalization.CultureInfo.InvariantCulture);
        character.data = data;
        character.player = player;
        character.createDialog = dialogSystem != null ? (Action<Character>)dialogSystem.CreateDialog : null;
        character.earlierCharacters = new List<Character>(spawned); // Python only knows the characters created before this one
        spawned.Add(character);
        go.SetActive(true);
    }

    static string Property(SuperCustomProperties props, string name)
    {
        return props != null && props.TryGetCustomProperty(name, out var property) ? property.m_Value : "";
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills the sheets and applies pixel-art import settings
    void Reset()
    {
        shadowTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/other/shadow.png");

        var sheets = new System.Collections.Generic.List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/graphics/characters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/player.png")) continue; // already set up by hand for the player
            sheets.Add(OverworldAnimations.LoadPixelArt(path));
        }
        characterSheets = sheets.ToArray();
    }
#endif
}

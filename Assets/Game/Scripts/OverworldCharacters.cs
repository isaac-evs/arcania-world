using System;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;

// Port of the 'Entities' part of Game.setup in main.py: places the trainers from the map.
// Added to each imported map by MapLoader, which calls Build.
public class OverworldCharacters : MonoBehaviour
{
    const float TileSize = 64f;

    Texture2D[] characterSheets;
    Texture2D shadowTexture;
    PlayerController player;
    DialogSystem dialogSystem;
    readonly List<Character> spawned = new List<Character>();

    public void Build(Texture2D[] characterSheets, Texture2D shadowTexture, PlayerController player, DialogSystem dialogSystem)
    {
        this.characterSheets = characterSheets;
        this.shadowTexture = shadowTexture;
        this.player = player;
        this.dialogSystem = dialogSystem;

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
        float tiledX = obj.m_X, tiledY = obj.m_Y;
        if (name == "hospital2" && id == "Nurse")
        {
            // hospital2.tmx puts the nurse inside the counter's collision block (y 374), where the player can never get within the
            // 100 px talking range. The hospital layouts are identical, so she stands where the first hospital's nurse does.
            tiledX = 287.846f;
            tiledY = 431.973f;
        }
        go.transform.position = (Vector2)transform.position + new Vector2(tiledX / TileSize, -tiledY / TileSize);

        go.AddComponent<SpriteRenderer>();
        var character = go.AddComponent<Character>();

        // hospital2.tmx has a nurse without 'direction' and 'radius' (the Python game would crash on it), so default them
        string facingName = Property(props, "direction");
        var facing = (Direction)Enum.Parse(typeof(Direction), facingName == "" ? "down" : facingName, true);
        character.Setup(sheet, shadowTexture, facing);
        character.characterId = id;
        string radiusText = Property(props, "radius");
        character.radius = radiusText == "" ? 0f : float.Parse(radiusText, System.Globalization.CultureInfo.InvariantCulture);
        character.nurse = id == "Nurse";
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
}

using System.Collections.Generic;
using UnityEngine;

public enum ProtoSpawnKind { Grunt, Shield, Runner, Guard, Hostage, Barrel, Crate }
public enum ProtoLevelEnding { Escape, Knockout }

[System.Serializable]
public class ProtoSpawn
{
    public ProtoSpawnKind kind;
    [Tooltip("x = sideways (-5.5..5.5), y = distance from the start of the rooftop (5..18)")]
    public Vector2 position;
    [Tooltip("Seconds after the wave starts before this one drops in")]
    public float delay;

    public ProtoSpawn(ProtoSpawnKind kind, float x, float z, float delay = 0f)
    {
        this.kind = kind;
        position = new Vector2(x, z);
        this.delay = delay;
    }
}

[System.Serializable]
public class ProtoWave
{
    public string title;
    public string hint;
    public float speedMultiplier = 1f;
    public List<ProtoSpawn> spawns = new List<ProtoSpawn>();
}

// One level: a chase across a few rooftops, one wave of goons per rooftop. It ends one of two ways: the boss escapes
// to the next rooftop once the last wave is cleared, or the last rooftop is a showdown where goons keep coming until
// he's knocked out. Spawn positions are relative to each rooftop's origin, tuned for the default curve (hook 0.5,
// boss at z = 19.4, ninja at the rooftop origin).
[CreateAssetMenu(menuName = "Prototype/Level", fileName = "Level")]
public class ProtoLevel : ScriptableObject
{
    // The scene has this many rooftops. An escape needs a spare one for the boss to flee to.
    public const int SceneRooftops = 5;

    [Tooltip("Shown under LEVEL N when the level starts")]
    public string title;
    public ProtoLevelEnding ending = ProtoLevelEnding.Escape;
    [Tooltip("Boss health for this level; 0 keeps the boss prefab's")]
    public int bossHealth;
    [Tooltip("One wave per rooftop: up to 4 for an escape, 5 for a showdown")]
    public List<ProtoWave> rooftops = new List<ProtoWave>();

    public int MaxRooftops => ending == ProtoLevelEnding.Escape ? SceneRooftops - 1 : SceneRooftops;
    public bool IsShowdown(int rooftop) => ending == ProtoLevelEnding.Knockout && rooftop == rooftops.Count - 1;
}

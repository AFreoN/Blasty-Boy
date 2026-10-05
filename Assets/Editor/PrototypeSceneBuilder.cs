using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the rooftop-chase prototype scene and its gameplay prefabs. Safe to re-run: it overwrites
// Assets/Scenes/Prototype.unity, Assets/Prefabs/Prototype/* and Assets/Materials/Prototype/*, and never modifies the
// original game's scenes or prefabs. Run Tools > Prototype > Art > Build All first (characters + controllers).
public static class PrototypeSceneBuilder
{
    const string scenePath = "Assets/Scenes/Prototype.unity";
    const string gameScenePath = "Assets/Scenes/Game.unity";
    const string prefabDir = "Assets/Prefabs/Prototype";
    const string materialDir = "Assets/Materials/Prototype";
    const string castDir = "Assets/Art/Characters/Prefabs";
    const string cityDir = "Assets/ThirdParty/Quaternius/DowntownCity";
    const string propDir = "Assets/Art/Props";
    // Asset Store content installed locally (gitignored). Everything that uses it is optional.
    const string storeDir = "Assets/_Store";
    const string skinPath = "Assets/Art/UI/UISkin.asset";
    const int rooftopCount = 5;
    const float zoneSpacing = 34f;

    struct Materials { public Material alpha, additive, aimLine, roof, brick, trim, metal; }
    struct Prefabs { public GameObject goon, riot, runner, hostage, boss, barrel, crate, blade, rooftop; }

    [MenuItem("Tools/Prototype/Build Prototype Scene")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        EnsureFolder("Assets/Prefabs", "Prototype");
        EnsureFolder("Assets/Materials", "Prototype");
        AssetDatabase.DeleteAsset(prefabDir + "/Proto_Enemy.prefab");
        AssetDatabase.DeleteAsset(prefabDir + "/Proto_Board.prefab");

        Materials mats = BuildMaterials();
        Prefabs prefabs = BuildPrefabs(mats);
        BuildScene(prefabs, mats);
        AssetDatabase.SaveAssets();
        return "Prototype built: " + scenePath;
    }

    #region Materials
    static Materials BuildMaterials()
    {
        var m = new Materials
        {
            alpha = MaterialAsset("FX_Alpha", "Sprites/Default"),
            additive = MaterialAsset("FX_Additive", "Legacy Shaders/Particles/Additive"),
            aimLine = MaterialAsset("AimLine", "Sprites/Default"),
            roof = Surface("Roof", "T_Concrete_BaseColor", new Vector2(4f, 6f), new Color(0.82f, 0.82f, 0.85f)),
            brick = Surface("Brick", "T_RedBrick_BaseColor", new Vector2(5f, 14f), Color.white),
            trim = Surface("Trim", "T_Trim_BaseColor", new Vector2(6f, 1f), new Color(0.75f, 0.72f, 0.7f)),
            metal = Surface("Metal", "T_MetalConcrete_BaseColor", new Vector2(1f, 1f), new Color(0.6f, 0.65f, 0.7f)),
        };
        return m;
    }

    static Material Surface(string name, string texture, Vector2 tiling, Color tint)
    {
        Material mat = MaterialAsset(name, "Standard");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(cityDir + "/Textures/" + texture + ".png");
        mat.mainTextureScale = tiling;
        mat.color = tint;
        mat.SetFloat("_Glossiness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MaterialAsset(string name, string shader)
    {
        string path = materialDir + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = Shader.Find(shader);
        }
        return mat;
    }
    #endregion

    #region Prefabs
    static Prefabs BuildPrefabs(Materials mats)
    {
        var p = new Prefabs();
        var shield = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KayKit/Props/shield_square_color.fbx");

        p.goon = MakePrefab(castDir + "/Goon.prefab", "Proto_Goon", root => root.AddComponent<ProtoEnemy>());
        p.runner = MakePrefab(castDir + "/Runner.prefab", "Proto_Runner", root => root.AddComponent<ProtoEnemy>());
        p.riot = MakePrefab(castDir + "/RiotGoon.prefab", "Proto_RiotGoon", root => SetField(root.AddComponent<ProtoEnemy>(), "shieldPrefab", shield));
        p.hostage = MakePrefab(castDir + "/Hostage.prefab", "Proto_Hostage", root => root.AddComponent<ProtoHostage>());
        p.boss = MakePrefab(castDir + "/Boss.prefab", "Proto_Boss", root => root.AddComponent<ProtoBoss>());

        p.barrel = MakePrefab(propDir + "/Prop_ExplosiveDrum.fbx", "Proto_Barrel", root =>
        {
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.62f, 0f);
            col.height = 1.24f;
            col.radius = 0.44f;
            root.AddComponent<ProtoBarrel>();
        });

        p.crate = MakePrefab(propDir + "/Prop_HVAC.fbx", "Proto_Crate", root =>
        {
            Bounds b = WorldBounds(root);
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(b.center);
            col.size = b.size;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.isKinematic = true;
            root.AddComponent<ProtoCrate>();
        });

        p.blade = MakePrefab("Assets/Prefabs/shuriken_4.prefab", "Proto_Blade", root =>
        {
            Object.DestroyImmediate(root.GetComponent<Blade>());
            Object.DestroyImmediate(root.GetComponent<Rigidbody>());
            Object.DestroyImmediate(root.GetComponent<Collider>());
            Transform mesh = root.transform.GetChild(0);
            Object.DestroyImmediate(mesh.GetComponent<Weapon>());

            TrailRenderer trail = root.GetComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.widthMultiplier = 0.55f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var blade = root.AddComponent<ProtoBlade>();
            SetField(blade, "mesh", mesh);
            SetField(blade, "trail", trail);
        });

        p.rooftop = BuildRooftop(mats);
        return p;
    }

    // One rooftop arena: concrete roof (play area x -6..6, z -2.5..21.5), the brick building under it, trim parapets
    // and rooftop clutter kept outside the play lanes.
    static GameObject BuildRooftop(Materials mats)
    {
        var root = new GameObject("Proto_Rooftop");
        try
        {
            Box(root, "Roof", new Vector3(0f, -0.25f, 9.5f), new Vector3(13f, 0.5f, 24f), mats.roof, true);
            Box(root, "Building", new Vector3(0f, -20.5f, 9.5f), new Vector3(13.6f, 40f, 24.6f), mats.brick, false);
            Box(root, "ParapetL", new Vector3(-6.65f, 0.25f, 9.5f), new Vector3(0.35f, 0.5f, 24.6f), mats.trim, false);
            Box(root, "ParapetR", new Vector3(6.65f, 0.25f, 9.5f), new Vector3(0.35f, 0.5f, 24.6f), mats.trim, false);
            Box(root, "ParapetFront", new Vector3(0f, 0.15f, -2.65f), new Vector3(13.6f, 0.3f, 0.35f), mats.trim, false);
            Box(root, "LedgeFar", new Vector3(0f, 0.08f, 21.65f), new Vector3(13.6f, 0.16f, 0.35f), mats.trim, false);

            var ac = AssetDatabase.LoadAssetAtPath<GameObject>(cityDir + "/Models/Prop_ACUnit.fbx");
            foreach (var pos in new[] { new Vector3(-5.6f, 0f, -1.2f), new Vector3(5.6f, 0f, 3f), new Vector3(-5.7f, 0f, 18.5f) })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(ac);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0f, pos.x < 0f ? 90f : -90f, 0f);
            }
            WaterTower(root, new Vector3(5.4f, 0f, 19.5f), mats);
            return PrefabUtility.SaveAsPrefabAsset(root, prefabDir + "/Proto_Rooftop.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void WaterTower(GameObject parent, Vector3 position, Materials mats)
    {
        var tower = new GameObject("WaterTower");
        tower.transform.SetParent(parent.transform, false);
        tower.transform.localPosition = position;
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            Box(tower, "Leg", new Vector3(Mathf.Cos(a) * 0.7f, 1.1f, Mathf.Sin(a) * 0.7f), new Vector3(0.12f, 2.2f, 0.12f), mats.metal, false);
        }
        var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(tank.GetComponent<Collider>());
        tank.name = "Tank";
        tank.transform.SetParent(tower.transform, false);
        tank.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        tank.transform.localScale = new Vector3(1.9f, 1.1f, 1.9f);
        tank.GetComponent<Renderer>().sharedMaterial = mats.trim;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.name = "Roof";
        cap.transform.SetParent(tower.transform, false);
        cap.transform.localPosition = new Vector3(0f, 4.3f, 0f);
        cap.transform.localScale = new Vector3(2f, 0.8f, 2f);
        cap.GetComponent<Renderer>().sharedMaterial = mats.metal;
    }

    static GameObject Box(GameObject parent, string name, Vector3 position, Vector3 size, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject MakePrefab(string sourcePath, string name, System.Action<GameObject> edit)
    {
        // Model files can't be opened as prefab contents; build from an instance instead.
        if (sourcePath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
            try
            {
                edit(instance);
                instance.name = name;
                return PrefabUtility.SaveAsPrefabAsset(instance, prefabDir + "/" + name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            edit(root);
            root.name = name;
            return PrefabUtility.SaveAsPrefabAsset(root, prefabDir + "/" + name + ".prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
    #endregion

    #region Scene
    static void BuildScene(Prefabs prefabs, Materials mats)
    {
        // The prototype scene can't be overwritten while it's open; if it's the only scene, switch to the game scene.
        Scene existing = SceneManager.GetSceneByPath(scenePath);
        if (existing.isLoaded)
        {
            if (SceneManager.sceneCount == 1)
                EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);
            else
                EditorSceneManager.CloseScene(existing, true);
        }

        Scene previousActive = SceneManager.GetActiveScene();
        Scene game = SceneManager.GetSceneByPath(gameScenePath);
        bool openedGame = !game.isLoaded;
        if (openedGame)
            game = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(game);
        Material skybox = RenderSettings.skybox;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.75f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.62f, 0.75f, 0.95f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 140f;

        GameObject sun = Copy(game, "Directional Light", scene);
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        var light = sun.GetComponent<Light>();
        light.color = new Color(1f, 0.93f, 0.82f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        Copy(game, "EventSystem", scene);

        GameObject camera = Copy(game, "Main Camera", scene);
        Object.DestroyImmediate(camera.GetComponent<CameraController>());
        camera.AddComponent<ProtoCamera>();
        camera.GetComponent<Camera>().farClipPlane = 200f;

        // The chase: one rooftop per wave, plus the city around and below it.
        var world = new GameObject("Rooftops");
        SceneManager.MoveGameObjectToScene(world, scene);
        for (int i = 0; i < rooftopCount; i++)
        {
            var roof = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.rooftop, scene);
            roof.name = "Rooftop " + (i + 1);
            roof.transform.SetParent(world.transform, false);
            roof.transform.localPosition = Vector3.forward * zoneSpacing * i;
        }
        BuildSkyline(scene);

        var lineGo = new GameObject("AimLine");
        SceneManager.MoveGameObjectToScene(lineGo, scene);
        var line = lineGo.AddComponent<LineRenderer>();
        line.sharedMaterial = mats.aimLine;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(1.6f, 1f);
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.widthMultiplier = 0.2f;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 0;

        var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.boss, scene);
        boss.transform.SetPositionAndRotation(new Vector3(0f, 0f, 19.4f), Quaternion.LookRotation(Vector3.back));

        var ninja = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(castDir + "/Ninja.prefab"), scene);
        ninja.name = "Player";
        ninja.tag = "Player";
        ninja.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var thrower = ninja.AddComponent<ProtoThrower>();
        SetField(thrower, "bladePrefab", prefabs.blade.GetComponent<ProtoBlade>());
        SetField(thrower, "target", boss.GetComponent<ProtoBoss>());
        SetField(thrower, "aimLine", line);

        var systems = new GameObject("Prototype");
        SceneManager.MoveGameObjectToScene(systems, scene);
        systems.AddComponent<ProtoTime>();
        systems.AddComponent<ProtoAudio>();
        var music = systems.AddComponent<ProtoMusic>();
        AssignMusic(music);
        var fx = systems.AddComponent<ProtoFX>();
        SetField(fx, "alphaMaterial", mats.alpha);
        SetField(fx, "additiveMaterial", mats.additive);
        SetField(fx, "explosionPrefab", StoreAsset<GameObject>("ExplosionEffect2", "t:Prefab"));
        var hud = systems.AddComponent<ProtoHUD>();
        SetField(hud, "skin", BuildSkin());
        SetField(hud, "legacyFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/LuckiestGuy-Regular.ttf"));
        var director = systems.AddComponent<ProtoGame>();
        SetField(director, "gruntPrefab", prefabs.goon.GetComponent<ProtoEnemy>());
        SetField(director, "shieldPrefab", prefabs.riot.GetComponent<ProtoEnemy>());
        SetField(director, "runnerPrefab", prefabs.runner.GetComponent<ProtoEnemy>());
        SetField(director, "hostagePrefab", prefabs.hostage.GetComponent<ProtoHostage>());
        SetField(director, "barrelPrefab", prefabs.barrel.GetComponent<ProtoBarrel>());
        SetField(director, "cratePrefab", prefabs.crate.GetComponent<ProtoCrate>());
        SetField(director, "boss", boss.GetComponent<ProtoBoss>());
        director.EditorAssignDefaultWaves();
        EditorUtility.SetDirty(director);

        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new System.Exception("Could not save " + scenePath);

        if (previousActive.IsValid() && previousActive.isLoaded)
            SceneManager.SetActiveScene(previousActive);
        EditorSceneManager.CloseScene(scene, true);
        if (openedGame)
            EditorSceneManager.CloseScene(game, true);
    }

    // City-kit buildings flanking the chase corridor, a few rising above roof level for a skyline, the rest dropping
    // away below so the rooftops read as high up.
    static void BuildSkyline(Scene scene)
    {
        var root = new GameObject("Skyline");
        SceneManager.MoveGameObjectToScene(root, scene);
        string[] models = { "Building_Large_2", "Building_Medium_2_001", "Building_Small_1" };
        var rng = new System.Random(11);
        float length = zoneSpacing * rooftopCount + 40f;
        for (float z = -10f; z < length; z += 14f)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                string model = models[rng.Next(models.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(cityDir + "/Models/" + model + ".fbx");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetParent(root.transform, true);
                float scale = 1.2f + (float)rng.NextDouble() * 0.6f;
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);
                Bounds b = WorldBounds(go);
                float top = -14f + (float)rng.NextDouble() * (rng.NextDouble() < 0.3 ? 26f : 12f);
                float x = side * (14f + b.extents.x + (float)rng.NextDouble() * 6f);
                go.transform.position += new Vector3(x - b.center.x, top - b.max.y, z - b.center.z);
            }
        }
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static GameObject Copy(Scene from, string rootName, Scene into)
    {
        foreach (GameObject go in from.GetRootGameObjects())
        {
            if (go.name != rootName)
                continue;
            GameObject copy = Object.Instantiate(go);
            copy.name = rootName;
            SceneManager.MoveGameObjectToScene(copy, into);
            return copy;
        }
        throw new System.Exception("'" + rootName + "' not found in " + from.path);
    }
    #endregion

    #region Optional store content (UI kit, music, VFX)
    // Looks an asset up by exact name under Assets/_Store; null when the store content isn't installed.
    static T StoreAsset<T>(string name, string filter) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(storeDir))
            return null;
        foreach (string guid in AssetDatabase.FindAssets(name + " " + filter, new[] { storeDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                continue;
            // Prefer the 256px variant of icons that ship in several sizes.
            if (path.Contains("/ItemIcons/") || path.Contains("/Pictoicons/"))
            {
                if (!path.Contains("/256/"))
                    continue;
            }
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }
        return null;
    }

    static Sprite KitSprite(string name) => StoreAsset<Sprite>(name, "t:Sprite");

    static ProtoUISkin BuildSkin()
    {
        EnsureFolder("Assets/Art", "UI");
        var skin = AssetDatabase.LoadAssetAtPath<ProtoUISkin>(skinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<ProtoUISkin>();
            AssetDatabase.CreateAsset(skin, skinPath);
        }

        skin.font = StoreAsset<TMP_FontAsset>("LilitaOne-Regular Outline 120 SDF", "t:TMP_FontAsset");
        skin.fontOutlineMaterial = skin.font != null ? skin.font.material : null;

        skin.buttonGreen = KitSprite("button_green");
        skin.buttonYellow = KitSprite("button_yellow");
        skin.buttonBlue = KitSprite("button_blue");
        skin.buttonRed = KitSprite("button_red");
        skin.buttonPause = KitSprite("play_btn_pause");
        skin.panel = KitSprite("popup_bg");
        skin.pill = KitSprite("top_status_bg_white");
        skin.toast = KitSprite("top_status_bg_white");
        skin.dim = KitSprite("common_dimed");
        skin.glow = KitSprite("common_bg_glow_512");

        skin.ribbonOrange = KitSprite("ribbon_bg_orange");
        skin.ribbonGreen = KitSprite("ribbon_bg_green");
        skin.ribbonYellow = KitSprite("ribbon_bg_yellow");
        skin.starLarge = KitSprite("result_star_large");
        skin.starSmall = KitSprite("result_star_samll");
        skin.tile = KitSprite("reward_menu_bg");
        skin.textDecoLeft = KitSprite("text_deco_line_left");
        skin.textDecoRight = KitSprite("text_deco_line_right");

        skin.killDouble = KitSprite("txt_double_kill");
        skin.killTriple = KitSprite("txt_triple_kill");
        skin.killQuadra = KitSprite("txt_quadra_kill");
        skin.killPenta = KitSprite("txt_penta_kill");

        skin.bossBarBack = KitSprite("top_status_bg_white");
        skin.bossBarFill = KitSprite("user_enemy_prg_red");
        skin.bossNameTag = KitSprite("play_stage_name_bg");

        skin.iconHeart = KitSprite("common_icon_heart");
        skin.iconTrophy = KitSprite("common_icon_trophy");
        skin.iconCrown = KitSprite("common_icon_crown");
        skin.iconSword = KitSprite("common_icon_sword");
        skin.iconTarget = KitSprite("common_icon_target");
        skin.iconBolt = KitSprite("common_icon_bolt");
        skin.iconBomb = KitSprite("common_icon_bomb");
        skin.iconBoss = KitSprite("stage_icon_boss");
        skin.skillFrame = KitSprite("play_skill_0");
        skin.skillCooldown = KitSprite("play_skill_cooltime_bg");
        skin.tutorialHand = KitSprite("btn_icon_tap");
        skin.speechBubble = KitSprite("tutorial_chat_bg");
        skin.speechArrow = KitSprite("tutorial_chat_bg_arrow");

        EditorUtility.SetDirty(skin);
        return skin;
    }

    static void AssignMusic(ProtoMusic music)
    {
        (string field, string clip, bool loop)[] map =
        {
            ("menu", "Chase MENU LOOP", true),
            ("chase", "Chase LOOP", true),
            ("showdown", "Boss Battle 1 Loop", true),
            ("getReady", "Chase GET READY 3 COUNT", false),
            ("rooftopClear", "Chase Win QUICK", false),
            ("victory", "Triumphant Victory", false),
            ("defeat", "Dramatic Defeat SHORT", false),
            ("star1", "Perc 1st Star", false),
            ("star2", "Perc 2nd Star", false),
            ("star3", "Perc 3rd Star", false),
            ("fever", "Level Up BRASS", false),
        };
        foreach (var m in map)
        {
            AudioClip clip = StoreAsset<AudioClip>(m.clip, "t:AudioClip");
            if (clip != null)
                ConfigureMusicImport(AssetDatabase.GetAssetPath(clip), m.loop);
            SetField(music, m.field, clip);
        }
    }

    // Mobile-friendly import: loops stream as Vorbis, stingers stay compressed in memory.
    static void ConfigureMusicImport(string path, bool loop)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        var load = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
        if (settings.loadType == load && settings.compressionFormat == AudioCompressionFormat.Vorbis)
            return;
        settings.loadType = load;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = !loop;
        importer.SaveAndReimport();
    }
    #endregion

    static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
            throw new System.Exception(target.GetType().Name + " has no serialized field '" + field + "'");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}

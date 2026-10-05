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

        p.barrel = MakePrefab("Assets/Prefabs/Barrell_Explosive.prefab", "Proto_Barrel", root =>
        {
            Object.DestroyImmediate(root.GetComponent<InteractibleObject>());
            root.AddComponent<ProtoBarrel>();
        });

        p.crate = MakePrefab("Assets/Prefabs/Box.prefab", "Proto_Crate", root =>
        {
            Object.DestroyImmediate(root.GetComponent<InteractibleObject>());
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
        var fx = systems.AddComponent<ProtoFX>();
        SetField(fx, "alphaMaterial", mats.alpha);
        SetField(fx, "additiveMaterial", mats.additive);
        SetField(systems.AddComponent<ProtoHUD>(), "font", AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/LuckiestGuy-Regular.ttf"));
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

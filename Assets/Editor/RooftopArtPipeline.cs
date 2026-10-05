using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Art pipeline for the rooftop re-theme. Re-runnable; run the steps in order (or Build All):
//   1. ConfigureRigs     - third-party characters + animation libraries -> Humanoid, so any clip plays on any character
//   2. BuildControllers  - animator controllers assembled from the CC0 clip libraries
//   3. BuildCast         - KayKit characters re-skinned (recolor_kaykit.py), scaled to gameplay height, with controllers
// Plus RenderLineup, a preview-scene renderer used to judge art choices from the CLI.
public static class RooftopArtPipeline
{
    const string thirdParty = "Assets/ThirdParty";
    const string castDir = "Assets/Art/Characters";
    const string controllerDir = "Assets/Art/Characters/Controllers";

    const string UAL1 = thirdParty + "/Quaternius/Animations/UAL1_Standard.fbx";
    const string UAL2 = thirdParty + "/Quaternius/Animations/UAL2_Standard.fbx";
    const string KK = thirdParty + "/KayKit/Animations/Rig_Medium_";

    [MenuItem("Tools/Prototype/Art/Build All")]
    public static string BuildAll() => ConfigureRigs() + "\n" + BuildControllers() + "\n" + BuildCast();

    #region 1. Rigs and clips
    static readonly string[] loopHints = { "Idle", "Walk", "Running", "Loop", "Sprint", "Cheering", "Blocking", "Jump_Idle" };

    [MenuItem("Tools/Prototype/Art/1 Configure Rigs")]
    public static string ConfigureRigs()
    {
        int changed = 0;
        var paths = new List<string>();
        foreach (string dir in new[] { "/Quaternius/BaseCharacters", "/Quaternius/Animations", "/KayKit/Characters", "/KayKit/Animations" })
            paths.AddRange(Directory.GetFiles(thirdParty + dir, "*.fbx").Select(p => p.Replace('\\', '/')));

        foreach (string path in paths)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            if (path.Contains("/Animations/"))
            {
                // Gameplay code moves characters, so bake all root motion into the pose; loop the cyclic clips.
                var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                // Quaternius' UAL rig faces the opposite way to KayKit's; turn its clips around so both libraries agree.
                float rotationOffset = path.Contains("/Quaternius/") ? 180f : 0f;
                foreach (var clip in clips)
                {
                    bool loop = loopHints.Any(h => clip.name.Contains(h)) && !clip.name.Contains("Break") && !clip.name.Contains("Start");
                    if (clip.loopTime != loop || !clip.lockRootRotation || !clip.lockRootHeightY || !clip.lockRootPositionXZ || clip.rotationOffset != rotationOffset)
                        dirty = true;
                    clip.rotationOffset = rotationOffset;
                    clip.loopTime = loop;
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                changed++;
            }
        }
        return "Rigs/clips configured: " + changed;
    }

    public static AnimationClip Clip(string fbxPath, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name));
    }
    #endregion

    #region 2. Animator controllers
    // (state, fbx, clip, returnTo): one-shot states with returnTo blend back on their own; others hold until code moves on.
    struct S
    {
        public string state, fbx, clip, returnTo;
        public S(string state, string fbx, string clip, string returnTo = null) { this.state = state; this.fbx = fbx; this.clip = clip; this.returnTo = returnTo; }
    }

    [MenuItem("Tools/Prototype/Art/2 Build Controllers")]
    public static string BuildControllers()
    {
        Directory.CreateDirectory(controllerDir);
        var made = new List<string>
        {
            Controller("Ninja", "Idle",
                new S("Idle", KK + "CombatMelee.fbx", "Melee_Unarmed_Idle"),
                new S("Throw", KK + "General.fbx", "Throw", "Idle"),
                new S("Run", KK + "MovementBasic.fbx", "Running_A"),
                new S("JumpStart", UAL2, "NinjaJump_Start", "JumpAir"),
                new S("JumpAir", UAL2, "NinjaJump_Idle_Loop"),
                new S("Land", UAL2, "NinjaJump_Land", "Idle"),
                new S("Hit", KK + "General.fbx", "Hit_A", "Idle"),
                new S("Death", KK + "General.fbx", "Death_A"),
                new S("Cheer", KK + "Simulation.fbx", "Cheering")),
            Controller("Goon", "Idle",
                new S("Idle", KK + "General.fbx", "Idle_B"),
                new S("Guard", KK + "CombatMelee.fbx", "Melee_Blocking"),
                new S("Walk", KK + "MovementBasic.fbx", "Walking_A"),
                new S("Run", KK + "MovementBasic.fbx", "Running_A"),
                new S("Attack", KK + "CombatMelee.fbx", "Melee_Unarmed_Attack_Punch_A"),
                new S("Fall", KK + "MovementBasic.fbx", "Jump_Idle"),
                new S("Land", KK + "MovementBasic.fbx", "Jump_Land", "Idle"),
                new S("Hit", KK + "General.fbx", "Hit_A"),
                new S("Death", KK + "General.fbx", "Death_A"),
                new S("Taunt", KK + "Simulation.fbx", "Cheering")),
            Controller("Boss", "Idle",
                new S("Idle", UAL2, "Idle_FoldArms_Loop"),
                new S("Taunt", UAL2, "Idle_No_Loop", "Idle"),
                new S("Hit", KK + "General.fbx", "Hit_B", "Idle"),
                new S("Run", KK + "MovementBasic.fbx", "Running_A"),
                new S("JumpAir", KK + "MovementBasic.fbx", "Jump_Idle"),
                new S("Land", KK + "MovementBasic.fbx", "Jump_Land", "Idle"),
                new S("KO", KK + "General.fbx", "Death_B")),
            Controller("Hostage", "Captive",
                new S("Captive", KK + "Simulation.fbx", "Sit_Floor_Idle"),
                new S("Cheer", KK + "Simulation.fbx", "Cheering"),
                new S("Run", KK + "MovementBasic.fbx", "Running_A"),
                new S("Hit", KK + "General.fbx", "Hit_A")),
        };
        AssetDatabase.SaveAssets();
        return "Controllers: " + string.Join(", ", made);
    }

    static string Controller(string name, string defaultState, params S[] states)
    {
        string path = controllerDir + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = controller.layers[0].stateMachine;
        var built = new Dictionary<string, AnimatorState>();
        foreach (S s in states)
        {
            AnimationClip clip = Clip(s.fbx, s.clip);
            if (clip == null)
                throw new System.Exception(name + ": clip '" + s.clip + "' not found in " + s.fbx);
            AnimatorState st = sm.AddState(s.state);
            st.motion = clip;
            built[s.state] = st;
        }
        foreach (S s in states)
        {
            if (s.returnTo == null)
                continue;
            AnimatorStateTransition t = built[s.state].AddTransition(built[s.returnTo]);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.12f;
        }
        sm.defaultState = built[defaultState];
        return name;
    }
    #endregion

    #region 3. Cast
    // name, source model, recolored atlas (null keeps the original), gameplay height in metres
    static readonly (string name, string fbx, string texture, float height)[] cast =
    {
        ("Ninja", thirdParty + "/KayKit/Characters/Rogue_Hooded.fbx", "ninja_texture.png", 2.0f),
        ("Goon", thirdParty + "/KayKit/Characters/Rogue_Hooded.fbx", "goon_texture.png", 2.0f),
        ("Runner", thirdParty + "/KayKit/Characters/Rogue_Hooded.fbx", "runner_texture.png", 1.8f),
        ("RiotGoon", thirdParty + "/KayKit/Characters/Knight.fbx", "riot_texture.png", 2.1f),
        ("Boss", thirdParty + "/KayKit/Characters/Barbarian.fbx", "boss_texture.png", 2.9f),
        ("Hostage", thirdParty + "/KayKit/Characters/Rogue.fbx", null, 1.8f),
    };

    static string ControllerFor(string castName) =>
        castName == "Ninja" ? "Ninja" : castName == "Boss" ? "Boss" : castName == "Hostage" ? "Hostage" : "Goon";

    [MenuItem("Tools/Prototype/Art/3 Build Cast")]
    public static string BuildCast()
    {
        Directory.CreateDirectory(castDir + "/Prefabs");
        var built = new List<string>();
        foreach (var c in cast)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(c.fbx);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                Material original = instance.GetComponentInChildren<Renderer>().sharedMaterial;
                string matPath = castDir + "/" + c.name + ".mat";
                AssetDatabase.DeleteAsset(matPath);
                var mat = new Material(original);
                if (c.texture != null)
                    mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(castDir + "/" + c.texture);
                mat.SetFloat("_Glossiness", 0.15f);
                // Emission drives hit flashes and the "will be hit" glow at runtime via MaterialPropertyBlocks.
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                AssetDatabase.CreateAsset(mat, matPath);
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                instance.transform.localScale = Vector3.one * (c.height / Bounds(instance).size.y);

                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerDir + "/" + ControllerFor(c.name) + ".controller");
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(instance, castDir + "/Prefabs/" + c.name + ".prefab");
                built.Add(c.name);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
        AssetDatabase.SaveAssets();
        return "Cast: " + string.Join(", ", built);
    }
    #endregion

    #region Lineup renderer
    // Renders the given characters side by side, each posed at `normalizedTime` of `clip`, in an isolated preview scene.
    public static string RenderLineup(string outPath, string[] characterPaths, string clipFbx, string clipName, float normalizedTime,
        int width = 1600, int height = 900, bool gameplayAngle = false, string backdropPath = null, float facing = 0f)
    {
        AnimationClip clip = Clip(clipFbx, clipName);
        if (clip == null)
            return "clip not found: " + clipName;

        Scene preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Lineup");
        SceneManager.MoveGameObjectToScene(root, preview);
        var posed = new List<GameObject>();
        try
        {
            const float spacing = 2.2f;
            for (int i = 0; i < characterPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPaths[i]);
                // Humanoid sampling resets the character's own transform, so placement lives on a parent slot.
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(root.transform, false);
                var go = (GameObject)Object.Instantiate(prefab, slot);
                float scale = 1.8f / Mathf.Max(0.01f, Bounds(go).size.y);
                slot.localScale = Vector3.one * scale;
                slot.position = new Vector3((i - (characterPaths.Length - 1) * 0.5f) * spacing, 0f, 0f);
                slot.rotation = Quaternion.Euler(0f, facing, 0f);
                posed.Add(go);
            }

            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            foreach (GameObject go in posed)
                AnimationMode.SampleAnimationClip(go, clip, clip.length * normalizedTime);
            AnimationMode.EndSampling();

            if (backdropPath != null)
            {
                // Stand the lineup on the building's roof: top of its bounds at y = 0.
                var backdrop = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(backdropPath), root.transform);
                Bounds bb = Bounds(backdrop);
                backdrop.transform.position += new Vector3(-bb.center.x, -bb.max.y, -bb.center.z + 2f);
            }
            else
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.SetParent(root.transform);
                ground.transform.localScale = new Vector3(4f, 1f, 4f);
            }

            var lightGo = new GameObject("Key");
            lightGo.transform.SetParent(root.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45f, 150f, 0f);

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = preview;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.62f, 0.85f);
            cam.fieldOfView = gameplayAngle ? 65f : 30f;
            if (gameplayAngle)
                camGo.transform.SetPositionAndRotation(new Vector3(0f, 7f, -8f), Quaternion.Euler(32f, 0f, 0f));
            else
                camGo.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -9f - characterPaths.Length), Quaternion.Euler(4f, 0f, 0f));

            var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            return "rendered " + outPath;
        }
        finally
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
    #endregion
}

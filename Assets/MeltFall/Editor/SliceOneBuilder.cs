using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeltFall.EditorTools
{
    /// <summary>
    /// One-click scaffolder for the MELTFALL Slice 1 grey-box (design §13 Proto 1).
    /// Creates the ScriptableObject data assets (Water, Sand, LoopTuning, Level_01) and a
    /// playable grey-box scene (Meltfall_Play) wired end to end, using the real Unity API —
    /// so it works without any external tooling. Run: menu MeltFall ▸ Build Slice 1 (Grey-box),
    /// then open Assets/Scenes/Meltfall_Play.unity and press Play. Hold on the sand pillar to
    /// melt it; the gem should drop and land in the safe zone (a win).
    ///
    /// Re-running is safe: existing data assets are reused; the scene is rebuilt.
    /// </summary>
    public static class SliceOneBuilder
    {
        private const string DataRoot = "Assets/MeltFall/Data";
        private const string ScenePath = "Assets/Scenes/Meltfall_Play.unity";

        [MenuItem("MeltFall/Build Slice 1 (Grey-box)")]
        public static void Build()
        {
            EnsureFolder("Assets/MeltFall");
            EnsureFolder(DataRoot);
            EnsureFolder(DataRoot + "/Liquids");
            EnsureFolder(DataRoot + "/Materials");
            EnsureFolder(DataRoot + "/Levels");
            EnsureFolder("Assets/Scenes");

            // ---- Data assets --------------------------------------------------------------
            LoopTuningConfig tuning = LoadOrCreate<LoopTuningConfig>(DataRoot + "/LoopTuning.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetFloat(so, "minWinFallDistance", 1.0f);
                SetFloat(so, "coneReach", 20f);
                SetFloat(so, "coneHalfAngleDegrees", 25f);
                SetFloat(so, "purgeDelaySeconds", 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LiquidDefinition water = LoadOrCreate<LiquidDefinition>(DataRoot + "/Liquids/Water.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "water");
                SetString(so, "displayName", "Water");
                SetColor(so, "color", new Color(0.3f, 0.7f, 1f, 1f));
                SetFloat(so, "burnRatePerSecond", 8f);
                SetFloat(so, "meltPower", 60f);
                var arr = so.FindProperty("matchedMaterialIds");
                if (arr != null) { arr.arraySize = 1; arr.GetArrayElementAtIndex(0).stringValue = "sand"; }
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            MaterialDefinition sand = LoadOrCreate<MaterialDefinition>(DataRoot + "/Materials/Sand.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "sand");
                SetFloat(so, "maxIntegrity", 100f);
                SetString(so, "correctLiquidId", "water");
                SetColor(so, "dissolveColor", new Color(0.85f, 0.75f, 0.45f, 1f));
                SetFloat(so, "chipFraction", 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LevelDefinition level = LoadOrCreate<LevelDefinition>(DataRoot + "/Levels/Level_01.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetFloat(so, "startingFuel", 100f);
                var arr = so.FindProperty("availableLiquidIds");
                if (arr != null) { arr.arraySize = 1; arr.GetArrayElementAtIndex(0).stringValue = "water"; }
                SetInt(so, "gemCount", 1);
                SetInt(so, "starThreshold1", 1);
                SetInt(so, "starThreshold2", 2);
                SetInt(so, "starThreshold3", 3);
                var t = so.FindProperty("tuning");
                if (t != null) t.objectReferenceValue = tuning;
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            AssetDatabase.SaveAssets();

            // ---- Scene --------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera + rig (gun rides the camera so its melt cone points where you tap).
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 7f, -9f);
            camGO.transform.rotation = Quaternion.identity;
            var rig = camGO.AddComponent<StageCameraRig>();
            SetRef(rig, "cameraTransform", camGO.transform);
            SetRef(rig, "tuning", tuning);

            // Light
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground (static, 20x20)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            // Safe zone = a box VOLUME (the win check needs the resting gem's centre inside it).
            var safeGO = new GameObject("SafeZone");
            safeGO.transform.position = new Vector3(0f, 0.75f, 0f);
            var safeCol = safeGO.AddComponent<BoxCollider>();
            safeCol.isTrigger = true;
            safeCol.size = new Vector3(9f, 1.5f, 9f);
            safeGO.AddComponent<SafeZone>();

            // Sand support pillar (meltable). Rests on the ground; holds the gem aloft.
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "SandSupport";
            pillar.transform.position = new Vector3(0f, 2f, 0f);
            pillar.transform.localScale = new Vector3(1f, 4f, 1f);
            var pillarBody = pillar.AddComponent<Rigidbody>();
            pillarBody.mass = 4f;
            var meltable = pillar.AddComponent<MeltableMaterial>();
            SetRef(meltable, "def", sand);

            // Gem resting on top of the pillar. Never melted; falls when the support clears.
            var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = "Gem";
            gem.transform.position = new Vector3(0f, 4.6f, 0f);
            gem.transform.localScale = Vector3.one * 0.6f;
            gem.AddComponent<Rigidbody>();
            var gemComp = gem.AddComponent<Gem>();
            SetRef(gemComp, "tuning", tuning);

            // Liquid gun (child of the camera) — auto-selects Water on Start.
            var gunGO = new GameObject("LiquidGun");
            gunGO.transform.SetParent(camGO.transform, false);
            var gun = gunGO.AddComponent<LiquidGun>();
            SetRef(gun, "tuning", tuning);
            SetObjectArray(gun, "availableLiquids", new Object[] { water });

            // Level manager (auto-discovers gems + zones in Bootstrap).
            var lmGO = new GameObject("LevelManager");
            var lm = lmGO.AddComponent<LevelManager>();
            SetRef(lm, "levelDefinition", level);
            SetRef(lm, "loopTuning", tuning);
            SetRef(lm, "liquidGun", gun);
            SetRef(lm, "cameraRig", rig);

            // Input controller
            var inputGO = new GameObject("StageInput");
            var input = inputGO.AddComponent<StageInputController>();
            SetRef(input, "stageCamera", cam);
            SetRef(input, "liquidGun", gun);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MeltFall] Slice 1 grey-box built at " + ScenePath +
                      ". Press Play, then hold the mouse on the sand pillar to melt it — the gem should drop and land in the safe zone.");
        }

        // ---- helpers ----------------------------------------------------------------------

        private static T LoadOrCreate<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                configure(asset);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                // Re-load after saving: CreateAsset + SaveAssets can invalidate the in-memory
                // instance, so wiring the scene with the original variable would assign a stale
                // (fake-null) reference. The freshly loaded asset is a stable reference.
                asset = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetRef(Object component, string field, Object value)
        {
            var so = new SerializedObject(component);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetObjectArray(Object component, string field, Object[] values)
        {
            var so = new SerializedObject(component);
            var p = so.FindProperty(field);
            if (p != null)
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(SerializedObject so, string f, float v) { var p = so.FindProperty(f); if (p != null) p.floatValue = v; }
        private static void SetInt(SerializedObject so, string f, int v) { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        private static void SetString(SerializedObject so, string f, string v) { var p = so.FindProperty(f); if (p != null) p.stringValue = v; }
        private static void SetColor(SerializedObject so, string f, Color v) { var p = so.FindProperty(f); if (p != null) p.colorValue = v; }
    }
}

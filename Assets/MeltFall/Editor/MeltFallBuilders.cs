using MeltFall.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MeltFall.EditorTools
{
    /// <summary>
    /// One-click scaffolders for the rest of the MELTFALL build (design §4 matching, §8 depth):
    /// the full liquid/material data set, a Screen-Space-Overlay HUD prefab wired to the runtime UI
    /// views, and two content scenes (Slice 3 matching, Slice 4 depth). Complements
    /// <see cref="SliceOneBuilder"/> (Slice 1 grey-box).
    ///
    /// Everything is idempotent: existing data assets are reused (LoadOrCreate reloads after save to
    /// dodge the stale-reference bug), the HUD prefab is overwritten, and scenes are rebuilt from
    /// scratch. All SO tunables are written via SerializedObject onto private fields — nothing is
    /// hardcoded in runtime code. HUD views bind at runtime, so scenes carry a
    /// <see cref="SceneBootstrap"/> that finds the systems and calls each view's Bind method.
    /// </summary>
    public static class MeltFallBuilders
    {
        private const string DataRoot = "Assets/MeltFall/Data";
        private const string PrefabRoot = "Assets/MeltFall/Prefabs";
        private const string HudPrefabPath = PrefabRoot + "/HUD.prefab";
        private const string LiquidButtonPrefabPath = PrefabRoot + "/LiquidButton.prefab";
        private const string Slice3ScenePath = "Assets/Scenes/Meltfall_Match.unity";
        private const string Slice4ScenePath = "Assets/Scenes/Meltfall_Depth.unity";

        // ==================================================================================
        // (a) Data
        // ==================================================================================

        [MenuItem("MeltFall/Data/Build Liquids + Materials")]
        public static void BuildData()
        {
            EnsureFolder("Assets/MeltFall");
            EnsureFolder(DataRoot);
            EnsureFolder(DataRoot + "/Liquids");
            EnsureFolder(DataRoot + "/Materials");

            // ---- Liquids (design §4 matching table) -----------------------------------------
            // Water melts sand (cheap burn); Acid melts metal; Solvent melts stone; Heat melts ice.
            LoadOrCreate<LiquidDefinition>(DataRoot + "/Liquids/Water.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "water");
                SetString(so, "displayName", "Water");
                SetColor(so, "color", new Color(0.3f, 0.7f, 1f, 1f));
                SetFloat(so, "burnRatePerSecond", 8f);
                SetFloat(so, "meltPower", 60f);
                SetStringArray(so, "matchedMaterialIds", new[] { "sand" });
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LoadOrCreate<LiquidDefinition>(DataRoot + "/Liquids/Acid.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "acid");
                SetString(so, "displayName", "Acid");
                SetColor(so, "color", new Color(0.55f, 0.9f, 0.2f, 1f));
                SetFloat(so, "burnRatePerSecond", 18f);
                SetFloat(so, "meltPower", 60f);
                SetStringArray(so, "matchedMaterialIds", new[] { "metal" });
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LoadOrCreate<LiquidDefinition>(DataRoot + "/Liquids/Solvent.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "solvent");
                SetString(so, "displayName", "Solvent");
                SetColor(so, "color", new Color(0.8f, 0.4f, 0.95f, 1f));
                SetFloat(so, "burnRatePerSecond", 12f);
                SetFloat(so, "meltPower", 60f);
                SetStringArray(so, "matchedMaterialIds", new[] { "stone" });
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LoadOrCreate<LiquidDefinition>(DataRoot + "/Liquids/Heat.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "heat");
                SetString(so, "displayName", "Heat");
                SetColor(so, "color", new Color(1f, 0.5f, 0.15f, 1f));
                SetFloat(so, "burnRatePerSecond", 18f);
                SetFloat(so, "meltPower", 60f);
                SetStringArray(so, "matchedMaterialIds", new[] { "ice" });
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            // ---- Materials (maxIntegrity 100, wrong-liquid = Ignore, chip 0) -----------------
            LoadOrCreate<MaterialDefinition>(DataRoot + "/Materials/Sand.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "sand");
                SetFloat(so, "maxIntegrity", 100f);
                SetString(so, "correctLiquidId", "water");
                SetColor(so, "dissolveColor", new Color(0.85f, 0.75f, 0.45f, 1f));
                SetEnum(so, "wrongLiquidResponse", (int)WrongLiquidResponse.Ignore);
                SetFloat(so, "chipFraction", 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LoadOrCreate<MaterialDefinition>(DataRoot + "/Materials/Metal.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "metal");
                SetFloat(so, "maxIntegrity", 100f);
                SetString(so, "correctLiquidId", "acid");
                SetColor(so, "dissolveColor", new Color(0.7f, 0.72f, 0.78f, 1f));
                SetEnum(so, "wrongLiquidResponse", (int)WrongLiquidResponse.Ignore);
                SetFloat(so, "chipFraction", 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LoadOrCreate<MaterialDefinition>(DataRoot + "/Materials/Stone.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "stone");
                SetFloat(so, "maxIntegrity", 100f);
                SetString(so, "correctLiquidId", "solvent");
                SetColor(so, "dissolveColor", new Color(0.55f, 0.55f, 0.58f, 1f));
                SetEnum(so, "wrongLiquidResponse", (int)WrongLiquidResponse.Ignore);
                SetFloat(so, "chipFraction", 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            LoadOrCreate<MaterialDefinition>(DataRoot + "/Materials/Ice.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", "ice");
                SetFloat(so, "maxIntegrity", 100f);
                SetString(so, "correctLiquidId", "heat");
                SetColor(so, "dissolveColor", new Color(0.65f, 0.85f, 1f, 1f));
                SetEnum(so, "wrongLiquidResponse", (int)WrongLiquidResponse.Ignore);
                SetFloat(so, "chipFraction", 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeltFall] Data built: 4 liquids (water/acid/solvent/heat) + 4 materials " +
                      "(sand/metal/stone/ice) under " + DataRoot + ".");
        }

        // ==================================================================================
        // (b) HUD prefab
        // ==================================================================================

        [MenuItem("MeltFall/Build HUD Prefab")]
        public static GameObject BuildHud()
        {
            EnsureFolder("Assets/MeltFall");
            EnsureFolder(PrefabRoot);

            // A LiquidButton prefab the selector instantiates once per available liquid.
            GameObject buttonPrefab = BuildLiquidButtonPrefab();

            // ---- Root canvas ---------------------------------------------------------------
            var hud = new GameObject("HUD", typeof(RectTransform));
            var canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            hud.AddComponent<GraphicRaycaster>();

            // ---- Fuel gauge (top) ----------------------------------------------------------
            RectTransform fuelRoot = CreatePanel("FuelGauge", hud.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -80f), new Vector2(700f, 60f));

            Image fuelBg = CreateImage("Background", fuelRoot, new Color(0f, 0f, 0f, 0.5f));
            StretchToParent((RectTransform)fuelBg.transform);

            Image fuelFill = CreateImage("Fill", fuelRoot, new Color(0.3f, 0.8f, 1f, 1f));
            StretchToParent((RectTransform)fuelFill.transform);
            fuelFill.type = Image.Type.Filled;
            fuelFill.fillMethod = Image.FillMethod.Horizontal;
            fuelFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fuelFill.fillAmount = 1f;

            TextMeshProUGUI fuelLabel = CreateText("Label", fuelRoot, "100%", 32f,
                TextAlignmentOptions.Center);
            StretchToParent((RectTransform)fuelLabel.transform);

            var fuelGauge = fuelRoot.gameObject.AddComponent<FuelGaugeView>();
            SetRef(fuelGauge, "fillImage", fuelFill);
            SetRef(fuelGauge, "label", fuelLabel);

            // ---- Gem tracker (top, below fuel) --------------------------------------------
            RectTransform gemRoot = CreatePanel("GemTracker", hud.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -170f), new Vector2(300f, 70f));
            var gemLayout = gemRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            gemLayout.spacing = 16f;
            gemLayout.childAlignment = TextAnchor.MiddleCenter;
            gemLayout.childForceExpandWidth = false;
            gemLayout.childForceExpandHeight = false;

            var gemSlots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                Image slot = CreateImage("Slot" + i, gemRoot, new Color(1f, 1f, 1f, 0.35f));
                var slotRt = (RectTransform)slot.transform;
                slotRt.sizeDelta = new Vector2(56f, 56f);
                var le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 56f;
                le.preferredHeight = 56f;
                gemSlots[i] = slot;
            }

            var gemTracker = gemRoot.gameObject.AddComponent<GemTrackerView>();
            SetRef(gemTracker, "slotParent", gemRoot);
            SetObjectArray(gemTracker, "prewiredSlots", gemSlots);

            // ---- Liquid selector (bottom) --------------------------------------------------
            RectTransform selRoot = CreatePanel("LiquidSelector", hud.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 130f), new Vector2(760f, 180f));
            var selLayout = selRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            selLayout.spacing = 24f;
            selLayout.childAlignment = TextAnchor.MiddleCenter;
            selLayout.childForceExpandWidth = false;
            selLayout.childForceExpandHeight = false;

            var selector = selRoot.gameObject.AddComponent<LiquidSelectorView>();
            SetRef(selector, "buttonPrefab", buttonPrefab.GetComponent<LiquidButton>());
            SetRef(selector, "buttonParent", selRoot);

            // ---- Aim indicator -------------------------------------------------------------
            // The marker is a world-space 3D quad (AimIndicatorView.SetAimPoint writes a world
            // position), so it lives outside the Canvas rendering but under the HUD prefab root.
            var aimRoot = new GameObject("AimIndicator");
            aimRoot.transform.SetParent(hud.transform, false);
            var aimView = aimRoot.AddComponent<AimIndicatorView>();

            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "AimMarker";
            // Strip the collider so the marker can never be hit by physics / the aim raycast.
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(aimRoot.transform, false);
            marker.transform.localScale = Vector3.one * 0.5f;
            marker.SetActive(false); // Hidden by default (AimIndicatorView also enforces Hidden).
            SetRef(aimView, "marker", marker.transform);

            // ---- Level result panel (full-screen, hidden) ----------------------------------
            var resultRoot = new GameObject("LevelResult", typeof(RectTransform));
            resultRoot.transform.SetParent(hud.transform, false);
            StretchToParent((RectTransform)resultRoot.transform);

            Image dim = CreateImage("Dim", resultRoot.transform, new Color(0f, 0f, 0f, 0.75f));
            StretchToParent((RectTransform)dim.transform);
            dim.raycastTarget = true; // Block taps to the play field behind the overlay.

            // Cleared panel
            var cleared = new GameObject("ClearedPanel", typeof(RectTransform));
            cleared.transform.SetParent(resultRoot.transform, false);
            StretchToParent((RectTransform)cleared.transform);

            TextMeshProUGUI clearedTitle = CreateText("Title", cleared.transform, "LEVEL CLEARED",
                64f, TextAlignmentOptions.Center);
            AnchorCentered((RectTransform)clearedTitle.transform, new Vector2(0f, 320f),
                new Vector2(800f, 100f));
            _ = clearedTitle;

            TextMeshProUGUI clearedLabel = CreateText("GemsLabel", cleared.transform, "1/1 gems",
                40f, TextAlignmentOptions.Center);
            AnchorCentered((RectTransform)clearedLabel.transform, new Vector2(0f, -40f),
                new Vector2(800f, 80f));

            var starIcons = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                Image star = CreateImage("Star" + i, cleared.transform,
                    new Color(1f, 0.85f, 0.2f, 1f));
                AnchorCentered((RectTransform)star.transform,
                    new Vector2((i - 1) * 160f, 140f), new Vector2(120f, 120f));
                starIcons[i] = star.gameObject;
            }

            Button retryCleared = CreateButton("RetryButton", cleared.transform, "Retry",
                new Vector2(-160f, -260f));
            Button continueBtn = CreateButton("ContinueButton", cleared.transform, "Continue",
                new Vector2(160f, -260f));

            // Failed panel
            var failed = new GameObject("FailedPanel", typeof(RectTransform));
            failed.transform.SetParent(resultRoot.transform, false);
            StretchToParent((RectTransform)failed.transform);

            TextMeshProUGUI failedTitle = CreateText("Title", failed.transform, "OUT OF FUEL",
                64f, TextAlignmentOptions.Center);
            AnchorCentered((RectTransform)failedTitle.transform, new Vector2(0f, 120f),
                new Vector2(800f, 100f));

            Button retryFailed = CreateButton("RetryButton", failed.transform, "Retry",
                new Vector2(0f, -120f));

            var resultView = resultRoot.AddComponent<LevelResultView>();
            SetRef(resultView, "root", resultRoot);
            SetRef(resultView, "clearedPanel", cleared);
            SetRef(resultView, "failedPanel", failed);
            SetObjectArray(resultView, "starIcons", starIcons);
            SetRef(resultView, "clearedLabel", clearedLabel);
            SetRef(resultView, "failedLabel", failedTitle);
            SetRef(resultView, "retryButtonCleared", retryCleared);
            SetRef(resultView, "continueButton", continueBtn);
            SetRef(resultView, "retryButtonFailed", retryFailed);

            // Start hidden (LevelResultView.OnEnable also hides at runtime).
            cleared.SetActive(false);
            failed.SetActive(false);
            resultRoot.SetActive(false);

            // ---- Save as prefab ------------------------------------------------------------
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(hud, HudPrefabPath);
            Object.DestroyImmediate(hud);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeltFall] HUD prefab built at " + HudPrefabPath +
                      " (fuel gauge, gem tracker, selector, aim marker, result panel).");
            return prefab;
        }

        private static GameObject BuildLiquidButtonPrefab()
        {
            var go = new GameObject("LiquidButton", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(150f, 150f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg;

            // Swatch (tinted to the liquid color at runtime by LiquidButton.Setup).
            Image swatch = CreateImage("Swatch", go.transform, Color.white);
            AnchorCentered((RectTransform)swatch.transform, new Vector2(0f, 20f),
                new Vector2(100f, 100f));

            // Highlight ring shown when this liquid is active.
            Image highlight = CreateImage("Highlight", go.transform, new Color(1f, 1f, 1f, 0.9f));
            StretchToParent((RectTransform)highlight.transform);
            highlight.type = Image.Type.Sliced;
            highlight.enabled = false;

            // Purge / loading indicator shown while the active liquid is purging.
            Image purge = CreateImage("PurgeIndicator", go.transform, new Color(1f, 0.85f, 0.2f, 0.85f));
            AnchorCentered((RectTransform)purge.transform, new Vector2(0f, 20f),
                new Vector2(40f, 40f));
            purge.enabled = false;

            TextMeshProUGUI label = CreateText("Label", go.transform, "Liquid", 24f,
                TextAlignmentOptions.Center);
            AnchorCentered((RectTransform)label.transform, new Vector2(0f, -55f),
                new Vector2(150f, 40f));

            var lb = go.AddComponent<LiquidButton>();
            SetRef(lb, "button", button);
            SetRef(lb, "swatch", swatch);
            SetRef(lb, "highlight", highlight);
            SetRef(lb, "purgeIndicator", purge);
            SetRef(lb, "label", label);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, LiquidButtonPrefabPath);
            Object.DestroyImmediate(go);
            // Reload so the returned reference is the stable asset, not the destroyed scene object.
            return AssetDatabase.LoadAssetAtPath<GameObject>(LiquidButtonPrefabPath);
        }

        // ==================================================================================
        // (c) Slice 3 — Matching
        // ==================================================================================

        [MenuItem("MeltFall/Build Slice 3 (Matching)")]
        public static void BuildSlice3()
        {
            // Ensure data exists (idempotent) so we can resolve the assets below.
            BuildData();
            EnsureFolder(DataRoot + "/Levels");
            EnsureFolder("Assets/Scenes");

            LoopTuningConfig tuning = LoadTuning();
            LiquidDefinition water = LoadLiquid("Water");
            LiquidDefinition acid = LoadLiquid("Acid");
            LiquidDefinition solvent = LoadLiquid("Solvent");
            MaterialDefinition metal = LoadMaterial("Metal");
            MaterialDefinition stone = LoadMaterial("Stone");

            LevelDefinition level = LoadOrCreate<LevelDefinition>(DataRoot + "/Levels/Level_02.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetFloat(so, "startingFuel", 120f);
                SetStringArray(so, "availableLiquidIds", new[] { "water", "acid", "solvent" });
                SetInt(so, "gemCount", 1);
                SetInt(so, "starThreshold1", 1);
                SetInt(so, "starThreshold2", 2);
                SetInt(so, "starThreshold3", 3);
                var tp = so.FindProperty("tuning");
                if (tp != null) tp.objectReferenceValue = tuning;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            // Re-assign tuning after reload in case the asset already existed.
            SetRef(level, "tuning", tuning);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = CreateCameraRig(tuning, out Camera cam, out StageCameraRig rig);
            CreateLight();
            CreateGround();
            CreateSafeZoneBox(new Vector3(0f, 0.75f, 0f), new Vector3(9f, 1.5f, 9f));

            // Layered excavation: an outer METAL shell (needs acid) guards a STONE pillar (needs
            // solvent) that holds the gem. Water is a decoy on this level (nothing sand here).
            CreateMeltable("MetalShell", metal,
                new Vector3(0f, 2f, 0f), new Vector3(2.4f, 4f, 2.4f), 6f);

            CreateMeltable("StonePillar", stone,
                new Vector3(0f, 2f, 0f), new Vector3(1f, 3.6f, 1f), 4f);

            CreateGem("Gem", new Vector3(0f, 4.4f, 0f), tuning);

            LiquidGun gun = CreateLiquidGun(camGO.transform, tuning,
                new Object[] { water, acid, solvent });

            LevelManager lm = CreateLevelManager(level, tuning, gun, rig);
            CreateStageInput(cam, gun);
            CreateEventSystem();
            InstantiateHudInto(scene, lm, gun);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Slice3ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeltFall] Slice 3 (Matching) built at " + Slice3ScenePath +
                      ": acid the metal shell, then solvent the stone pillar to drop the gem.");
        }

        // ==================================================================================
        // (d) Slice 4 — Depth
        // ==================================================================================

        [MenuItem("MeltFall/Build Slice 4 (Depth)")]
        public static void BuildSlice4()
        {
            BuildData();
            EnsureFolder(DataRoot + "/Levels");
            EnsureFolder("Assets/Scenes");

            LoopTuningConfig tuning = LoadTuning();
            LiquidDefinition water = LoadLiquid("Water");
            LiquidDefinition acid = LoadLiquid("Acid");
            LiquidDefinition solvent = LoadLiquid("Solvent");
            LiquidDefinition heat = LoadLiquid("Heat");
            MaterialDefinition sand = LoadMaterial("Sand");
            MaterialDefinition metal = LoadMaterial("Metal");
            MaterialDefinition stone = LoadMaterial("Stone");
            MaterialDefinition ice = LoadMaterial("Ice");

            LevelDefinition level = LoadOrCreate<LevelDefinition>(DataRoot + "/Levels/Level_03.asset", asset =>
            {
                var so = new SerializedObject(asset);
                SetFloat(so, "startingFuel", 200f);
                SetStringArray(so, "availableLiquidIds", new[] { "water", "acid", "solvent", "heat" });
                SetInt(so, "gemCount", 3);
                SetInt(so, "starThreshold1", 1);
                SetInt(so, "starThreshold2", 2);
                SetInt(so, "starThreshold3", 3);
                var tp = so.FindProperty("tuning");
                if (tp != null) tp.objectReferenceValue = tuning;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            SetRef(level, "tuning", tuning);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = CreateCameraRig(tuning, out Camera cam, out StageCameraRig rig);
            CreateLight();
            CreateGround();

            // Safe landing zone covers most of the ground; a hazard kill-floor sits on the LEFT.
            CreateSafeZoneBox(new Vector3(1.5f, 0.75f, 0f), new Vector3(11f, 1.5f, 11f));
            CreateHazardZoneBox(new Vector3(-6f, 0.4f, 0f), new Vector3(4f, 0.8f, 11f));

            // Gem A (right): straightforward stone pillar (solvent) over safe ground.
            CreateMeltable("StonePillar_A", stone,
                new Vector3(4f, 2f, 0f), new Vector3(1f, 3.6f, 1f), 4f);
            CreateGem("Gem_A", new Vector3(4f, 4.4f, 0f), tuning);

            // Gem B (center): sand core (water) directly above the safe zone.
            CreateMeltable("SandPillar_B", sand,
                new Vector3(0.5f, 2f, 0f), new Vector3(1f, 3.6f, 1f), 4f);
            CreateGem("Gem_B", new Vector3(0.5f, 4.4f, 0f), tuning);

            // Gem C (over the hazard): sits atop an ICE column (heat) that leans over the kill-floor.
            // A metal brace on its LEFT (acid) must be removed to topple it toward the SAFE side —
            // melt the ice first and the gem drops straight into the hazard, so directional collapse
            // matters (design §8: forced collapse direction + layered obstacle).
            CreateMeltable("IceColumn_C", ice,
                new Vector3(-4f, 2f, 0f), new Vector3(1f, 3.6f, 1f), 4f);
            CreateMeltable("MetalBrace_C", metal,
                new Vector3(-5.2f, 2.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), 3f);
            CreateGem("Gem_C", new Vector3(-4f, 4.4f, 0f), tuning);

            LiquidGun gun = CreateLiquidGun(camGO.transform, tuning,
                new Object[] { water, acid, solvent, heat });

            LevelManager lm = CreateLevelManager(level, tuning, gun, rig);
            CreateStageInput(cam, gun);
            CreateEventSystem();
            InstantiateHudInto(scene, lm, gun);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Slice4ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeltFall] Slice 4 (Depth) built at " + Slice4ScenePath +
                      ": 3 gems, a hazard kill-floor on the left, and a directional-collapse gem over it.");
        }

        // ==================================================================================
        // (e) Build Everything
        // ==================================================================================

        [MenuItem("MeltFall/Build Everything")]
        public static void BuildAll()
        {
            BuildData();
            BuildHud();
            // Slice 1 is provided by SliceOneBuilder; keep it in the batch for a full rebuild.
            SliceOneBuilder.Build();
            BuildSlice3();
            BuildSlice4();
            Debug.Log("[MeltFall] Build Everything complete: data, HUD prefab, Slice 1/3/4 scenes.");
        }

        // ==================================================================================
        // HUD instantiation helper
        // ==================================================================================

        /// <summary>
        /// Instantiates the HUD prefab into the given scene and drops a <see cref="SceneBootstrap"/>
        /// that binds the views to the level systems at runtime (the views take live references that
        /// cannot be serialized from the prefab). The <paramref name="lm"/>/<paramref name="gun"/>
        /// are pre-wired onto the bootstrap so it does not have to search; any HUD view references it
        /// still auto-finds from the instantiated prefab. Returns the HUD instance.
        /// </summary>
        public static GameObject InstantiateHudInto(UnityEngine.SceneManagement.Scene scene,
            LevelManager lm, LiquidGun gun)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                // Build it on demand so a scene builder never fails just because the HUD is missing.
                prefab = BuildHud();
            }

            GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            hud.name = "HUD";

            var bootstrapGO = new GameObject("SceneBootstrap");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bootstrapGO, scene);
            var bootstrap = bootstrapGO.AddComponent<SceneBootstrap>();
            SetRef(bootstrap, "levelManager", lm);
            SetRef(bootstrap, "liquidGun", gun);

            return hud;
        }

        // ==================================================================================
        // Scene-object helpers
        // ==================================================================================

        private static GameObject CreateCameraRig(LoopTuningConfig tuning, out Camera cam,
            out StageCameraRig rig)
        {
            var camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 8f, -11f);
            camGO.transform.rotation = Quaternion.identity;
            rig = camGO.AddComponent<StageCameraRig>();
            SetRef(rig, "cameraTransform", camGO.transform);
            SetRef(rig, "tuning", tuning);
            return camGO;
        }

        private static void CreateLight()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
        }

        private static void CreateSafeZoneBox(Vector3 pos, Vector3 size)
        {
            var go = new GameObject("SafeZone");
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            go.AddComponent<SafeZone>();
        }

        private static void CreateHazardZoneBox(Vector3 pos, Vector3 size)
        {
            var go = new GameObject("HazardZone");
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            go.AddComponent<HazardZone>();
        }

        private static GameObject CreateMeltable(string name, MaterialDefinition def, Vector3 pos,
            Vector3 scale, float mass)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            var meltable = go.AddComponent<MeltableMaterial>();
            SetRef(meltable, "def", def);
            SetRef(meltable, "targetRenderer", go.GetComponent<Renderer>());
            return go;
        }

        private static GameObject CreateGem(string name, Vector3 pos, LoopTuningConfig tuning)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.6f;
            go.AddComponent<Rigidbody>();
            var gem = go.AddComponent<Gem>();
            SetRef(gem, "tuning", tuning);
            return go;
        }

        private static LiquidGun CreateLiquidGun(Transform parent, LoopTuningConfig tuning,
            Object[] liquids)
        {
            var gunGO = new GameObject("LiquidGun");
            gunGO.transform.SetParent(parent, false);
            var gun = gunGO.AddComponent<LiquidGun>();
            SetRef(gun, "tuning", tuning);
            SetObjectArray(gun, "availableLiquids", liquids);
            return gun;
        }

        private static LevelManager CreateLevelManager(LevelDefinition level, LoopTuningConfig tuning,
            LiquidGun gun, StageCameraRig rig)
        {
            var lmGO = new GameObject("LevelManager");
            var lm = lmGO.AddComponent<LevelManager>();
            SetRef(lm, "levelDefinition", level);
            SetRef(lm, "loopTuning", tuning);
            SetRef(lm, "liquidGun", gun);
            SetRef(lm, "cameraRig", rig);
            return lm;
        }

        private static void CreateStageInput(Camera cam, LiquidGun gun)
        {
            var inputGO = new GameObject("StageInput");
            var input = inputGO.AddComponent<StageInputController>();
            SetRef(input, "stageCamera", cam);
            SetRef(input, "liquidGun", gun);
        }

        private static void CreateEventSystem()
        {
            // New Input System: use InputSystemUIInputModule so UI buttons receive events.
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ==================================================================================
        // UI-construction helpers
        // ==================================================================================

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text,
            float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label,
            Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 100f);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.9f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            TextMeshProUGUI tmp = CreateText("Label", go.transform, label, 36f,
                TextAlignmentOptions.Center);
            StretchToParent((RectTransform)tmp.transform);

            return button;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorCentered(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        // ==================================================================================
        // Data-loading helpers
        // ==================================================================================

        private static LoopTuningConfig LoadTuning()
        {
            return AssetDatabase.LoadAssetAtPath<LoopTuningConfig>(DataRoot + "/LoopTuning.asset");
        }

        private static LiquidDefinition LoadLiquid(string file)
        {
            return AssetDatabase.LoadAssetAtPath<LiquidDefinition>(
                DataRoot + "/Liquids/" + file + ".asset");
        }

        private static MaterialDefinition LoadMaterial(string file)
        {
            return AssetDatabase.LoadAssetAtPath<MaterialDefinition>(
                DataRoot + "/Materials/" + file + ".asset");
        }

        // ==================================================================================
        // Shared editor helpers (mirrors SliceOneBuilder; reload-after-save fixes stale refs)
        // ==================================================================================

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
                // instance, so wiring with the original variable would assign a stale (fake-null)
                // reference. The freshly loaded asset is a stable reference.
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

        private static void SetStringArray(SerializedObject so, string field, string[] values)
        {
            var p = so.FindProperty(field);
            if (p != null)
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static void SetFloat(SerializedObject so, string f, float v) { var p = so.FindProperty(f); if (p != null) p.floatValue = v; }
        private static void SetInt(SerializedObject so, string f, int v) { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        private static void SetString(SerializedObject so, string f, string v) { var p = so.FindProperty(f); if (p != null) p.stringValue = v; }
        private static void SetColor(SerializedObject so, string f, Color v) { var p = so.FindProperty(f); if (p != null) p.colorValue = v; }
        private static void SetEnum(SerializedObject so, string f, int v) { var p = so.FindProperty(f); if (p != null) p.enumValueIndex = v; }
    }
}

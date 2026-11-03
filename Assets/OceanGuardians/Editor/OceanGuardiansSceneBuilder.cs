using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.Player;
using OceanGuardians.Progression;
using OceanGuardians.UI;

namespace OceanGuardians.EditorTools
{
    /// <summary>
    /// One-click greybox: Ocean Guardians > Build Greybox Beach Scene.
    /// Creates the data assets (9 item types + 50-stage database), materials, wildlife prefabs,
    /// and a fully wired, NavMesh-baked, playable beach scene with the complete HUD.
    /// Safe to re-run: existing data assets/materials are reused, the scene is rebuilt.
    /// Swap primitives for real art afterwards - the gameplay components don't care.
    /// </summary>
    public static class OceanGuardiansSceneBuilder
    {
        const string Root = "Assets/OceanGuardians";
        const string DataDir = Root + "/Data";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Beach_Greybox.unity";

        static Material fxMaterial;
        static Sprite roundSprite, panelSprite;

        [MenuItem("Ocean Guardians/Build Greybox Beach Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!TmpEssentialsReady()) return; // Build re-runs itself once the import finishes
            foreach (var dir in new[] { DataDir, MatDir, PrefabDir, SceneDir }) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fxMaterial = FxMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            var sun = Object.FindAnyObjectByType<Light>();
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ---------- Static world (baked into the NavMesh) ----------
            var sand = Prim(PrimitiveType.Plane, "Sand", null, Vector3.zero, new Vector3(5f, 1f, 5f), Mat("Sand", new Color(0.78f, 0.68f, 0.5f)));
            var navSurface = sand.AddComponent<NavMeshSurface>();
            foreach (var (pos, size) in new[] { (new Vector3(-20f, 0f, 18f), 3f), (new Vector3(18f, 0f, -16f), 2.5f), (new Vector3(4f, 0f, 20f), 2f), (new Vector3(-14f, 0f, 4f), 1.6f) })
                Prim(PrimitiveType.Sphere, "Rock", null, pos, new Vector3(size, size * 0.6f, size), Mat("Rock", new Color(0.45f, 0.45f, 0.48f)));
            var kiosk = BuildKiosk(new Vector3(-10f, 0f, -8f));

            navSurface.BuildNavMesh();
            AssetDatabase.DeleteAsset(SceneDir + "/Beach_NavMesh.asset");
            AssetDatabase.CreateAsset(navSurface.navMeshData, SceneDir + "/Beach_NavMesh.asset");

            // ---------- Things that shouldn't carve the NavMesh ----------
            var water = Prim(PrimitiveType.Plane, "Water", null, new Vector3(0f, -0.15f, 45f), new Vector3(8f, 1f, 4f), Mat("Water", new Color(0.2f, 0.4f, 0.42f)));
            Object.DestroyImmediate(water.GetComponent<Collider>()); // clicks on water shouldn't count as ground

            var data = CreateData();
            var player = BuildPlayer(new Vector3(0f, 0f, -4f), cam);
            BuildCamera(cam, player.transform);
            var seal = BuildSeal(new Vector3(8f, 0f, 14f));
            BuildLitter(data);

            var zone = new GameObject("RestorationZone_SealCove").AddComponent<RestorationZone>();
            zone.transform.position = new Vector3(0f, 0f, 2f);
            Wire(zone, ("radius", 28f), ("sandRenderer", sand.GetComponent<Renderer>()), ("waterRenderer", water.GetComponent<Renderer>()));
            var wildlife = new SerializedObject(zone);
            var arr = wildlife.FindProperty("wildlifePrefabs");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = CritterPrefab("GhostCrab", new Color(0.9f, 0.8f, 0.62f), new Vector3(0.4f, 0.15f, 0.3f));
            arr.GetArrayElementAtIndex(1).objectReferenceValue = CritterPrefab("Sanderling", new Color(0.95f, 0.95f, 0.95f), new Vector3(0.25f, 0.3f, 0.4f));
            wildlife.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Managers ----------
            var game = new GameObject("_Game");
            Wire(game.AddComponent<GameManager>(), ("player", player));
            game.AddComponent<EconomyManager>();
            game.AddComponent<BagSystem>();
            Wire(game.AddComponent<ProgressionManager>(), ("database", data.database));
            game.AddComponent<OceanJournal>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            BuildHud(cam, player.transform, kiosk.transform, seal.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Ocean Guardians] Greybox beach built at " + ScenePath + ". Press Play and click to move!");
            // CLI: Unity -batchmode -executeMethod ...Build (no -quit, so the async TMP import can finish first)
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // =====================================================================
        // Data
        // =====================================================================

        class GameData
        {
            public EquipmentStageData database;
            public readonly List<(ItemData data, PrimitiveType shape, Vector3 scale, Color color)> items = new();
        }

        static GameData CreateData()
        {
            var d = new GameData();
            d.database = Asset<EquipmentStageData>("EquipmentStageDatabase", db => db.PopulateDefaults());

            void Litter(string name, RecycleBin bin, int credits, int slots, int stage, string journal, PrimitiveType shape, Vector3 scale, Color color)
            {
                var asset = Asset<LitterData>("Litter_" + name.Replace(" ", ""), l =>
                {
                    l.displayName = name; l.bin = bin; l.creditValue = credits; l.bagSlots = slots;
                    l.requiredStage = stage; l.journalEntryId = journal; l.popupColor = new Color(0.75f, 1f, 0.8f);
                });
                d.items.Add((asset, shape, scale, color));
            }
            void Find(string name, int coins, string journal, PrimitiveType shape, Vector3 scale, Color color)
            {
                var asset = Asset<CollectibleData>("Find_" + name.Replace(" ", ""), c =>
                {
                    c.displayName = name; c.coinValue = coins; c.journalEntryId = journal; c.popupColor = new Color(1f, 0.85f, 1f);
                });
                d.items.Add((asset, shape, scale, color));
            }

            Litter("Plastic Bottle", RecycleBin.Blue, 5, 1, 0, "plastic_bottle", PrimitiveType.Cylinder, new Vector3(0.18f, 0.25f, 0.18f), new Color(0.6f, 0.85f, 1f));
            Litter("Soda Can", RecycleBin.Green, 6, 1, 0, "aluminum_can", PrimitiveType.Cylinder, new Vector3(0.15f, 0.12f, 0.15f), new Color(0.9f, 0.2f, 0.2f));
            Litter("Snack Wrapper", RecycleBin.Blue, 3, 1, 0, "", PrimitiveType.Cube, new Vector3(0.35f, 0.03f, 0.2f), new Color(1f, 0.75f, 0.1f));
            Litter("Glass Jar", RecycleBin.Green, 6, 1, 0, "", PrimitiveType.Cylinder, new Vector3(0.22f, 0.15f, 0.22f), new Color(0.75f, 0.95f, 0.85f));
            Litter("Car Tire", RecycleBin.Yellow, 25, 3, 9, "", PrimitiveType.Cylinder, new Vector3(0.9f, 0.12f, 0.9f), new Color(0.12f, 0.12f, 0.12f));
            Litter("Driftwood Clump", RecycleBin.Yellow, 2, 1, 0, "", PrimitiveType.Cube, new Vector3(0.9f, 0.15f, 0.2f), new Color(0.55f, 0.4f, 0.25f));
            Find("Sea Glass", 3, "sea_glass", PrimitiveType.Sphere, new Vector3(0.15f, 0.08f, 0.12f), new Color(0.3f, 0.85f, 0.7f));
            Find("Scallop Shell", 1, "mollusk_shells", PrimitiveType.Sphere, new Vector3(0.25f, 0.06f, 0.22f), new Color(1f, 0.7f, 0.65f));
            Find("Conch Shell", 2, "mollusk_shells", PrimitiveType.Capsule, new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.82f, 0.7f));
            return d;
        }

        static T Asset<T>(string name, System.Action<T> init) where T : ScriptableObject
        {
            string path = $"{DataDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // =====================================================================
        // World
        // =====================================================================

        static GameObject BuildPlayer(Vector3 pos, Camera cam)
        {
            var root = new GameObject("Coral");
            root.transform.position = pos;
            // Chibi greybox: big head, auburn curls, teal cap, round glasses, explorer backpack.
            Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.65f, 0.5f, 0.6f), Mat("Coral_Shirt", new Color(0.2f, 0.65f, 0.7f)));
            Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.25f, 0f), Vector3.one * 0.62f, Mat("Coral_Skin", new Color(1f, 0.85f, 0.72f)));
            Prim(PrimitiveType.Sphere, "Hair", root.transform, new Vector3(0f, 1.35f, -0.08f), new Vector3(0.82f, 0.7f, 0.78f), Mat("Coral_Hair", new Color(0.65f, 0.22f, 0.12f)));
            Prim(PrimitiveType.Cylinder, "Cap", root.transform, new Vector3(0f, 1.62f, 0.04f), new Vector3(0.66f, 0.05f, 0.66f), Mat("Coral_Cap", new Color(0.1f, 0.7f, 0.68f)));
            Prim(PrimitiveType.Cube, "Glasses", root.transform, new Vector3(0f, 1.27f, 0.3f), new Vector3(0.42f, 0.1f, 0.04f), Mat("Coral_Glasses", new Color(0.15f, 0.15f, 0.2f)));
            Prim(PrimitiveType.Cube, "Backpack", root.transform, new Vector3(0f, 0.6f, -0.36f), new Vector3(0.45f, 0.5f, 0.22f), Mat("Coral_Backpack", new Color(0.75f, 0.6f, 0.35f)));
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c); // clicks pass through Coral

            var agent = root.AddComponent<NavMeshAgent>();
            agent.baseOffset = 0f; agent.radius = 0.35f; agent.height = 1.7f;
            agent.speed = 5f; agent.angularSpeed = 720f; agent.acceleration = 24f; agent.stoppingDistance = 0.05f;

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var interaction = root.AddComponent<PlayerInteraction>();
            Wire(interaction, ("audioSource", audio));
            var controller = root.AddComponent<PlayerController>();
            Wire(controller, ("cam", cam), ("markerMaterial", fxMaterial));
            return root;
        }

        static void BuildCamera(Camera cam, Transform player)
        {
            cam.gameObject.AddComponent<CinemachineBrain>();
            cam.backgroundColor = new Color(0.55f, 0.82f, 0.95f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // 55 degree pitch, fixed rotation; CinemachineFollow in world space keeps the offset (no orbiting).
            const float pitch = 55f, distance = 24f;
            var offset = new Vector3(0f, Mathf.Sin(pitch * Mathf.Deg2Rad), -Mathf.Cos(pitch * Mathf.Deg2Rad)) * distance;
            var go = new GameObject("CM_IsoCamera");
            go.transform.SetPositionAndRotation(player.position + offset, Quaternion.Euler(pitch, 0f, 0f));
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = player;
            vcam.Lens.FieldOfView = 35f;
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.8f, 0.8f, 0.8f);
        }

        static GameObject BuildKiosk(Vector3 pos)
        {
            var root = new GameObject("RecyclingKiosk");
            root.transform.position = pos;
            Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(3.3f, 0.2f, 1.4f), Mat("Kiosk_Base", new Color(0.9f, 0.9f, 0.88f)));
            Prim(PrimitiveType.Cube, "Bin_Blue_Plastics", root.transform, new Vector3(-1.05f, 0.75f, 0f), new Vector3(0.9f, 1.1f, 0.9f), Mat("Bin_Blue", new Color(0.2f, 0.5f, 0.95f)));
            Prim(PrimitiveType.Cube, "Bin_Green_Metals", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.9f, 1.1f, 0.9f), Mat("Bin_Green", new Color(0.25f, 0.75f, 0.3f)));
            Prim(PrimitiveType.Cube, "Bin_Yellow_Paper", root.transform, new Vector3(1.05f, 0.75f, 0f), new Vector3(0.9f, 1.1f, 0.9f), Mat("Bin_Yellow", new Color(1f, 0.82f, 0.2f)));
            Prim(PrimitiveType.Cube, "Sign", root.transform, new Vector3(0f, 2.2f, -0.3f), new Vector3(2.6f, 0.7f, 0.1f), Mat("Kiosk_Sign", new Color(0.1f, 0.6f, 0.6f)));
            Prim(PrimitiveType.Cylinder, "SignPost", root.transform, new Vector3(0f, 1.4f, -0.3f), new Vector3(0.12f, 0.7f, 0.12f), Mat("Kiosk_Base", Color.white));
            root.AddComponent<RecyclingKiosk>();
            return root;
        }

        static GameObject BuildSeal(Vector3 pos)
        {
            var root = new GameObject("SealPup_Rescue");
            root.transform.position = pos;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.5f, 0.6f), Mat("Seal", new Color(0.62f, 0.62f, 0.66f))).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 0.45f, 0.5f), Vector3.one * 0.45f, Mat("Seal", Color.gray));

            var net = new GameObject("GhostNet").transform;
            net.SetParent(root.transform, false);
            var netMat = Mat("GhostNet", new Color(0.25f, 0.45f, 0.3f));
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Strand", net, new Vector3(0f, 0.35f, -0.2f + i * 0.2f), new Vector3(0.03f, 0.45f, 0.03f), netMat).transform.localRotation = Quaternion.Euler(0f, 0f, 90f + (i % 2 == 0 ? 25f : -25f));
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var aura = new GameObject("CareAura");
            aura.transform.SetParent(root.transform, false);
            aura.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.03f, 0f), Quaternion.Euler(90f, 0f, 0f));
            aura.transform.localScale = Vector3.one * 3f;
            var sr = aura.AddComponent<SpriteRenderer>();
            sr.sprite = roundSprite;
            sr.color = new Color(1f, 0.55f, 0.75f, 0.45f);

            var hearts = Particles(root.transform, "HeartBurst", new Color(1f, 0.4f, 0.6f), loop: false, rate: 0f, burst: 30, size: 0.3f, speed: 3f, lifetime: 1.2f);
            hearts.transform.localPosition = Vector3.up;

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 1f; col.center = new Vector3(0f, 0.4f, 0f);
            Wire(root.AddComponent<WildlifeRescue>(), ("netObject", net.gameObject), ("auraObject", aura), ("heartBurst", hearts), ("animalVisual", visual));
            return root;
        }

        static void BuildLitter(GameData data)
        {
            var parent = new GameObject("Litter").transform;
            var rng = new System.Random(7);
            var avoid = new[] { new Vector3(-10f, 0f, -8f), new Vector3(0f, 0f, -4f), new Vector3(8f, 0f, 14f) }; // kiosk, spawn, seal

            // 3 of each litter type (only 1 tire), 2 of each natural find.
            foreach (var (item, shape, scale, color) in data.items)
            {
                int count = item is CollectibleData ? 2 : item.displayName == "Car Tire" ? 1 : 3;
                for (int i = 0; i < count; i++)
                {
                    Vector3 p;
                    do p = new Vector3((float)rng.NextDouble() * 40f - 20f, 0f, (float)rng.NextDouble() * 40f - 18f);
                    while (avoid.Any(a => Vector3.Distance(a, p) < 4f) || !NavMesh.SamplePosition(p, out _, 0.3f, NavMesh.AllAreas));

                    var root = new GameObject(item.name);
                    root.transform.SetParent(parent);
                    root.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    var vis = Prim(shape, "Visual", root.transform, new Vector3(0f, scale.y + 0.02f, 0f), scale, Mat("Item_" + item.name, color));
                    if (shape is PrimitiveType.Cylinder or PrimitiveType.Capsule && item.displayName is "Plastic Bottle" or "Conch Shell")
                    {
                        vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // lying on its side
                        vis.transform.localPosition = new Vector3(0f, scale.x * 0.5f + 0.02f, 0f);
                    }
                    Object.DestroyImmediate(vis.GetComponent<Collider>());
                    var col = root.AddComponent<SphereCollider>();
                    col.isTrigger = true; col.radius = 0.7f; col.center = new Vector3(0f, 0.3f, 0f); // big tap target
                    Particles(root.transform, "Sparkle", item is CollectibleData ? new Color(1f, 0.85f, 1f) : new Color(1f, 1f, 0.75f),
                        loop: true, rate: 4f, burst: 0, size: 0.12f, speed: 0.4f, lifetime: 0.9f).transform.localPosition = Vector3.up * 0.3f;
                    Wire(root.AddComponent<LitterItem>(), ("data", item));
                }
            }
        }

        static GameObject CritterPrefab(string name, Color color, Vector3 scale)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var root = new GameObject(name);
            Prim(PrimitiveType.Sphere, "Body", root.transform, new Vector3(0f, scale.y, 0f), scale, Mat("Critter_" + name, color));
            Prim(PrimitiveType.Sphere, "Eye", root.transform, new Vector3(0.08f, scale.y * 1.6f, scale.z * 0.4f), Vector3.one * 0.06f, Mat("Critter_Eye", Color.black));
            Prim(PrimitiveType.Sphere, "Eye", root.transform, new Vector3(-0.08f, scale.y * 1.6f, scale.z * 0.4f), Vector3.one * 0.06f, Mat("Critter_Eye", Color.black));
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // =====================================================================
        // HUD
        // =====================================================================

        static void BuildHud(Camera cam, Transform player, Transform kiosk, Transform seal)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = (RectTransform)canvasGo.transform;

            // Floating text sits underneath everything else.
            var floatLayer = Rect("FloatingTextLayer", hud, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- Top bar ----
            var top = Panel("TopBar", hud, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(0f, 90f), new Color(0f, 0.2f, 0.3f, 0.55f));
            var credits = Pill(top, "Credits", new Vector2(190f, 0f), new Color(0.2f, 0.5f, 0.95f), "Credits: 0");
            var coins = Pill(top, "Finds", new Vector2(520f, 0f), new Color(0.7f, 0.4f, 0.85f), "Finds: 0");
            var eco = Pill(top, "Eco", new Vector2(850f, 0f), new Color(0.25f, 0.7f, 0.35f), "Eco: 0");

            // ---- Bag gauge (bottom-left) ----
            var bag = Panel("BagWidget", hud, Vector2.zero, Vector2.zero, new Vector2(220f, 70f), new Vector2(380f, 90f), new Color(0f, 0.2f, 0.3f, 0.7f));
            var bagIcon = Panel("BagIcon", bag, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(45f, 0f), new Vector2(60f, 60f), new Color(0.75f, 0.6f, 0.35f));
            Text("Label", bagIcon, "BAG", 18, Color.white);
            var bagBg = Panel("GaugeBg", bag, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(210f, -12f), new Vector2(260f, 26f), new Color(0f, 0f, 0f, 0.4f));
            var bagFill = Panel("GaugeFill", bagBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.35f, 0.85f, 0.4f));
            bagFill.type = Image.Type.Filled;
            bagFill.fillMethod = Image.FillMethod.Horizontal;
            bagFill.sprite = null;
            var bagText = Text("BagCount", bag, "0 / 10", 30, Color.white);
            Place(bagText.rectTransform, new Vector2(0f, 0.5f), new Vector2(210f, 20f), new Vector2(260f, 40f));

            // ---- Minimap (top-right) ----
            var mapRing = Panel("MinimapRing", hud, Vector2.one, Vector2.one, new Vector2(-160f, -235f), new Vector2(270f, 270f), new Color(0.95f, 0.95f, 0.9f), roundSprite);
            var map = Panel("Minimap", mapRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(255f, 255f), new Color(0.85f, 0.78f, 0.6f), roundSprite);
            map.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var mapPlayer = Panel("PlayerPin", map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Color(1f, 0.45f, 0.3f), roundSprite);
            var kioskPin = Panel("KioskPin", map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Color(0.2f, 0.5f, 0.95f), panelSprite);
            var sealPin = Panel("RescuePin", map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Color(1f, 0.4f, 0.65f), roundSprite);

            // ---- Alert (top-centre) ----
            var alert = Text("Alert", hud, "", 34, new Color(1f, 0.97f, 0.85f));
            Place(alert.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1100f, 70f));
            alert.fontStyle = FontStyles.Bold;
            alert.outlineWidth = 0.2f;

            // ---- Context prompts (bottom-right) ----
            var grabber = Panel("GrabberPrompt", hud, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 70f), new Vector2(220f, 70f), new Color(0.25f, 0.7f, 0.35f, 0.9f));
            Text("Label", grabber.rectTransform, "Grabber", 30, Color.white);
            var care = Btn("CareButton", hud, "Care <3", new Color(1f, 0.45f, 0.65f), new Vector2(1f, 0f), new Vector2(-150f, 165f), new Vector2(220f, 80f));

            // ---- Polaroid (right side) ----
            var polaroidCard = Panel("PolaroidCard", hud, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210f, -30f), new Vector2(360f, 470f), Color.white);
            polaroidCard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var group = polaroidCard.gameObject.AddComponent<CanvasGroup>();
            var photo = Panel("Photo", polaroidCard.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(320f, 210f), new Color(0.45f, 0.8f, 0.85f));
            var pTitle = Text("Title", polaroidCard.rectTransform, "", 24, new Color(0.15f, 0.2f, 0.25f));
            Place(pTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(330f, 70f));
            var pCat = Text("Category", polaroidCard.rectTransform, "", 18, new Color(0.1f, 0.55f, 0.55f));
            Place(pCat.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(330f, 26f));
            var pFact = Text("Fact", polaroidCard.rectTransform, "", 18, new Color(0.2f, 0.2f, 0.25f));
            Place(pFact.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(320f, 110f));
            pFact.alignment = TextAlignmentOptions.Top;
            Wire(canvasGo.AddComponent<PolaroidPopup>(), ("card", polaroidCard.rectTransform), ("group", group), ("photo", photo),
                ("titleText", pTitle), ("categoryText", pCat), ("factText", pFact));

            // ---- Kiosk modal ----
            var modal = Panel("KioskPanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 640f), new Color(0.97f, 0.97f, 0.94f, 0.97f));
            var mt = modal.rectTransform;
            var header = Text("Header", mt, "Recycling Kiosk", 40, new Color(0.1f, 0.4f, 0.45f));
            Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(700f, 60f));
            header.fontStyle = FontStyles.Bold;
            var close = Btn("Close", mt, "X", new Color(0.85f, 0.35f, 0.35f), Vector2.one, new Vector2(-45f, -45f), new Vector2(60f, 60f));
            var depositTab = Btn("DepositTab", mt, "Deposit", new Color(0.2f, 0.6f, 0.65f), new Vector2(0.5f, 1f), new Vector2(-130f, -115f), new Vector2(240f, 56f));
            var shopTab = Btn("ShopTab", mt, "Upgrade Shop", new Color(0.2f, 0.6f, 0.65f), new Vector2(0.5f, 1f), new Vector2(130f, -115f), new Vector2(240f, 56f));

            var depositPage = Rect("DepositPage", mt, Vector2.zero, Vector2.one, new Vector2(0f, -80f), new Vector2(-60f, -220f));
            Pill(depositPage, "BlueBin", new Vector2(-280f, 120f), new Color(0.2f, 0.5f, 0.95f), "Blue: Plastics", 0.5f);
            Pill(depositPage, "GreenBin", new Vector2(0f, 120f), new Color(0.25f, 0.7f, 0.3f), "Green: Metals & Glass", 0.5f);
            Pill(depositPage, "YellowBin", new Vector2(280f, 120f), new Color(0.95f, 0.75f, 0.15f), "Yellow: Paper & Wood", 0.5f);
            var deposit = Btn("QuickDeposit", depositPage, "Quick Deposit All", new Color(0.25f, 0.75f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(460f, 100f));
            var result = Text("Result", depositPage, "", 28, new Color(0.2f, 0.25f, 0.3f));
            Place(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(800f, 100f));

            var shopPage = Rect("ShopPage", mt, Vector2.zero, Vector2.one, new Vector2(0f, -80f), new Vector2(-60f, -220f));
            var nextText = Text("NextStage", shopPage, "", 24, new Color(0.15f, 0.2f, 0.25f));
            Place(nextText.rectTransform, new Vector2(0f, 1f), new Vector2(230f, -60f), new Vector2(440f, 120f));
            nextText.alignment = TextAlignmentOptions.TopLeft;
            var buy = Btn("BuyNext", shopPage, "Buy Upgrade", new Color(0.25f, 0.75f, 0.35f), new Vector2(0f, 1f), new Vector2(160f, -170f), new Vector2(300f, 70f));
            var listText = ScrollingText(shopPage, out _);

            Wire(canvasGo.AddComponent<HUDController>(),
                ("creditsText", credits), ("coinsText", coins), ("ecoText", eco),
                ("bagFill", bagFill), ("bagText", bagText),
                ("alertText", alert),
                ("grabberPrompt", grabber.gameObject), ("careButton", care),
                ("floatingTextLayer", floatLayer), ("worldCamera", cam),
                ("kioskPanel", modal.gameObject), ("depositTabButton", depositTab), ("shopTabButton", shopTab), ("closeButton", close),
                ("depositPage", depositPage.gameObject), ("shopPage", shopPage.gameObject),
                ("quickDepositButton", deposit), ("depositResultText", result),
                ("nextStageText", nextText), ("buyButton", buy), ("stageListText", listText),
                ("minimapRoot", map.rectTransform));

            var so = new SerializedObject(canvasGo.GetComponent<HUDController>());
            var pins = so.FindProperty("minimapPins");
            pins.arraySize = 2;
            void Pin(int i, Transform t, Image icon)
            {
                var e = pins.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("target").objectReferenceValue = t;
                e.FindPropertyRelative("icon").objectReferenceValue = icon.rectTransform;
            }
            Pin(0, kiosk, kioskPin);
            Pin(1, seal, sealPin);
            so.ApplyModifiedPropertiesWithoutUndo();
            mapPlayer.transform.SetAsLastSibling();
        }

        static TMP_Text ScrollingText(RectTransform parent, out ScrollRect scroll)
        {
            var view = Panel("StageList", parent, new Vector2(1f, 0f), Vector2.one, new Vector2(-210f, 0f), new Vector2(400f, 0f), new Color(0f, 0.2f, 0.3f, 0.08f));
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            var text = Text("Content", view.rectTransform, "", 20, Color.black);
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(-20f, 0f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rt;
            scroll.viewport = view.rectTransform;
            return text;
        }

        // =====================================================================
        // Small builders
        // =====================================================================

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Soft round alpha-blended material shared by sparkles, hearts and the click ripple.</summary>
        static Material FxMaterial()
        {
            string path = $"{MatDir}/FX_Soft.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Sprites/Default"));
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            m.mainTexture = tex ? tex : roundSprite.texture;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static ParticleSystem Particles(Transform parent, string name, Color color, bool loop, float rate, int burst, float size, float speed, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = loop;
            main.playOnAwake = loop;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = loop ? 0f : -0.3f; // hearts float up
            var emission = ps.emission;
            emission.rateOverTime = rate;
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = fxMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        static RectTransform Rect(string name, Component parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent.transform, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static Image Panel(string name, Component parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
        {
            var img = Rect(name, parent, anchorMin, anchorMax, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite ? sprite : panelSprite;
            img.type = img.sprite == panelSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(string name, Component parent, string text, float size, Color color)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        static TMP_Text Pill(Component parent, string name, Vector2 pos, Color color, string label, float anchorX = 0f)
        {
            var img = Panel(name, parent, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), pos, new Vector2(300f, 62f), color);
            var t = Text("Label", img.rectTransform, label, 28, Color.white);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        static Button Btn(string name, Component parent, string label, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Panel(name, parent, anchor, anchor, pos, size, color);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var t = Text("Label", img.rectTransform, label, 30, Color.white);
            t.fontStyle = FontStyles.Bold;
            return b;
        }

        /// <summary>Assign private [SerializeField]s by name, the same way the Inspector does.</summary>
        static void Wire(Object target, params (string name, object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (name, value) in fields)
            {
                var p = so.FindProperty(name);
                if (p == null) { Debug.LogError($"[Ocean Guardians] {target.GetType().Name}.{name} not found"); continue; }
                switch (value)
                {
                    case Object o: p.objectReferenceValue = o; break;
                    case float f: p.floatValue = f; break;
                    case int i: p.intValue = i; break;
                    case string s: p.stringValue = s; break;
                    case Color c: p.colorValue = c; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// TextMeshPro needs its essential resources (default font) before any text can be created.
        /// The package import is asynchronous, so if they're missing we start it and re-run Build when it completes.
        /// </summary>
        static bool TmpEssentialsReady()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
            var pkg = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (pkg == null)
            {
                Debug.LogError("[Ocean Guardians] Import TMP Essentials via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
                return false;
            }
            Debug.Log("[Ocean Guardians] Importing TMP Essential Resources first...");
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(pkg, false);
            return false;
        }

        static void OnTmpImported(string _)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Build;
        }
    }
}

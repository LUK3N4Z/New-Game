using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor.AssetPackage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using OceanGuardians.CameraControl;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.Player;
using OceanGuardians.Progression;
using OceanGuardians.UI;
using OceanGuardians.Quests;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        const string ArtDir = Root + "/Art";

        static Material fxMaterial, glintMaterial, glintFindMaterial;
        static Sprite roundSprite, panelSprite;

        [MenuItem("Ocean Guardians/Build Greybox Beach Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!TmpEssentialsReady()) return; // Build re-runs itself once the import finishes
            foreach (var dir in new[] { DataDir, MatDir, PrefabDir, SceneDir, ArtDir + "/Characters/Coral", ArtDir + "/Environment", ArtDir + "/Items", ArtDir + "/Creatures", ArtDir + "/Shaders" }) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fxMaterial = FxMaterial();
            glintMaterial = GlintMaterial("FX_Glint", new Color(1.4f, 1.3f, 0.9f));
            glintFindMaterial = GlintMaterial("FX_Glint_Find", new Color(0.9f, 1.3f, 1.4f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            var sun = Object.FindAnyObjectByType<Light>();
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ---------- Static world (baked into the NavMesh) ----------
            var sand = Prim(PrimitiveType.Plane, "Sand", null, Vector3.zero, new Vector3(5f, 1f, 5f), Mat("Sand", new Color(0.78f, 0.68f, 0.5f)));
            var navSurface = sand.AddComponent<NavMeshSurface>();
            var rocks = new List<Transform>();
            foreach (var (pos, size) in new[] { (new Vector3(-20f, 0f, 18f), 3f), (new Vector3(18f, 0f, -16f), 2.5f), (new Vector3(4f, 0f, 20f), 2f), (new Vector3(-14f, 0f, 4f), 1.6f) })
                rocks.Add(Prim(PrimitiveType.Sphere, "Rock", null, pos, new Vector3(size, size * 0.6f, size), Mat("Rock", new Color(0.45f, 0.45f, 0.48f))).transform);
            // Tall bluff between the usual camera position and the beach: shows the camera deoccluder at work.
            Prim(PrimitiveType.Cube, "RockBluff", null, new Vector3(12f, 3f, -21f), new Vector3(9f, 6f, 4f), Mat("Rock", Color.gray)).transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            var kiosk = BuildKiosk(new Vector3(-10f, 0f, -8f));

            navSurface.BuildNavMesh();
            AssetDatabase.DeleteAsset(SceneDir + "/Beach_NavMesh.asset");
            AssetDatabase.CreateAsset(navSurface.navMeshData, SceneDir + "/Beach_NavMesh.asset");

            // ---------- Things that shouldn't carve the NavMesh ----------
            var water = Prim(PrimitiveType.Plane, "Water", null, new Vector3(0f, -0.15f, 45f), new Vector3(8f, 1f, 4f), WaterMaterial());
            water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Sloped seabed under the shallows so the water shader's depth ramp/foam have real depth to read.
            var seabed = Prim(PrimitiveType.Plane, "Seabed", null, new Vector3(0f, -1.7f, 42f), new Vector3(8f, 1f, 4f), Mat("Seabed", new Color(0.72f, 0.64f, 0.48f)));
            seabed.transform.rotation = Quaternion.Euler(4.5f, 0f, 0f); // rises toward the shore
            Object.DestroyImmediate(seabed.GetComponent<Collider>());
            Object.DestroyImmediate(water.GetComponent<Collider>()); // clicks on water shouldn't count as ground
            BuildWaves();
            foreach (var pos in new[] { new Vector3(-7f, 0f, 21f), new Vector3(13f, 0f, 20f) })
            {
                var pool = Prim(PrimitiveType.Cylinder, "Tidepool", null, pos + Vector3.up * 0.01f, new Vector3(2.6f, 0.01f, 1.8f), Mat("Tidepool", new Color(0.25f, 0.55f, 0.6f)));
                Object.DestroyImmediate(pool.GetComponent<Collider>());
                pool.AddComponent<WaterZone>(); // splashy footsteps
                rocks.Add(pool.transform); // crab habitat
            }

            var data = CreateData();
            var player = BuildPlayer(new Vector3(0f, 0f, -4f), cam);
            BuildCamera(cam, player.transform);
            var seal = BuildSeal(new Vector3(8f, 0f, 14f));
            var campLights = BuildCamp(new Vector3(-18f, 0f, -16f), data);
            BuildRegionGate(new Vector3(20f, 0f, 21f));
            BuildLitter(data);

            var zone = new GameObject("RestorationZone_SealCove").AddComponent<RestorationZone>();
            zone.transform.position = new Vector3(0f, 0f, 2f);
            Wire(zone, ("radius", 28f), ("sandRenderer", sand.GetComponent<Renderer>()), ("waterRenderer", water.GetComponent<Renderer>()));
            WireArray(zone, "wildlifePrefabs",
                CritterPrefab("GhostCrab", AmbientCreature.Species.GhostCrab, new Color(0.9f, 0.8f, 0.62f), new Vector3(0.4f, 0.15f, 0.3f)),
                CritterPrefab("Sanderling", AmbientCreature.Species.Sanderling, new Color(0.95f, 0.95f, 0.95f), new Vector3(0.25f, 0.3f, 0.4f)));
            WireArray(zone, "habitatPoints", rocks.ToArray());

            // ---------- Managers ----------
            var game = new GameObject("_Game");
            Wire(game.AddComponent<GameManager>(), ("player", player));
            game.AddComponent<EconomyManager>();
            game.AddComponent<BagSystem>();
            Wire(game.AddComponent<ProgressionManager>(), ("database", data.database));
            game.AddComponent<OceanJournal>();
            WireArray(game.AddComponent<SaveManager>(), "itemCatalog", data.items.Select(i => (Object)i.data).ToArray());
            WireArray(game.AddComponent<QuestManager>(), "questCatalog", CreateQuestAssets().Select(q => (Object)q).ToArray());
            Wire(game.AddComponent<OceanGuardians.Audio.AudioManager>(), ("wavesEmitter", (Object)Object.FindAnyObjectByType<ShoreWaves>().transform));
            game.AddComponent<DayStats>();
            var cycle = game.AddComponent<DayNightCycle>();
            Wire(cycle, ("sun", sun));
            WireArray(cycle, "lanterns", campLights.Select(l => (Object)l).ToArray());
            var cove = new GameObject("QuestArea_WildlifeCove") { transform = { position = seal.transform.position } };
            Wire(cove.AddComponent<QuestAreaTrigger>(), ("areaId", "wildlife_cove"), ("radius", 6f), ("player", player.transform));

            BuildPostProcessing(cam, sun);
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

        [MenuItem("Ocean Guardians/Delete Save File")]
        public static void DeleteSave()
        {
            string path = Path.Combine(Application.persistentDataPath, "savegame.json");
            if (File.Exists(path)) File.Delete(path);
            Debug.Log("[Ocean Guardians] Save file deleted: " + path);
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

            void Litter(string name, RecycleBin bin, int credits, int stack, int stage, string journal, PrimitiveType shape, Vector3 scale, Color color)
            {
                var asset = Asset<LitterItemData>("Litter_" + name.Replace(" ", ""), l =>
                {
                    l.SetId(name.ToLowerInvariant().Replace(' ', '_'));
                    l.displayName = name; l.bin = bin; l.creditValue = credits; l.maxStack = stack;
                    l.requiredStage = stage; l.journalEntryId = journal; l.popupColor = new Color(0.75f, 1f, 0.8f);
                });
                d.items.Add((asset, shape, scale, color));
            }
            void Find(string name, int coins, string journal, PrimitiveType shape, Vector3 scale, Color color)
            {
                var asset = Asset<CollectibleItemData>("Find_" + name.Replace(" ", ""), c =>
                {
                    c.SetId(name.ToLowerInvariant().Replace(' ', '_'));
                    c.maxStack = 99;
                    c.displayName = name; c.coinValue = coins; c.journalEntryId = journal; c.popupColor = new Color(1f, 0.85f, 1f);
                });
                d.items.Add((asset, shape, scale, color));
            }

            Litter("Plastic Bottle", RecycleBin.Blue, 5, 3, 0, "plastic_bottle", PrimitiveType.Cylinder, new Vector3(0.18f, 0.25f, 0.18f), new Color(0.6f, 0.85f, 1f));
            Litter("Soda Can", RecycleBin.Green, 6, 4, 0, "aluminum_can", PrimitiveType.Cylinder, new Vector3(0.15f, 0.12f, 0.15f), new Color(0.9f, 0.2f, 0.2f));
            Litter("Snack Wrapper", RecycleBin.Blue, 3, 5, 0, "", PrimitiveType.Cube, new Vector3(0.35f, 0.03f, 0.2f), new Color(1f, 0.75f, 0.1f));
            Litter("Glass Jar", RecycleBin.Green, 6, 2, 0, "", PrimitiveType.Cylinder, new Vector3(0.22f, 0.15f, 0.22f), new Color(0.75f, 0.95f, 0.85f));
            Litter("Car Tire", RecycleBin.Yellow, 25, 1, 9, "", PrimitiveType.Cylinder, new Vector3(0.9f, 0.12f, 0.9f), new Color(0.12f, 0.12f, 0.12f));
            Litter("Driftwood Clump", RecycleBin.Yellow, 2, 2, 0, "", PrimitiveType.Cube, new Vector3(0.9f, 0.15f, 0.2f), new Color(0.55f, 0.4f, 0.25f));
            Litter("Ghost Net", RecycleBin.Blue, 8, 2, 0, "ghost_gear", PrimitiveType.Sphere, new Vector3(0.55f, 0.25f, 0.55f), new Color(0.35f, 0.5f, 0.45f)); // crafting ingredient (Ocean Sunglasses)
            Find("Sea Glass", 3, "sea_glass", PrimitiveType.Sphere, new Vector3(0.15f, 0.08f, 0.12f), new Color(0.3f, 0.85f, 0.7f));
            Find("Scallop Shell", 1, "mollusk_shells", PrimitiveType.Sphere, new Vector3(0.25f, 0.06f, 0.22f), new Color(1f, 0.7f, 0.65f));
            Find("Conch Shell", 2, "mollusk_shells", PrimitiveType.Capsule, new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.82f, 0.7f));
            return d;
        }

        /// <summary>Authors the 3 tutorial QuestData assets from QuestManager's built-in definitions.</summary>
        static List<QuestData> CreateQuestAssets() =>
            QuestManager.CreateTutorialQuests().Select(t => Asset<QuestData>(t.name, q =>
            {
                q.questId = t.questId; q.questTitle = t.questTitle; q.questDescription = t.questDescription;
                q.rewardCredits = t.rewardCredits; q.rewardCollectorCoins = t.rewardCollectorCoins;
                q.rewardEcoTokens = t.rewardEcoTokens; q.rewardJournalEntryId = t.rewardJournalEntryId;
                q.nextQuestId = t.nextQuestId;
                q.steps.AddRange(t.steps.Select(s => s.Clone()));
            })).ToList();

        /// <summary>Authors the 3 starter upcycling recipes, resolving ingredients from the item assets.</summary>
        static List<Crafting.CraftingRecipeData> CreateRecipeAssets(GameData data)
        {
            ItemData ById(string id) => data.items.Select(i => i.data).First(i => i.Id == id);
            Crafting.InventoryCost Cost(string id, int n) => new() { item = ById(id), quantity = n };
            Crafting.CraftingRecipeData Make(string file, string id, string name, string desc, int coins, int eco,
                Crafting.InventoryCost[] litter, Crafting.InventoryCost[] finds) =>
                Asset<Crafting.CraftingRecipeData>(file, r =>
                {
                    r.recipeId = id; r.recipeName = name; r.recipeDescription = desc;
                    r.rewardCollectorCoins = coins; r.rewardEcoTokens = eco;
                    r.requiredLitter.AddRange(litter); r.requiredFinds.AddRange(finds);
                });
            return new List<Crafting.CraftingRecipeData>
            {
                Make("Recipe_OceanSunglasses", "ocean_sunglasses", "Ocean Sunglasses", "Shade your eyes, save the seas.", 35, 15,
                    new[] { Cost("plastic_bottle", 3), Cost("ghost_net", 1) }, new Crafting.InventoryCost[0]),
                Make("Recipe_SeaGlassBracelet", "sea_glass_bracelet", "Sea Glass Bracelet", "Sparkly wrist treasure.", 50, 25,
                    new Crafting.InventoryCost[0], new[] { Cost("sea_glass", 2), Cost("scallop_shell", 2) }),
                Make("Recipe_CoastalEcoPlanter", "coastal_eco_planter", "Coastal Eco-Planter", "A home for dune flowers.", 40, 20,
                    new[] { Cost("soda_can", 2), Cost("driftwood_clump", 1) }, new Crafting.InventoryCost[0]),
            };
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
            // Phase 6 stylized Coral (character bible): auburn curl clusters, round glasses with
            // catchlit eyes, wave explorer cap, explorer vest, yellow gloves, notebook on her pack.
            // Everything hangs off "Visual" so ProceduralAnimator bobs/squashes the whole model.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var skin = Mat("Coral_Skin", new Color(1f, 0.85f, 0.72f));
            var hairMat = Mat("Coral_Hair", new Color(0.72f, 0.3f, 0.12f));
            var shirtMat = Mat("Coral_Shirt", new Color(0.15f, 0.72f, 0.7f));
            var vestMat = Mat("Coral_Vest", new Color(0.98f, 0.75f, 0.3f));
            var darkMat = Mat("Coral_Glasses", new Color(0.15f, 0.15f, 0.2f));
            var whiteMat = Mat("Coral_White", Color.white);
            var gloveMat = Mat("Coral_Gloves", new Color(1f, 0.8f, 0.15f));
            foreach (var x in new[] { -0.15f, 0.15f })
                Prim(PrimitiveType.Cylinder, "Leg", visual, new Vector3(x, 0.24f, 0f), new Vector3(0.16f, 0.22f, 0.16f), skin);
            Prim(PrimitiveType.Cube, "Shorts", visual, new Vector3(0f, 0.48f, 0f), new Vector3(0.5f, 0.26f, 0.42f), Mat("Coral_Shorts", new Color(0.25f, 0.4f, 0.75f)));
            Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.42f, 0.56f), shirtMat);
            Prim(PrimitiveType.Cube, "Vest", visual, new Vector3(0f, 0.82f, 0.1f), new Vector3(0.52f, 0.42f, 0.42f), vestMat);
            foreach (var side in new[] { -1f, 1f })
            {
                var sleeve = Prim(PrimitiveType.Cylinder, "Sleeve", visual, new Vector3(side * 0.36f, 0.92f, 0f), new Vector3(0.17f, 0.14f, 0.17f), whiteMat);
                sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
                var arm = Prim(PrimitiveType.Cylinder, "Arm", visual, new Vector3(side * 0.42f, 0.74f, 0.02f), new Vector3(0.12f, 0.2f, 0.12f), skin);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * 14f);
                Prim(PrimitiveType.Sphere, "Glove", visual, new Vector3(side * 0.47f, 0.54f, 0.04f), Vector3.one * 0.17f, gloveMat);
            }
            Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.32f, 0f), Vector3.one * 0.64f, skin);
            foreach (var side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "EyeWhite", visual, new Vector3(side * 0.16f, 1.36f, 0.26f), new Vector3(0.17f, 0.21f, 0.1f), whiteMat);
                Prim(PrimitiveType.Sphere, "Pupil", visual, new Vector3(side * 0.16f, 1.35f, 0.31f), Vector3.one * 0.085f, darkMat);
                Prim(PrimitiveType.Sphere, "Catchlight", visual, new Vector3(side * 0.135f, 1.4f, 0.34f), Vector3.one * 0.03f, whiteMat);
                Prim(PrimitiveType.Sphere, "Cheek", visual, new Vector3(side * 0.23f, 1.22f, 0.24f), new Vector3(0.13f, 0.08f, 0.05f), Mat("Coral_Cheek", new Color(1f, 0.62f, 0.55f)));
            }
            Prim(PrimitiveType.Sphere, "Nose", visual, new Vector3(0f, 1.28f, 0.32f), Vector3.one * 0.055f, skin);
            Prim(PrimitiveType.Sphere, "Mouth", visual, new Vector3(0f, 1.18f, 0.29f), new Vector3(0.14f, 0.05f, 0.05f), Mat("Coral_Mouth", new Color(0.55f, 0.25f, 0.2f)));
            var glassesMesh = MeshAsset("Characters/Coral", "GlassesRing", () => Torus(0.115f, 0.014f, 20));
            foreach (var side in new[] { -1f, 1f })
            {
                MeshPrim(glassesMesh, "GlassesRing", visual, new Vector3(side * 0.16f, 1.36f, 0.3f), Vector3.one, darkMat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var temple = Prim(PrimitiveType.Cylinder, "GlassesTemple", visual, new Vector3(side * 0.3f, 1.38f, 0.14f), new Vector3(0.025f, 0.16f, 0.025f), darkMat);
                temple.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var bridge = Prim(PrimitiveType.Cylinder, "GlassesBridge", visual, new Vector3(0f, 1.38f, 0.3f), new Vector3(0.025f, 0.08f, 0.025f), darkMat);
            bridge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Sphere, "HairTop", visual, new Vector3(0f, 1.56f, -0.05f), new Vector3(0.72f, 0.5f, 0.7f), hairMat);
            Prim(PrimitiveType.Sphere, "HairBack", visual, new Vector3(0f, 1.34f, -0.24f), new Vector3(0.66f, 0.62f, 0.4f), hairMat);
            foreach (var (x, y, z, s) in new[] { (-0.3f, 1.5f, 0.14f, 0.26f), (0.3f, 1.5f, 0.14f, 0.26f), (0f, 1.58f, 0.2f, 0.24f), (-0.34f, 1.28f, 0.05f, 0.24f), (0.34f, 1.28f, 0.05f, 0.24f), (-0.2f, 1.66f, -0.02f, 0.22f), (0.2f, 1.66f, -0.02f, 0.22f) })
                Prim(PrimitiveType.Sphere, "HairCurl", visual, new Vector3(x, y, z), Vector3.one * s, hairMat);
            Prim(PrimitiveType.Sphere, "CapDome", visual, new Vector3(0f, 1.64f, -0.03f), new Vector3(0.6f, 0.32f, 0.6f), Mat("Coral_Cap", new Color(0.1f, 0.7f, 0.68f)));
            Prim(PrimitiveType.Cube, "CapPanel", visual, new Vector3(0f, 1.62f, 0.24f), new Vector3(0.3f, 0.2f, 0.08f), whiteMat);
            Prim(PrimitiveType.Cylinder, "CapBill", visual, new Vector3(0f, 1.54f, 0.36f), new Vector3(0.52f, 0.03f, 0.34f), Mat("Coral_Cap", Color.white));
            Prim(PrimitiveType.Cylinder, "WaveLogo", visual, new Vector3(0f, 1.62f, 0.29f), new Vector3(0.13f, 0.02f, 0.13f), Mat("Coral_Cap", Color.white)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var backpackRoot = Prim(PrimitiveType.Cube, "Backpack", visual, new Vector3(0f, 0.82f, -0.34f), new Vector3(0.46f, 0.52f, 0.24f), Mat("Coral_Backpack", new Color(0.75f, 0.6f, 0.35f)));
            Prim(PrimitiveType.Cube, "Notebook", backpackRoot.transform, new Vector3(0.55f, 0f, 0f), new Vector3(0.1f, 0.6f, 0.75f), Mat("Coral_Notebook", new Color(0.95f, 0.5f, 0.35f)));
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c); // clicks pass through Coral
            var tong = Prim(PrimitiveType.Cylinder, "GrabberTong", root.transform, new Vector3(0.35f, 0.65f, 0.25f), new Vector3(0.05f, 0.4f, 0.05f), Mat("Coral_Cap", Color.white));
            Object.DestroyImmediate(tong.GetComponent<Collider>());

            var agent = root.AddComponent<NavMeshAgent>();
            agent.baseOffset = 0f; agent.radius = 0.35f; agent.height = 1.7f;
            agent.speed = 5f; agent.angularSpeed = 720f; agent.acceleration = 24f; agent.stoppingDistance = 0.05f;

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var interaction = root.AddComponent<PlayerInteraction>();
            Wire(interaction, ("audioSource", audio));
            var controller = root.AddComponent<PlayerController>();
            Wire(controller, ("cam", cam), ("markerMaterial", fxMaterial));
            Wire(root.AddComponent<ProceduralAnimator>(), ("visual", visual), ("grabberTong", tong.transform));
            // Modular gear looks swapped by PlayerVisualEquipment as stages unlock.
            var backpack1 = visual.Find("Backpack").gameObject;
            var backpack2 = Prim(PrimitiveType.Cube, "Backpack_Teal", visual, new Vector3(0f, 0.62f, -0.42f), new Vector3(0.59f, 0.66f, 0.3f) * 1.0f, Mat("Coral_Cap", Color.white));
            backpack2.transform.localScale = new Vector3(0.59f, 0.65f, 0.29f); // ~1.3x the stage-1 bag
            Object.DestroyImmediate(backpack2.GetComponent<Collider>());
            var pouchL = Prim(PrimitiveType.Cube, "PouchL", backpack2.transform, new Vector3(-0.55f, 0f, 0f), new Vector3(0.25f, 0.4f, 0.6f), Mat("Coral_Cap", Color.white));
            Object.DestroyImmediate(pouchL.GetComponent<Collider>());
            var pouchR = Prim(PrimitiveType.Cube, "PouchR", backpack2.transform, new Vector3(0.55f, 0f, 0f), new Vector3(0.25f, 0.4f, 0.6f), Mat("Coral_Cap", Color.white));
            Object.DestroyImmediate(pouchR.GetComponent<Collider>());
            backpack2.SetActive(false);
            GameObject Boots(string name, Color color)
            {
                var bootRoot = new GameObject(name);
                bootRoot.transform.SetParent(visual, false);
                foreach (var x in new[] { -0.16f, 0.16f })
                    Object.DestroyImmediate(Prim(PrimitiveType.Cube, "Boot", bootRoot.transform, new Vector3(x, 0.09f, 0.05f), new Vector3(0.2f, 0.18f, 0.34f), Mat(name, color)).GetComponent<Collider>());
                return bootRoot;
            }
            var boots1 = Boots("Boots_Brown", new Color(0.4f, 0.27f, 0.15f));
            var boots5 = Boots("Boots_Aqua", new Color(0.2f, 0.85f, 0.85f));
            boots5.SetActive(false);
            var sparkle = Particles(root.transform, "UpgradeSparkle", new Color(1f, 0.85f, 0.3f), false, 0f, 26, 0.22f, 2.2f, 0.9f);
            sparkle.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            Wire(root.AddComponent<PlayerVisualEquipment>(), ("bagStage1", backpack1), ("bagStage2", backpack2),
                ("bootsStage1", boots1), ("bootsStage5", boots5),
                ("grabberRenderer", tong.GetComponent<Renderer>()),
                ("tongStage1Material", Mat("Tong_Basic", new Color(0.55f, 0.4f, 0.2f))),
                ("tongStage3Material", Mat("Tong_Silver", new Color(0.85f, 0.88f, 0.92f))),
                ("visual", visual), ("upgradeSparkle", sparkle));
            return root;
        }

        static void BuildCamera(Camera cam, Transform player)
        {
            cam.gameObject.AddComponent<CinemachineBrain>();
            cam.backgroundColor = new Color(0.55f, 0.82f, 0.95f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // 55 degree pitch, fixed rotation; CinemachineFollow in world space keeps the offset (no orbiting).
            // IsoCameraRig drives zoom + look-ahead; CinemachineDeoccluder keeps rocks from blocking the view.
            const float pitch = 55f, distance = 24f;
            var offset = new Vector3(0f, Mathf.Sin(pitch * Mathf.Deg2Rad), -Mathf.Cos(pitch * Mathf.Deg2Rad)) * distance;
            var lead = new GameObject("CameraLeadTarget").transform;
            lead.position = player.position;
            var go = new GameObject("CM_IsoCamera");
            go.transform.SetPositionAndRotation(player.position + offset, Quaternion.Euler(pitch, 0f, 0f));
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = lead;
            vcam.Lens.FieldOfView = 35f;
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);

            var deoccluder = go.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = 1; // Default layer: sand, rocks, bluff (litter colliders are triggers and are ignored)
            deoccluder.MinimumDistanceFromTarget = 2f;
            var avoid = deoccluder.AvoidObstacles;
            avoid.Enabled = true;
            avoid.CameraRadius = 0.5f;
            avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            avoid.MaximumEffort = 4;
            avoid.SmoothingTime = 0.3f;
            avoid.Damping = 0.6f;              // ease back out once the obstacle is gone
            avoid.DampingWhenOccluded = 0.15f; // pull in quickly so Coral is never hidden
            deoccluder.AvoidObstacles = avoid;

            Wire(go.AddComponent<IsoCameraRig>(), ("player", player), ("leadTarget", lead), ("pitch", pitch), ("startDistance", distance));
        }

        static void BuildWaves()
        {
            var root = new GameObject("ShoreWaves");
            root.transform.position = new Vector3(0f, 0f, 25f); // the sand's far edge; forward (+Z) = out to sea
            var wash = Prim(PrimitiveType.Cube, "Wash", root.transform, Vector3.zero, new Vector3(50f, 0.02f, 1f), Mat("Wash", new Color(0.55f, 0.78f, 0.78f)));
            var foam = Prim(PrimitiveType.Cube, "Foam", root.transform, Vector3.zero, new Vector3(50f, 0.03f, 0.35f), Mat("Foam", new Color(0.97f, 0.98f, 0.97f)));
            foreach (var go in new[] { wash, foam })
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Wire(root.AddComponent<ShoreWaves>(), ("foam", foam.transform), ("wash", wash.transform), ("reach", 4.5f));
            // Phase 4 shoreline polish: undulating segmented foam + a wet-sand band that dries slowly.
            var wetBand = Prim(PrimitiveType.Cube, "WetSand", root.transform, Vector3.zero, new Vector3(50f, 0.015f, 1f), WetSandMaterial());
            Object.DestroyImmediate(wetBand.GetComponent<Collider>());
            wetBand.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var foamMat = Mat("Foam", new Color(0.97f, 0.98f, 0.97f));
            var segments = new List<Object>();
            for (int i = 0; i < 14; i++)
            {
                var seg = Prim(PrimitiveType.Cube, "FoamSeg", root.transform, new Vector3(-24f + i * 3.7f, 0.04f, 0f), new Vector3(3.9f, 0.03f, 0.4f), foamMat);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                segments.Add(seg.transform);
            }
            var shoreline = root.AddComponent<ShorelineWave>();
            Wire(shoreline, ("wetSandBand", wetBand.GetComponent<Renderer>()));
            WireArray(shoreline, "foamSegments", segments.ToArray());
        }

        static GameObject BuildKiosk(Vector3 pos)
        {
            // Phase 6 stylized kiosk: timber shelter, sloped green shake roof, three rounded
            // color-coded bins with pictogram discs, recycle signboard + upgrade notice board.
            var root = new GameObject("RecyclingKiosk");
            root.transform.position = pos;
            var wood = Mat("Kiosk_Wood", new Color(0.55f, 0.38f, 0.22f));
            var woodDark = Mat("Kiosk_WoodDark", new Color(0.42f, 0.28f, 0.16f));
            Prim(PrimitiveType.Cube, "Deck", root.transform, new Vector3(0f, 0.09f, 0.2f), new Vector3(4f, 0.18f, 2.2f), woodDark);
            foreach (var (x, z) in new[] { (-1.8f, 0.9f), (1.8f, 0.9f), (-1.8f, -0.6f), (1.8f, -0.6f) })
                Prim(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(x, 1.5f, z), new Vector3(0.16f, 1.5f, 0.16f), wood);
            var roofMat = Mat("Kiosk_Roof", new Color(0.25f, 0.6f, 0.4f));
            var roofL = Prim(PrimitiveType.Cube, "RoofLeft", root.transform, new Vector3(-1.05f, 3.15f, 0.15f), new Vector3(2.5f, 0.14f, 2.7f), roofMat);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
            var roofR = Prim(PrimitiveType.Cube, "RoofRight", root.transform, new Vector3(1.05f, 3.15f, 0.15f), new Vector3(2.5f, 0.14f, 2.7f), roofMat);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            Prim(PrimitiveType.Cylinder, "RoofRidge", root.transform, new Vector3(0f, 3.5f, 0.15f), new Vector3(0.14f, 1.35f, 0.14f), woodDark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            void Bin(string name, float x, Color color, string iconKind)
            {
                var bin = new GameObject(name);
                bin.transform.SetParent(root.transform, false);
                bin.transform.localPosition = new Vector3(x, 0f, -0.35f);
                var binMat = Mat(name, color);
                Prim(PrimitiveType.Cylinder, "Body", bin.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0.86f, 0.55f, 0.86f), binMat);
                Prim(PrimitiveType.Sphere, "RoundBottom", bin.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.86f, 0.4f, 0.86f), binMat);
                Prim(PrimitiveType.Cylinder, "Lid", bin.transform, new Vector3(0f, 1.32f, 0f), new Vector3(0.94f, 0.1f, 0.94f), Mat(name + "Lid", color * 0.78f));
                Prim(PrimitiveType.Sphere, "Knob", bin.transform, new Vector3(0f, 1.42f, 0f), Vector3.one * 0.13f, woodDark);
                // White pictogram disc + tiny icon shape
                Prim(PrimitiveType.Cylinder, "IconDisc", bin.transform, new Vector3(0f, 0.85f, -0.45f), new Vector3(0.42f, 0.03f, 0.42f), Mat("Coral_White", Color.white)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (iconKind == "bottle") Prim(PrimitiveType.Cylinder, "Icon", bin.transform, new Vector3(0f, 0.85f, -0.48f), new Vector3(0.09f, 0.13f, 0.09f), binMat);
                else if (iconKind == "can") Prim(PrimitiveType.Cylinder, "Icon", bin.transform, new Vector3(0f, 0.85f, -0.48f), new Vector3(0.13f, 0.09f, 0.13f), MetalMat("CanSilver", new Color(0.8f, 0.82f, 0.85f)));
                else Prim(PrimitiveType.Cube, "Icon", bin.transform, new Vector3(0f, 0.85f, -0.48f), new Vector3(0.2f, 0.15f, 0.04f), Mat("PaperIcon", new Color(0.85f, 0.7f, 0.45f)));
            }
            Bin("Bin_Blue", -1.2f, new Color(0.2f, 0.5f, 0.95f), "bottle");
            Bin("Bin_Green", 0f, new Color(0.25f, 0.75f, 0.3f), "can");
            Bin("Bin_Yellow", 1.2f, new Color(1f, 0.82f, 0.2f), "paper");

            var sign = Prim(PrimitiveType.Cube, "Signboard", root.transform, new Vector3(0f, 2.55f, -1.05f), new Vector3(2.8f, 0.7f, 0.1f), wood);
            Object.DestroyImmediate(sign.GetComponent<Collider>());
            SignText(root.transform, new Vector3(0f, 2.55f, -1.12f), "RECYCLE!", 1.5f, Color.white);
            var notice = Prim(PrimitiveType.Cube, "NoticeBoard", root.transform, new Vector3(1.95f, 1.5f, -0.9f), new Vector3(0.9f, 1.1f, 0.08f), woodDark);
            Object.DestroyImmediate(notice.GetComponent<Collider>());
            SignText(root.transform, new Vector3(1.95f, 1.5f, -0.97f), "Upgrades\nsold here!", 0.55f, new Color(1f, 0.95f, 0.8f));
            root.AddComponent<RecyclingKiosk>();
            return root;
        }

        /// <summary>Small world-space TMP label used by signposts and boards.</summary>
        static void SignText(Transform parent, Vector3 localPos, string text, float size, Color color)
        {
            var go = new GameObject("SignText", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
        }
        static GameObject BuildSeal(Vector3 pos)
        {
            var root = new GameObject("SealPup_Rescue");
            root.transform.position = pos;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // Phase 6 stylized seal pup: rounded mottled body, big soulful eyes, tiny flippers.
            var sealMat = Mat("Seal", new Color(0.66f, 0.68f, 0.72f));
            var sealDark = Mat("Seal_Spots", new Color(0.45f, 0.47f, 0.52f));
            var sealBelly = Mat("Seal_Belly", new Color(0.88f, 0.9f, 0.92f));
            Prim(PrimitiveType.Sphere, "Body", visual, new Vector3(0f, 0.3f, -0.08f), new Vector3(0.62f, 0.48f, 1.02f), sealMat);
            Prim(PrimitiveType.Sphere, "Belly", visual, new Vector3(0f, 0.2f, 0.12f), new Vector3(0.5f, 0.34f, 0.8f), sealBelly);
            foreach (var (x, y, z) in new[] { (-0.16f, 0.5f, -0.25f), (0.14f, 0.52f, 0.02f), (-0.05f, 0.48f, -0.5f) })
                Prim(PrimitiveType.Sphere, "Spot", visual, new Vector3(x, y, z), new Vector3(0.18f, 0.08f, 0.22f), sealDark);
            Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 0.48f, 0.48f), Vector3.one * 0.44f, sealMat);
            Prim(PrimitiveType.Sphere, "Muzzle", visual, new Vector3(0f, 0.42f, 0.68f), new Vector3(0.22f, 0.16f, 0.16f), sealBelly);
            Prim(PrimitiveType.Sphere, "Nose", visual, new Vector3(0f, 0.47f, 0.75f), Vector3.one * 0.07f, Mat("Seal_Nose", new Color(0.2f, 0.16f, 0.18f)));
            foreach (var side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "Eye", visual, new Vector3(side * 0.13f, 0.56f, 0.65f), Vector3.one * 0.095f, Mat("Seal_Nose", Color.black));
                Prim(PrimitiveType.Sphere, "EyeGlint", visual, new Vector3(side * 0.11f, 0.6f, 0.72f), Vector3.one * 0.028f, Mat("Coral_White", Color.white));
                var flipper = Prim(PrimitiveType.Sphere, "Flipper", visual, new Vector3(side * 0.36f, 0.16f, 0.28f), new Vector3(0.22f, 0.08f, 0.36f), sealDark);
                flipper.transform.localRotation = Quaternion.Euler(0f, side * 25f, side * 12f);
                var tail = Prim(PrimitiveType.Sphere, "TailFlipper", visual, new Vector3(side * 0.12f, 0.18f, -0.62f), new Vector3(0.2f, 0.07f, 0.3f), sealDark);
                tail.transform.localRotation = Quaternion.Euler(0f, side * -30f, 0f);
            }
            // Tangled ghost net draped over the pup (disabled by the Tender Care sequence).
            var net = new GameObject("GhostNet").transform;
            net.SetParent(root.transform, false);
            var netMat = Mat("GhostNet", new Color(0.2f, 0.55f, 0.45f));
            var netMesh = MeshAsset("Creatures", "GhostNetRing", () => Torus(0.34f, 0.035f, 14));
            foreach (var (z, tilt) in new[] { (-0.25f, 12f), (0f, -8f), (0.22f, 18f) })
                MeshPrim(netMesh, "NetRing", net, new Vector3(0f, 0.34f, z), Vector3.one, netMat).transform.localRotation = Quaternion.Euler(tilt, 20f, 0f);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Strand", net, new Vector3(0f, 0.4f, -0.2f + i * 0.2f), new Vector3(0.03f, 0.45f, 0.03f), netMat).transform.localRotation = Quaternion.Euler(0f, 0f, 90f + (i % 2 == 0 ? 25f : -25f));
            Prim(PrimitiveType.Sphere, "NetBuoy", net, new Vector3(0.3f, 0.5f, -0.1f), Vector3.one * 0.11f, Mat("BuoyOrange", new Color(1f, 0.5f, 0.15f)));
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

        /// <summary>Coral's base camp: clickable tent + campfire with a dusk lantern light. Returns the camp lights.</summary>
        static Light[] BuildCamp(Vector3 pos, GameData data)
        {
            var root = new GameObject("BaseCamp");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, 35f, 0f); // doorway toward the beach

            var tentMat = Mat("Tent", new Color(0.95f, 0.55f, 0.25f));
            var sideL = Prim(PrimitiveType.Cube, "TentLeft", root.transform, new Vector3(-0.85f, 0.95f, 0f), new Vector3(0.12f, 2.2f, 2.6f), tentMat);
            sideL.transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
            var sideR = Prim(PrimitiveType.Cube, "TentRight", root.transform, new Vector3(0.85f, 0.95f, 0f), new Vector3(0.12f, 2.2f, 2.6f), tentMat);
            sideR.transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
            Prim(PrimitiveType.Cube, "TentBack", root.transform, new Vector3(0f, 0.8f, -1.25f), new Vector3(1.9f, 1.7f, 0.12f), tentMat);
            // Phase 6 camp dressing: door flap + warm interior lantern (fades on at dusk with the campfire).
            var flap = Prim(PrimitiveType.Cube, "TentFlap", root.transform, new Vector3(0.45f, 0.75f, 1.28f), new Vector3(0.8f, 1.4f, 0.08f), Mat("Tent_Flap", new Color(0.85f, 0.45f, 0.2f)));
            flap.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);
            Object.DestroyImmediate(flap.GetComponent<Collider>());
            var lanternGo = new GameObject("TentLantern");
            lanternGo.transform.SetParent(root.transform, false);
            lanternGo.transform.localPosition = new Vector3(0f, 1f, 0.6f);
            var lanternLight = lanternGo.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1f, 0.75f, 0.45f);
            lanternLight.range = 6f;
            lanternLight.intensity = 0f;
            var lanternBulb = Prim(PrimitiveType.Sphere, "LanternBulb", root.transform, new Vector3(0f, 1f, 1.15f), Vector3.one * 0.12f, Mat("LanternGlow", new Color(1f, 0.85f, 0.55f)));
            lanternBulb.GetComponent<Renderer>().sharedMaterial.EnableKeyword("_EMISSION");
            Object.DestroyImmediate(lanternBulb.GetComponent<Collider>());
            var tentHit = new GameObject("BaseCampTent");
            tentHit.transform.SetParent(root.transform, false);
            tentHit.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var col = tentHit.AddComponent<SphereCollider>();
            col.radius = 1.8f;
            tentHit.AddComponent<BaseCampTent>();
            foreach (var c in root.GetComponentsInChildren<Collider>()) if (c != col) Object.DestroyImmediate(c); // only the tent sphere is clickable

            var firePos = new Vector3(2.6f, 0f, 1.8f);
            Object.DestroyImmediate(Prim(PrimitiveType.Cylinder, "Campfire", root.transform, firePos + Vector3.up * 0.12f, new Vector3(0.7f, 0.12f, 0.7f), Mat("Rock", Color.gray)).GetComponent<Collider>());
            Particles(root.transform, "CampfireFlame", new Color(1f, 0.6f, 0.25f), true, 14f, 0, 0.35f, 0.9f, 0.8f).transform.localPosition = firePos + Vector3.up * 0.3f;
            var fireLightGo = new GameObject("CampfireLight");
            fireLightGo.transform.SetParent(root.transform, false);
            fireLightGo.transform.localPosition = firePos + Vector3.up * 1f;
            var fireLight = fireLightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.62f, 0.3f);
            fireLight.range = 9f;
            fireLight.intensity = 0f; // DayNightCycle fades it on at dusk

            // Upcycling workbench beside the tent: table + click sphere + starter recipes.
            var benchRoot = new GameObject("CraftingWorkbench");
            benchRoot.transform.SetParent(root.transform, false);
            benchRoot.transform.localPosition = new Vector3(-2.8f, 0f, 1.6f);
            var benchHit = benchRoot.AddComponent<SphereCollider>();
            benchHit.radius = 1.4f; benchHit.center = new Vector3(0f, 0.7f, 0f);
            var tableMat = Mat("Workbench", new Color(0.45f, 0.32f, 0.18f));
            Object.DestroyImmediate(Prim(PrimitiveType.Cube, "BenchTop", benchRoot.transform, new Vector3(0f, 0.85f, 0f), new Vector3(2f, 0.15f, 1f), tableMat).GetComponent<Collider>());
            Object.DestroyImmediate(Prim(PrimitiveType.Cube, "BenchLegs", benchRoot.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 0.8f), tableMat).GetComponent<Collider>());
            Object.DestroyImmediate(Prim(PrimitiveType.Sphere, "BenchDecor", benchRoot.transform, new Vector3(-0.5f, 1.05f, 0f), Vector3.one * 0.3f, Mat("Coral_Cap", Color.white)).GetComponent<Collider>());
            WireArray(benchRoot.AddComponent<Crafting.CraftingWorkbench>(), "recipes", CreateRecipeAssets(data).Select(r => (Object)r).ToArray());
            // Recycled tool dressing on the bench: hammer + tool pots.
            var hammerHandle = Prim(PrimitiveType.Cylinder, "HammerHandle", benchRoot.transform, new Vector3(0.5f, 1f, 0f), new Vector3(0.05f, 0.22f, 0.05f), tableMat);
            hammerHandle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Object.DestroyImmediate(hammerHandle.GetComponent<Collider>());
            var hammerHead = Prim(PrimitiveType.Cube, "HammerHead", benchRoot.transform, new Vector3(0.72f, 1.02f, 0f), new Vector3(0.1f, 0.1f, 0.22f), MetalMat("CanSilver", Color.white));
            Object.DestroyImmediate(hammerHead.GetComponent<Collider>());
            foreach (var px in new[] { -0.8f, -0.55f })
            {
                var pot = Prim(PrimitiveType.Cylinder, "ToolPot", benchRoot.transform, new Vector3(px, 1.05f, -0.25f), new Vector3(0.16f, 0.14f, 0.16f), Mat("Bin_Green", Color.white));
                Object.DestroyImmediate(pot.GetComponent<Collider>());
            }
            // Campfire dressing: crossed logs + glowing embers under the flame particles.
            foreach (var rot in new[] { 25f, 115f })
            {
                var logGo = Prim(PrimitiveType.Cylinder, "FireLog", root.transform, firePos + Vector3.up * 0.16f, new Vector3(0.09f, 0.34f, 0.09f), tableMat);
                logGo.transform.localRotation = Quaternion.Euler(90f, rot, 0f);
                Object.DestroyImmediate(logGo.GetComponent<Collider>());
            }
            var ember = Prim(PrimitiveType.Sphere, "Embers", root.transform, firePos + Vector3.up * 0.14f, new Vector3(0.3f, 0.12f, 0.3f), Mat("EmberGlow", new Color(1f, 0.45f, 0.15f)));
            ember.GetComponent<Renderer>().sharedMaterial.EnableKeyword("_EMISSION");
            ember.GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.1f) * 1.6f);
            Object.DestroyImmediate(ember.GetComponent<Collider>());
            return new[] { fireLight, lanternLight };
        }

        /// <summary>"The Rocky Passage": two bluffs, a spanning arch rock, driftwood barricade, wooden signpost.</summary>
        static void BuildRegionGate(Vector3 pos)
        {
            var root = new GameObject("RegionGate_RockyPassage");
            root.transform.position = pos;
            var rock = Mat("Rock", Color.gray);
            Object.DestroyImmediate(Prim(PrimitiveType.Cube, "BluffLeft", root.transform, new Vector3(-3.4f, 3.5f, 0f), new Vector3(3.5f, 7f, 3.5f), rock).GetComponent<Collider>());
            Object.DestroyImmediate(Prim(PrimitiveType.Cube, "BluffRight", root.transform, new Vector3(3.4f, 3.5f, 0f), new Vector3(3.5f, 7f, 3.5f), rock).GetComponent<Collider>());
            Object.DestroyImmediate(Prim(PrimitiveType.Cube, "ArchRock", root.transform, new Vector3(0f, 6.4f, 0f), new Vector3(8f, 1.8f, 3.4f), rock).GetComponent<Collider>());
            // Phase 6: weathered driftwood archway in front of the rock arch + coastal boulders.
            var driftMat = Mat("Gate_Driftwood", new Color(0.62f, 0.52f, 0.4f));
            foreach (var side in new[] { -1f, 1f })
            {
                var post = Prim(PrimitiveType.Cylinder, "ArchPost", root.transform, new Vector3(side * 1.9f, 1.7f, -2.2f), new Vector3(0.22f, 1.8f, 0.22f), driftMat);
                post.transform.localRotation = Quaternion.Euler(4f, 0f, side * 7f);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                var boulder2 = Prim(PrimitiveType.Sphere, "GateBoulder", root.transform, new Vector3(side * 2.6f, 0.4f, -2.4f), new Vector3(0.9f, 0.7f, 0.9f), rock);
                Object.DestroyImmediate(boulder2.GetComponent<Collider>());
            }
            var crossbar = Prim(PrimitiveType.Cylinder, "ArchCrossbar", root.transform, new Vector3(0f, 3.5f, -2.2f), new Vector3(0.18f, 2.2f, 0.18f), driftMat);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 88f);
            Object.DestroyImmediate(crossbar.GetComponent<Collider>());

            var blockage = new GameObject("Blockage");
            blockage.transform.SetParent(root.transform, false);
            var logMat = Mat("Workbench", Color.white);
            for (int i = 0; i < 3; i++)
            {
                var log = Prim(PrimitiveType.Cylinder, "DriftwoodLog", blockage.transform, new Vector3(0f, 0.5f + i * 0.55f, 0f), new Vector3(0.35f, 1.9f, 0.35f), logMat);
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 88f + i * 4f);
                Object.DestroyImmediate(log.GetComponent<Collider>());
            }
            var boulder = Prim(PrimitiveType.Sphere, "Boulder", blockage.transform, new Vector3(1.2f, 0.6f, 0.2f), new Vector3(1.3f, 1.1f, 1.3f), rock);
            Object.DestroyImmediate(boulder.GetComponent<Collider>());

            var signMat = Mat("Workbench", Color.white);
            Object.DestroyImmediate(Prim(PrimitiveType.Cylinder, "SignPost", root.transform, new Vector3(-2.2f, 0.9f, -1.6f), new Vector3(0.12f, 0.9f, 0.12f), signMat).GetComponent<Collider>());
            var board = Prim(PrimitiveType.Cube, "SignBoard", root.transform, new Vector3(-2.2f, 1.7f, -1.6f), new Vector3(2.2f, 0.55f, 0.1f), signMat);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var signText = new GameObject("SignText", typeof(TextMeshPro));
            signText.transform.SetParent(root.transform, false);
            signText.transform.localPosition = new Vector3(-2.2f, 1.7f, -1.67f);
            var tmp = signText.GetComponent<TextMeshPro>();
            tmp.text = "To Region 2:\nRocky Shore ->";
            tmp.fontSize = 0.55f; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.2f, 0.12f, 0.05f);

            var hit = root.AddComponent<SphereCollider>();
            hit.radius = 2.6f; hit.center = new Vector3(0f, 1.2f, 0f);
            Wire(root.AddComponent<RegionGate>(), ("blockage", blockage));
        }

        /// <summary>Phase 6 stylized pickup models. Visual only; the tap collider + glint stay on the root.</summary>
        static void BuildItemVisual(ItemData item, Transform parent, int index)
        {
            var vis = new GameObject("Visual").transform;
            vis.SetParent(parent, false);
            switch (item.Id)
            {
                case "plastic_bottle":
                {
                    var bottle = MeshPrim(MeshAsset("Items", "PlasticBottle", () => Bottle(0.09f, 0.42f)), "Bottle", vis, new Vector3(0f, 0.1f, 0f), Vector3.one, GlassMat("Item_BottlePET", new Color(0.55f, 0.8f, 1f), 0.5f));
                    bottle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // washed up on its side
                    var cap = Prim(PrimitiveType.Cylinder, "Cap", vis, new Vector3(0.22f, 0.1f, 0f), new Vector3(0.07f, 0.04f, 0.07f), Mat("Coral_White", Color.white));
                    cap.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Object.DestroyImmediate(cap.GetComponent<Collider>());
                    break;
                }
                case "soda_can":
                {
                    var canMat = MetalMat(index % 2 == 0 ? "Item_CanRed" : "Item_CanBlue", index % 2 == 0 ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.2f, 0.4f, 0.95f));
                    Prim(PrimitiveType.Cylinder, "Can", vis, new Vector3(0f, 0.13f, 0f), new Vector3(0.16f, 0.13f, 0.16f), canMat);
                    var top = Prim(PrimitiveType.Cylinder, "CanTop", vis, new Vector3(0f, 0.27f, 0f), new Vector3(0.14f, 0.015f, 0.14f), MetalMat("CanSilver", new Color(0.8f, 0.82f, 0.85f)));
                    Object.DestroyImmediate(top.GetComponent<Collider>());
                    var tab = Prim(PrimitiveType.Cube, "PopTab", vis, new Vector3(0.02f, 0.29f, 0f), new Vector3(0.05f, 0.012f, 0.08f), MetalMat("CanSilver", Color.white));
                    Object.DestroyImmediate(tab.GetComponent<Collider>());
                    break;
                }
                case "glass_jar":
                    MeshPrim(MeshAsset("Items", "GlassBottle", () => Bottle(0.11f, 0.34f)), "GlassBottle", vis, new Vector3(0f, 0.02f, 0f), Vector3.one,
                        GlassMat(index % 2 == 0 ? "Item_GlassGreen" : "Item_GlassAmber", index % 2 == 0 ? new Color(0.3f, 0.75f, 0.45f) : new Color(0.9f, 0.55f, 0.2f), 0.6f));
                    break;
                case "car_tire":
                {
                    var tire = MeshPrim(MeshAsset("Items", "CarTire", () => Torus(0.34f, 0.15f, 18)), "Tire", vis, new Vector3(0f, 0.16f, 0f), Vector3.one, Mat("Item_Tire", new Color(0.12f, 0.12f, 0.13f)));
                    tire.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // flat on the sand
                    var sandCake = Prim(PrimitiveType.Cylinder, "SandCake", vis, new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.05f, 0.36f), Mat("Sand", new Color(0.78f, 0.68f, 0.5f)));
                    Object.DestroyImmediate(sandCake.GetComponent<Collider>());
                    break;
                }
                case "ghost_net":
                {
                    var netMat2 = Mat("GhostNet", new Color(0.2f, 0.55f, 0.45f));
                    var ring = MeshAsset("Items", "GhostNetRing", () => Torus(0.2f, 0.03f, 12));
                    foreach (var (x, z, tilt) in new[] { (-0.08f, 0f, 70f), (0.08f, 0.05f, 95f), (0f, -0.08f, 80f) })
                        MeshPrim(ring, "NetLoop", vis, new Vector3(x, 0.14f, z), Vector3.one, netMat2).transform.localRotation = Quaternion.Euler(tilt, x * 100f, 0f);
                    foreach (var bx in new[] { -0.22f, 0.24f })
                    {
                        var buoy = Prim(PrimitiveType.Sphere, "CorkBuoy", vis, new Vector3(bx, 0.16f, 0.1f), Vector3.one * 0.11f, Mat("BuoyOrange", new Color(1f, 0.5f, 0.15f)));
                        Object.DestroyImmediate(buoy.GetComponent<Collider>());
                    }
                    break;
                }
                case "sea_glass":
                {
                    var glassColors = new[] { ("Item_SeaGlassFoam", new Color(0.35f, 0.9f, 0.75f)), ("Item_SeaGlassCobalt", new Color(0.2f, 0.45f, 0.95f)), ("Item_SeaGlassFrost", new Color(0.9f, 0.96f, 0.97f)) };
                    var (matName, matColor) = glassColors[index % 3];
                    var gem = MeshPrim(MeshAsset("Items", "SeaGlassShard", Gem), "SeaGlass", vis, new Vector3(0f, 0.08f, 0f), new Vector3(0.13f, 0.07f, 0.1f), GlassMat(matName, matColor, 0.65f));
                    gem.transform.localRotation = Quaternion.Euler(15f, index * 50f, 10f);
                    break;
                }
                case "scallop_shell":
                {
                    var shellMat = Mat("Item_Scallop", new Color(1f, 0.62f, 0.58f));
                    var fan = Prim(PrimitiveType.Sphere, "ShellFan", vis, new Vector3(0f, 0.05f, 0f), new Vector3(0.34f, 0.08f, 0.3f), shellMat);
                    Object.DestroyImmediate(fan.GetComponent<Collider>());
                    for (int rib = -2; rib <= 2; rib++)
                    {
                        var ribGo = Prim(PrimitiveType.Cube, "Rib", vis, new Vector3(rib * 0.05f, 0.09f, 0.03f), new Vector3(0.025f, 0.02f, 0.24f), Mat("Item_ScallopRib", new Color(0.95f, 0.45f, 0.42f)));
                        ribGo.transform.localRotation = Quaternion.Euler(0f, rib * 18f, 0f);
                        Object.DestroyImmediate(ribGo.GetComponent<Collider>());
                    }
                    break;
                }
                case "conch_shell":
                {
                    var conchMat = Mat("Item_Conch", new Color(1f, 0.78f, 0.62f));
                    foreach (var (x, y, s) in new[] { (0f, 0.12f, 0.3f), (0.1f, 0.16f, 0.22f), (0.17f, 0.2f, 0.14f) })
                    {
                        var whorl = Prim(PrimitiveType.Sphere, "Whorl", vis, new Vector3(x, y, 0f), new Vector3(s, s * 0.85f, s), conchMat);
                        Object.DestroyImmediate(whorl.GetComponent<Collider>());
                    }
                    Prim(PrimitiveType.Sphere, "Lip", vis, new Vector3(-0.1f, 0.1f, 0.02f), new Vector3(0.16f, 0.1f, 0.24f), Mat("Item_ConchLip", new Color(1f, 0.6f, 0.55f)));
                    break;
                }
                case "snack_wrapper":
                {
                    var wrap = Prim(PrimitiveType.Cube, "Wrapper", vis, new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.04f, 0.22f), MetalMat("Item_Wrapper", new Color(1f, 0.6f, 0.15f)));
                    wrap.transform.localRotation = Quaternion.Euler(4f, 20f, 6f);
                    Object.DestroyImmediate(wrap.GetComponent<Collider>());
                    break;
                }
                case "driftwood_clump":
                {
                    var woodMat = Mat("Item_Driftwood", new Color(0.6f, 0.48f, 0.34f));
                    foreach (var (z, rot) in new[] { (-0.06f, 8f), (0.08f, -12f) })
                    {
                        var logGo = Prim(PrimitiveType.Capsule, "Driftwood", vis, new Vector3(0f, 0.09f, z), new Vector3(0.14f, 0.42f, 0.14f), woodMat);
                        logGo.transform.localRotation = Quaternion.Euler(0f, rot, 90f);
                        Object.DestroyImmediate(logGo.GetComponent<Collider>());
                    }
                    break;
                }
                default:
                    Prim(PrimitiveType.Sphere, "Generic", vis, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.24f, Mat("Item_" + item.name, Color.magenta));
                    break;
            }
            foreach (var c in vis.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        static void BuildLitter(GameData data)
        {
            var parent = new GameObject("Litter").transform;
            var rng = new System.Random(7);
            var avoid = new[] { new Vector3(-10f, 0f, -8f), new Vector3(0f, 0f, -4f), new Vector3(8f, 0f, 14f), new Vector3(-18f, 0f, -16f), new Vector3(20f, 0f, 21f) }; // kiosk, spawn, seal, camp, gate

            // 3 of each litter type (only 1 tire), 2 of each natural find.
            foreach (var (item, shape, scale, color) in data.items)
            {
                int count = item is CollectibleItemData ? 2 : item.displayName == "Car Tire" ? 1 : 3;
                for (int i = 0; i < count; i++)
                {
                    Vector3 p;
                    do p = new Vector3((float)rng.NextDouble() * 40f - 20f, 0f, (float)rng.NextDouble() * 40f - 18f);
                    while (avoid.Any(a => Vector3.Distance(a, p) < 4f) || !NavMesh.SamplePosition(p, out _, 0.3f, NavMesh.AllAreas));

                    var root = new GameObject(item.name);
                    root.transform.SetParent(parent);
                    root.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    BuildItemVisual(item, root.transform, i);
                    var col = root.AddComponent<SphereCollider>();
                    col.isTrigger = true; col.radius = 0.7f; col.center = new Vector3(0f, 0.3f, 0f); // big tap target
                    var glint = Prim(PrimitiveType.Quad, "Glint", root.transform, new Vector3(0f, 0.45f, 0f), Vector3.one, item is CollectibleItemData ? glintFindMaterial : glintMaterial);
                    Object.DestroyImmediate(glint.GetComponent<Collider>());
                    glint.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Wire(root.AddComponent<LitterItem>(), ("data", item), ("saveId", $"litter_{parent.childCount:000}"));
                }
            }
        }

        /// <summary>Greybox creature prefab. Always regenerated (it's builder-owned) so component changes land.</summary>
        static GameObject CritterPrefab(string name, AmbientCreature.Species species, Color color, Vector3 scale)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var root = new GameObject(name);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // Phase 6 stylized critters (crab colour passed in is overridden to chunky red-orange).
            var eyeMat = Mat("Critter_Eye", Color.black);
            if (species == AmbientCreature.Species.GhostCrab)
            {
                var crabMat = Mat("Critter_GhostCrab", new Color(0.92f, 0.38f, 0.2f));
                Prim(PrimitiveType.Sphere, "Body", visual, new Vector3(0f, 0.16f, 0f), new Vector3(0.46f, 0.2f, 0.34f), crabMat);
                foreach (var side in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cylinder, "EyeStalk", visual, new Vector3(side * 0.09f, 0.32f, 0.12f), new Vector3(0.035f, 0.09f, 0.035f), crabMat);
                    Prim(PrimitiveType.Sphere, "Eye", visual, new Vector3(side * 0.09f, 0.42f, 0.13f), Vector3.one * 0.075f, eyeMat);
                    Prim(PrimitiveType.Sphere, "Claw", visual, new Vector3(side * 0.26f, 0.14f, 0.2f), new Vector3(0.16f, 0.12f, 0.2f), crabMat);
                    for (int leg = 0; leg < 3; leg++)
                    {
                        var legGo = Prim(PrimitiveType.Cylinder, "Leg", visual, new Vector3(side * 0.28f, 0.07f, -0.1f + leg * 0.1f), new Vector3(0.03f, 0.16f, 0.03f), crabMat);
                        legGo.transform.localRotation = Quaternion.Euler(0f, 0f, side * 55f);
                    }
                }
            }
            else
            {
                var birdMat = Mat("Critter_Sanderling", new Color(0.96f, 0.95f, 0.93f));
                var wingMat = Mat("Critter_Wing", new Color(0.55f, 0.55f, 0.6f));
                Prim(PrimitiveType.Sphere, "Body", visual, new Vector3(0f, 0.22f, 0f), new Vector3(0.24f, 0.26f, 0.38f), birdMat);
                Prim(PrimitiveType.Sphere, "Wing", visual, new Vector3(0f, 0.3f, -0.04f), new Vector3(0.22f, 0.1f, 0.3f), wingMat);
                Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 0.4f, 0.17f), Vector3.one * 0.19f, birdMat);
                Prim(PrimitiveType.Sphere, "Cap", visual, new Vector3(0f, 0.46f, 0.15f), new Vector3(0.18f, 0.1f, 0.18f), wingMat);
                var beak = Prim(PrimitiveType.Cylinder, "Beak", visual, new Vector3(0f, 0.39f, 0.32f), new Vector3(0.025f, 0.11f, 0.025f), eyeMat);
                beak.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foreach (var side in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Sphere, "Eye", visual, new Vector3(side * 0.07f, 0.43f, 0.22f), Vector3.one * 0.045f, eyeMat);
                    Prim(PrimitiveType.Cylinder, "Leg", visual, new Vector3(side * 0.05f, 0.06f, 0f), new Vector3(0.02f, 0.07f, 0.02f), eyeMat);
                }
                Prim(PrimitiveType.Cube, "Tail", visual, new Vector3(0f, 0.28f, -0.22f), new Vector3(0.12f, 0.03f, 0.14f), wingMat);
            }
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.2f; agent.height = 0.5f; agent.acceleration = 30f; agent.angularSpeed = 600f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Coral steers around them, not vice versa
            var creature = root.AddComponent<AmbientCreature>();
            Wire(creature, ("visual", visual));
            var so = new SerializedObject(creature);
            so.FindProperty("species").enumValueIndex = (int)species;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // =====================================================================
        // HUD
        // =====================================================================

        /// <summary>
        /// Phase 6 tropical grade: ACES tonemapping, +12% saturation, warm balance, gentle bloom,
        /// depth texture on both pipelines (water foam + SSAO), SSAO feature ensured, soft shadows.
        /// </summary>
        static void BuildPostProcessing(Camera cam, Light sun)
        {
            string profilePath = ArtDir + "/BeachVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
                var color = profile.Add<ColorAdjustments>(true);
                color.saturation.Override(12f);
                color.postExposure.Override(0.08f);
                color.contrast.Override(4f);
                var wb = profile.Add<WhiteBalance>(true);
                wb.temperature.Override(10f); // warm tropical balance
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.9f);
                bloom.intensity.Override(0.35f);
                bloom.scatter.Override(0.55f);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            var volumeGo = new GameObject("GlobalVolume", typeof(Volume));
            var volume = volumeGo.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var camData = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            foreach (var rpPath in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
                if (rp == null) continue;
                var so = new SerializedObject(rp);
                var depthProp = so.FindProperty("m_RequireDepthTexture");
                if (depthProp != null) depthProp.boolValue = true;
                var shadowDist = so.FindProperty("m_ShadowDistance");
                if (shadowDist != null) shadowDist.floatValue = 55f;
                so.ApplyModifiedProperties();
            }
            foreach (var rendererPath in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (rendererData == null) continue;
                bool hasSsao = AssetDatabase.LoadAllAssetsAtPath(rendererPath).Any(a => a is ScreenSpaceAmbientOcclusion);
                if (!hasSsao)
                {
                    var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                    ssao.name = "ScreenSpaceAmbientOcclusion";
                    AssetDatabase.AddObjectToAsset(ssao, rendererData);
                    var so = new SerializedObject(rendererData);
                    var features = so.FindProperty("m_RendererFeatures");
                    features.arraySize++;
                    features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                    var map = so.FindProperty("m_RendererFeatureMap");
                    if (map != null) { map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = AssetDatabase.GetAssetPath(ssao).GetHashCode(); }
                    so.ApplyModifiedProperties();
                }
            }
        }

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
            var speechLayer = Rect("SpeechBubbleLayer", hud, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Wire(canvasGo.AddComponent<SpeechBubbleManager>(), ("layer", speechLayer), ("worldCamera", cam));

            // ---- Top bar ----
            var top = Panel("TopBar", hud, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(0f, 90f), new Color(0f, 0.2f, 0.3f, 0.55f));
            var gear = Btn("GearButton", top, "Gear", new Color(0.45f, 0.55f, 0.65f), new Vector2(0f, 0.5f), new Vector2(55f, 0f), new Vector2(84f, 62f));
            gear.GetComponentInChildren<TMP_Text>().fontSize = 22;
            var credits = Pill(top, "Credits", new Vector2(260f, 0f), new Color(0.2f, 0.5f, 0.95f), "Credits: 0");
            var coins = Pill(top, "CollectorCoins", new Vector2(570f, 0f), new Color(0.7f, 0.4f, 0.85f), "Collector Coins: 0", width: 380f);
            var eco = Pill(top, "Eco", new Vector2(940f, 0f), new Color(0.25f, 0.7f, 0.35f), "Eco: 0");
            var journalBtn = Btn("JournalButton", top, "Journal (J)", new Color(0.95f, 0.6f, 0.3f), new Vector2(0f, 0.5f), new Vector2(1250f, 0f), new Vector2(230f, 62f));
            // Sun dial rides the right end of the top bar, clear of the minimap below it.
            var dial = Rect("SunDial", hud, Vector2.one, Vector2.one, new Vector2(-430f, -45f), new Vector2(180f, 90f));
            var dialSun = Panel("SunIcon", dial, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(30f, 30f), new Color(1f, 0.82f, 0.25f), roundSprite);
            var dialDay = Text("DayLabel", dial, "Day 1", 22, Color.white);
            Place(dialDay.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(180f, 30f));
            dialDay.fontStyle = FontStyles.Bold;
            Wire(canvasGo.AddComponent<SunDialUI>(), ("sunIcon", dialSun.rectTransform), ("dayText", dialDay));

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

            // ---- Quest tracker (right side, under the minimap; tap header to fold) ----
            var questCard = Panel("QuestTracker", hud, Vector2.one, Vector2.one, new Vector2(-160f, -490f), new Vector2(300f, 210f), new Color(0.99f, 0.97f, 0.9f, 0.95f));
            questCard.raycastTarget = true;
            var questHeader = Btn("QuestHeader", questCard.rectTransform, "Quest", new Color(0.15f, 0.55f, 0.55f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(280f, 46f));
            var questTitle = questHeader.GetComponentInChildren<TMP_Text>();
            questTitle.fontSize = 24;
            var questStar = Panel("QuestStar", questCard.rectTransform, Vector2.one, Vector2.one, new Vector2(-24f, -28f), new Vector2(34f, 34f), new Color(1f, 0.82f, 0.2f), roundSprite);
            var questSteps = Text("QuestSteps", questCard.rectTransform, "", 20, new Color(0.14f, 0.25f, 0.29f));
            Place(questSteps.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(280f, 140f));
            questSteps.alignment = TextAlignmentOptions.TopLeft;
            Wire(canvasGo.AddComponent<QuestTrackerUI>(), ("panel", questCard), ("headerButton", questHeader),
                ("titleText", questTitle), ("stepsText", questSteps), ("starIcon", questStar));

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
            modal.raycastTarget = true; // block clicks from reaching the beach behind the modal
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
            var listText = ScrollingText("StageList", shopPage, new Vector2(1f, 0f), Vector2.one, new Vector2(-210f, 0f), new Vector2(400f, 0f));

            // ---- Ocean Journal panel ----
            var journal = Panel("JournalPanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 760f), new Color(0.99f, 0.96f, 0.88f, 0.98f));
            journal.raycastTarget = true;
            var jHeader = Text("Header", journal, "Ocean Journal", 40, new Color(0.1f, 0.4f, 0.45f));
            Place(jHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(700f, 60f));
            jHeader.fontStyle = FontStyles.Bold;
            var jClose = Btn("Close", journal, "X", new Color(0.85f, 0.35f, 0.35f), Vector2.one, new Vector2(-45f, -45f), new Vector2(60f, 60f));
            var journalText = ScrollingText("Entries", journal, Vector2.zero, Vector2.one, new Vector2(0f, -40f), new Vector2(-60f, -140f));

            // ---- Day Wrap-Up modal + fade-to-camp overlay ----
            var fade = Panel("FadeOverlay", hud, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.05f, 0.12f, 0f));
            fade.sprite = null;
            fade.raycastTarget = false;
            var summary = Panel("DaySummaryPanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 560f), new Color(0.99f, 0.96f, 0.88f, 0.98f));
            summary.raycastTarget = true;
            var sHeader = Text("Header", summary, "Day 1 Complete!", 40, new Color(0.1f, 0.4f, 0.45f));
            Place(sHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(820f, 70f));
            sHeader.fontStyle = FontStyles.Bold;
            var sHaul = Text("Haul", summary, "", 30, new Color(0.15f, 0.22f, 0.27f));
            Place(sHaul.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(800f, 230f));
            sHaul.alignment = TextAlignmentOptions.TopLeft;
            var sJournal = Text("JournalHighlight", summary, "", 26, new Color(0.1f, 0.5f, 0.55f));
            Place(sJournal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(800f, 80f));
            sJournal.alignment = TextAlignmentOptions.TopLeft;
            var sleep = Btn("SleepButton", summary, "Sleep & Greet Tomorrow", new Color(0.25f, 0.75f, 0.35f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(560f, 100f));
            // ---- Crafting modal ----
            var craftPanel = Panel("CraftingPanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f), new Color(0.98f, 0.95f, 0.9f, 0.97f));
            craftPanel.raycastTarget = true;
            var craftHeader = Text("Header", craftPanel, "Upcycling Workshop", 40, new Color(0.1f, 0.4f, 0.45f));
            Place(craftHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(700f, 60f));
            craftHeader.fontStyle = FontStyles.Bold;
            var craftClose = Btn("Close", craftPanel, "X", new Color(0.85f, 0.35f, 0.35f), Vector2.one, new Vector2(-45f, -45f), new Vector2(60f, 60f));
            var craftRows = Rect("RecipeRows", craftPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(800f, 400f));
            var craftLayout = craftRows.gameObject.AddComponent<VerticalLayoutGroup>();
            craftLayout.spacing = 12f; craftLayout.childForceExpandWidth = false; craftLayout.childForceExpandHeight = false;
            var craftResult = Text("Result", craftPanel, "", 26, new Color(0.15f, 0.35f, 0.3f));
            Place(craftResult.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(820f, 50f));
            Wire(canvasGo.AddComponent<CraftingUI>(), ("panel", craftPanel.gameObject), ("rowsRoot", craftRows),
                ("closeButton", craftClose), ("resultText", craftResult));

            // ---- Region gate confirm + world-map teaser ----
            var gatePanel = Panel("RegionGatePanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 380f), new Color(0.99f, 0.96f, 0.88f, 0.98f));
            gatePanel.raycastTarget = true;
            var gateTitle = Text("Header", gatePanel, "The Rocky Passage", 38, new Color(0.1f, 0.4f, 0.45f));
            Place(gateTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(660f, 60f));
            gateTitle.fontStyle = FontStyles.Bold;
            var gateConfirmText = Text("Prompt", gatePanel, "", 30, new Color(0.15f, 0.22f, 0.27f));
            Place(gateConfirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(640f, 120f));
            var gateYes = Btn("ConfirmJourney", gatePanel, "Journey Onward!", new Color(0.25f, 0.75f, 0.35f), new Vector2(0.5f, 0f), new Vector2(-165f, 70f), new Vector2(310f, 90f));
            var gateNo = Btn("CancelJourney", gatePanel, "Not Yet", new Color(0.6f, 0.65f, 0.7f), new Vector2(0.5f, 0f), new Vector2(165f, 70f), new Vector2(310f, 90f));
            var mapPanel = Panel("WorldMapPanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 640f), new Color(0.75f, 0.88f, 0.9f, 0.98f));
            mapPanel.raycastTarget = true;
            var mapTitle = Text("Header", mapPanel, "Ocean Guardians World Map", 40, new Color(0.08f, 0.35f, 0.42f));
            Place(mapTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 65f));
            mapTitle.fontStyle = FontStyles.Bold;
            var region1 = Panel("Region1Card", mapPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240f, 20f), new Vector2(380f, 300f), new Color(0.85f, 0.78f, 0.6f));
            Text("Label", region1, "Region 1\nCoral's Beach\n\n<b><color=#2E9E4F>MASTERED</color></b>", 34, new Color(0.2f, 0.25f, 0.2f));
            var region2 = Panel("Region2Card", mapPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 20f), new Vector2(380f, 300f), new Color(0.55f, 0.6f, 0.68f));
            Text("Label", region2, "Region 2\nRocky Shore\n\n<color=#FFF3B0>Coming Soon!</color>", 34, Color.white);
            var mapClose = Btn("CloseMap", mapPanel, "Back to the Beach", new Color(0.25f, 0.75f, 0.35f), new Vector2(0.5f, 0f), new Vector2(0f, 65f), new Vector2(440f, 85f));
            Wire(canvasGo.AddComponent<RegionGateUI>(), ("confirmPanel", gatePanel.gameObject), ("confirmText", gateConfirmText),
                ("confirmButton", gateYes), ("cancelButton", gateNo), ("mapPanel", mapPanel.gameObject), ("mapCloseButton", mapClose));

            Wire(canvasGo.AddComponent<DaySummaryUI>(), ("panel", summary.gameObject), ("headerText", sHeader),
                ("haulText", sHaul), ("journalText", sJournal), ("sleepButton", sleep), ("fadeOverlay", fade));

            // ---- Pause / settings modal (last sibling = always on top) ----
            var pausePanel = Panel("PausePanel", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 760f), new Color(0.92f, 0.97f, 1f, 0.92f));
            pausePanel.raycastTarget = true;
            var pauseTitle = Text("Header", pausePanel, "Paused - Settings", 42, new Color(0.1f, 0.4f, 0.45f));
            Place(pauseTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(680f, 65f));
            pauseTitle.fontStyle = FontStyles.Bold;
            var (masterSlider, masterValue) = VolumeRow(pausePanel, "Master Volume", new Vector2(0f, -150f));
            var (musicSlider, musicValue) = VolumeRow(pausePanel, "Music Volume", new Vector2(0f, -220f));
            var (sfxSlider, sfxValue) = VolumeRow(pausePanel, "SFX Volume", new Vector2(0f, -290f));
            var controls = Text("Controls", pausePanel,
                "<b>Controls</b>\nClick / Tap: move & interact    Hold: keep walking\nMouse wheel / 2-finger pinch: zoom\nJ: Ocean Journal    F5: Quick Save    F9: Quick Load    Esc: Pause",
                24, new Color(0.15f, 0.22f, 0.27f));
            Place(controls.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(680f, 170f));
            var resume = Btn("ResumeButton", pausePanel, "Resume Game", new Color(0.25f, 0.75f, 0.35f), new Vector2(0.5f, 0f), new Vector2(-190f, 65f), new Vector2(350f, 80f));
            var quickSave = Btn("QuickSaveButton", pausePanel, "Quick Save (F5)", new Color(0.2f, 0.55f, 0.85f), new Vector2(0.5f, 0f), new Vector2(190f, 65f), new Vector2(350f, 80f));
            var quit = Btn("QuitButton", pausePanel, "Save & Quit", new Color(0.85f, 0.4f, 0.35f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(350f, 70f));
            Wire(canvasGo.AddComponent<PauseMenuUI>(), ("panel", pausePanel.gameObject), ("gearButton", gear),
                ("resumeButton", resume), ("quickSaveButton", quickSave), ("quitButton", quit),
                ("masterSlider", masterSlider), ("musicSlider", musicSlider), ("sfxSlider", sfxSlider),
                ("masterValue", masterValue), ("musicValue", musicValue), ("sfxValue", sfxValue));

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
                ("journalPanel", journal.gameObject), ("journalButton", journalBtn), ("journalCloseButton", jClose), ("journalText", journalText),
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

        static TMP_Text ScrollingText(string name, Component parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var view = Panel(name, parent, anchorMin, anchorMax, pos, size, new Color(0f, 0.2f, 0.3f, 0.08f));
            view.raycastTarget = true; // the ScrollRect needs a raycast target to receive wheel/drag
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
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

        /// <summary>Transparent dark-sand material for the wet band left behind by waves.</summary>
        /// <summary>The stylized shoreline water material (shader in Shaders/, copied to Art/Shaders on build).</summary>
        static Material WaterMaterial()
        {
            string path = ArtDir + "/Shaders/StylizedWater.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("OceanGuardians/StylizedWater");
            m = new Material(shader ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            if (shader == null) Debug.LogWarning("[Ocean Guardians] StylizedWater shader not found; water falls back to Unlit.");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material WetSandMaterial()
        {
            string path = $"{MatDir}/WetSand.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.22f, 0f));
            m.SetFloat("_Surface", 1f); // transparent
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
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

        // =====================================================================
        // Phase 6 stylized art: procedural meshes + translucent/metal materials
        // =====================================================================

        /// <summary>Creates (or loads) a procedural Mesh asset under Art/&lt;folder&gt;.</summary>
        static Mesh MeshAsset(string folder, string name, System.Func<Mesh> build)
        {
            string path = $"{ArtDir}/{folder}/{name}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            m = build();
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Revolves a (radius, height) profile around Y. Bottles, cans, tori (circle profile), cones.</summary>
        static Mesh Lathe(Vector2[] profile, int segments = 14)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                foreach (var p in profile) verts.Add(dir * p.x + Vector3.up * p.y);
            }
            int ring = profile.Length;
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < ring - 1; i++)
                {
                    int a = s * ring + i, b = a + ring;
                    tris.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            var mesh = new Mesh { vertices = verts.ToArray(), triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Torus(float radius, float tube, int segments = 16)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                profile.Add(new Vector2(radius + Mathf.Cos(a) * tube, Mathf.Sin(a) * tube));
            }
            return Lathe(profile.ToArray(), segments);
        }

        static Mesh Bottle(float bodyRadius, float height)
        {
            float r = bodyRadius, h = height;
            return Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(r * 0.85f, 0f), new Vector2(r, h * 0.06f),
                new Vector2(r * 0.94f, h * 0.3f), new Vector2(r, h * 0.42f), new Vector2(r * 0.94f, h * 0.55f), // molded ridges
                new Vector2(r * 0.55f, h * 0.78f), new Vector2(r * 0.34f, h * 0.86f), new Vector2(r * 0.34f, h * 0.97f),
                new Vector2(r * 0.4f, h), new Vector2(0.001f, h),
            });
        }

        static Mesh Gem()
        {
            // Faceted octahedron shard for sea glass.
            var verts = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            var tris = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>Places a procedural mesh (no collider - interaction colliders live on roots, as before).</summary>
        static GameObject MeshPrim(Mesh mesh, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Translucent stylized glass/plastic (URP Lit, transparent surface).</summary>
        static Material GlassMat(string name, Color color, float alpha = 0.55f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            color.a = alpha;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Smoothness", 0.85f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material MetalMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", 0.85f);
            m.SetFloat("_Smoothness", 0.6f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material GlintMaterial(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("OceanGuardians/SparkleGlint"));
            m.SetColor("_Color", color);
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

        static TMP_Text Pill(Component parent, string name, Vector2 pos, Color color, string label, float anchorX = 0f, float width = 300f)
        {
            var img = Panel(name, parent, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), pos, new Vector2(width, 62f), color);
            var t = Text("Label", img.rectTransform, label, 28, Color.white);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        /// <summary>A labelled 0..1 volume slider row for the pause menu. Returns (slider, value label).</summary>
        static (Slider, TMP_Text) VolumeRow(Component parent, string label, Vector2 pos)
        {
            var nameText = Text("Label", parent, label, 26, new Color(0.15f, 0.22f, 0.27f));
            Place(nameText.rectTransform, new Vector2(0.5f, 1f), pos + new Vector2(-235f, 0f), new Vector2(250f, 40f));
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            var track = Panel("SliderTrack", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos + new Vector2(70f, 0f), new Vector2(340f, 24f), new Color(0.75f, 0.8f, 0.85f));
            track.raycastTarget = true;
            var fill = Panel("Fill", track.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.25f, 0.7f, 0.45f));
            fill.sprite = null;
            var handle = Panel("Handle", track.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(34f, 34f), Color.white, roundSprite);
            handle.raycastTarget = true;
            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            var value = Text("Value", parent, "100%", 26, new Color(0.15f, 0.22f, 0.27f));
            Place(value.rectTransform, new Vector2(0.5f, 1f), pos + new Vector2(300f, 0f), new Vector2(100f, 40f));
            return (slider, value);
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

        /// <summary>Assign an object array [SerializeField] by name.</summary>
        static void WireArray(Object target, string name, params Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(name);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
            Package.Import(pkg, false);
            return false;
        }

        static void OnTmpImported(string _)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Build;
        }
    }
}

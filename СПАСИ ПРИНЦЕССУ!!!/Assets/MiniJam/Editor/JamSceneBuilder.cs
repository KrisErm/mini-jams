using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MiniJam.Editor
{
    public static class JamSceneBuilder
    {
        public const string Root = "Assets/MiniJam/Generated";
        public const string IntroPath = Root + "/Scenes/MJ_Intro.unity";
        public const string CastlePath = Root + "/Scenes/MJ_Castle.unity";
        static JamConfig cfg;
        static Material stone, darkStone, floor, trim, gold, steel, wood, red, blue, skin, violet, green, glow;
        static RuntimeAnimatorController controller;
        static readonly List<JamGuard> guards = new List<JamGuard>();
        static string materialFolder = Root + "/Materials";
        static readonly Dictionary<Material, Material> characterMaterials = new Dictionary<Material, Material>();

        // Updates the interior only. Neither CreateIntro nor its materials are touched.
        public static void UpdateInterior(JamConfig config)
        {
            string backup = BackupInterior();
            JamSuppliedAssets.Install(config);
            cfg = config; materialFolder = Root + "/InteriorV2/Materials";
            Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(materialFolder);
            AssetDatabase.Refresh();
            Materials(); CreateAnimationController();
            EditorUtility.DisplayProgressBar("Mini Jam", "Обновляем интерьер и персонажей", 0.7f);
            CreateCastle(); EnsureBuildScenes(); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(CastlePath);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Интерьер обновлён", "Открыта MJ_Castle. Нажми Play. Для начала с заставкой открой MJ_Intro.\n\nРезервная копия: " + backup, "OK");
        }
        static string BackupInterior()
        {
            string backup = "MiniJam_Backups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backup);
            if (Directory.Exists(Root))
                foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(Root.Length).TrimStart('/', '\\');
                    string target = Path.Combine(backup, "Generated", relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, false);
                }
            foreach (string file in new[] { "GameConfig.asset", "GameConfig.asset.meta" })
                if (File.Exists("Assets/MiniJam/" + file)) File.Copy("Assets/MiniJam/" + file, Path.Combine(backup, file), false);
            return backup;
        }

        public static void Build(JamConfig config)
        {
            cfg = config;
            materialFolder = Root + "/Materials";
            Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            Materials(); CreateAnimationController();
            EditorUtility.DisplayProgressBar("Mini Jam", "Вступление у замка", 0.2f);
            CreateIntro();
            EditorUtility.DisplayProgressBar("Mini Jam", "Комнаты и игровые механики", 0.55f);
            CreateCastle();
            EnsureBuildScenes(); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(IntroPath);
            EditorUtility.ClearProgressBar();
            Debug.Log("Mini Jam: сцены готовы. Открой MJ_Intro и нажми Play. Меню Mini Jam → Проверить сцены проверяет связи.");
            EditorUtility.DisplayDialog("Готово", "Сцены созданы. Нажми Play для начала игры.\n\nМодели можно назначить в этом окне и пересоздать сцены. Для ручного редактирования открой MJ_Castle.", "Играть");
        }
        static void Materials()
        {
            characterMaterials.Clear();
            stone = Mat("Stone", new Color(0.27f, 0.34f, 0.43f));
            darkStone = Mat("Dark stone", new Color(0.13f, 0.18f, 0.24f));
            floor = Mat("Floor", new Color(0.39f, 0.43f, 0.48f));
            trim = Mat("Sandstone trim", new Color(0.60f, 0.56f, 0.47f));
            gold = Mat("Gold", new Color(0.88f, 0.63f, 0.22f), 0.45f);
            steel = Mat("Steel", new Color(0.65f, 0.75f, 0.8f), 0.5f);
            wood = Mat("Wood", new Color(0.30f, 0.15f, 0.09f));
            red = Mat("Crimson", new Color(0.49f, 0.075f, 0.12f));
            blue = Mat("Hero blue", new Color(0.06f, 0.31f, 0.48f));
            skin = Mat("Skin", new Color(0.91f, 0.67f, 0.48f));
            violet = Mat("Royal violet", new Color(0.31f, 0.13f, 0.40f));
            green = Mat("Moss", new Color(0.19f, 0.30f, 0.25f));
            glow = Mat("Warm light", new Color(1, 0.6f, 0.18f));
            glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(1.6f, 0.65f, 0.1f));
        }
        static Material Mat(string name, Color color, float metallic = 0)
        {
            string path = materialFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
            bool dungeonSurface = cfg.suppliedArt && (name == "Stone" || name == "Floor" || name == "Sandstone trim");
            if (!srp && dungeonSurface) shader = Shader.Find("MiniJam/DoubleSidedStone");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Не найден Standard/URP Lit shader. Используй 3D Built-in или URP проект.");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader; m.color = color;
            m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
            if (dungeonSurface && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(m); return m;
        }
        static GameObject Node(string name, Transform parent = null, Vector3 position = default(Vector3))
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go;
        }
        static GameObject Shape(string name, PrimitiveType kind, Transform parent, Vector3 position, Vector3 size, Material material, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(kind); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { Object.DestroyImmediate(go.GetComponent<Collider>()); go.layer = 2; }
            return go;
        }
        static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material m, bool solid = false)
        { return Shape(name, PrimitiveType.Cube, parent, p, size, m, solid); }
        static Transform Marker(string name, Vector3 p, Vector3? lookAt = null)
        {
            var t = Node(name, null, p).transform; if (lookAt.HasValue) t.LookAt(lookAt.Value); return t;
        }
        static void SetupLighting(bool outside)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = outside ? new Color(0.46f, 0.51f, 0.62f) : new Color(0.39f, 0.43f, 0.51f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = outside ? new Color(0.13f, 0.2f, 0.29f) : new Color(0.08f, 0.1f, 0.15f);
            RenderSettings.fogDensity = outside ? 0.014f : 0.009f;
            var sun = Node("Moonlight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(0.68f, 0.80f, 1);
            sun.intensity = outside ? 1 : 0.7f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.sun = sun;
        }
        static JamGame Systems(bool intro)
        {
            var root = Node("GAME — director, UI, audio");
            var sound = root.AddComponent<JamSound>();
            string[] names = { "jump", "swing", "hit", "key", "gate", "checkpoint", "respawn", "twist" };
            sound.cues = names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MiniJam/Audio/" + n + ".wav")).ToArray();
            sound.ambience = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MiniJam/Audio/ambience.wav");
            root.AddComponent<JamHUD>().font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MiniJam/Fonts/DejaVuSans.ttf");
            var game = root.AddComponent<JamGame>(); game.config = cfg; game.introScene = intro; game.Sound = sound;
            if (!intro) game.Video = root.AddComponent<JamVideo>();
            var cameraObject = Node("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.095f, 0.15f, 0.23f);
            camera.fieldOfView = 61; camera.nearClipPlane = 0.08f; camera.farClipPlane = 220;
            game.Camera = camera;
            return game;
        }
        static void CreateIntro()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting(true); JamGame game = Systems(true);
            var world = Node("Exterior — replaceable scenery").transform;
            Box("Ground", world, new Vector3(0, -0.65f, 0), new Vector3(180, 1, 180), green, true);
            Box("Road", world, new Vector3(0, -0.10f, 3), new Vector3(5, 0.15f, 46), trim);
            if (cfg.castleExterior != null)
                FitVisual(cfg.castleExterior, world, new Vector3(0, 0, 26), new Vector3(32, 18, 24), true);
            else
            {
                Box("Keep", world, new Vector3(0, 7, 28), new Vector3(17, 14, 10), stone, true);
                Box("Front wall left", world, new Vector3(-10, 4, 19), new Vector3(13, 8, 2), darkStone, true);
                Box("Front wall right", world, new Vector3(10, 4, 19), new Vector3(13, 8, 2), darkStone, true);
                Box("Gate arch", world, new Vector3(0, 7, 19), new Vector3(8, 3, 2), trim);
                Box("Great gate", world, new Vector3(0, 2.5f, 18.8f), new Vector3(6, 5, 0.7f), wood);
                foreach (float x in new[] { -17f, 17f })
                {
                    Shape("Tower", PrimitiveType.Cylinder, world, new Vector3(x, 6, 19), new Vector3(7, 6, 7), stone, true);
                    Shape("Tower rim", PrimitiveType.Cylinder, world, new Vector3(x, 12, 19), new Vector3(8, 0.55f, 8), trim);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Box("Merlon", world, new Vector3(x + Mathf.Cos(a) * 3.2f, 13, 19 + Mathf.Sin(a) * 3.2f), new Vector3(1.5f, 1.6f, 1.5f), stone);
                    }
                    Box("Flag pole", world, new Vector3(x, 15, 19), new Vector3(0.12f, 6, 0.12f), wood);
                    Box("Flag", world, new Vector3(x + 1.1f, 17, 19), new Vector3(2.3f, 1.3f, 0.08f), red);
                }
                for (int x = -7; x <= 7; x += 2) Box("Keep merlon", world, new Vector3(x, 14.7f, 23.5f), new Vector3(1, 1.5f, 1), trim);
                for (int x = -5; x <= 5; x += 5) Box("Lit window", world, new Vector3(x, 9, 22.93f), new Vector3(1.1f, 2, 0.06f), glow);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (11 + i % 4 * 4);
                float z = -6 + i * 4;
                var rock = Shape("Low-poly rock", PrimitiveType.Cube, world, new Vector3(x, 0.2f, z), new Vector3(3 + i % 3, 2 + i % 2, 3), darkStone);
                rock.transform.rotation = Quaternion.Euler(12, i * 31, 23);
            }
            CharacterVisual(Node("Hero in prologue", world, new Vector3(0, 0, -1)).transform, cfg.hero, blue, false, cfg.heroYaw);
            Torch(world, new Vector3(-4, 1, 17)); Torch(world, new Vector3(4, 1, 17));
            game.introCameraStart = Marker("Camera start", new Vector3(-12, 7, -14), new Vector3(1, 6, 20));
            game.introCameraEnd = Marker("Camera end", new Vector3(-4, 3.5f, -8), new Vector3(1, 5, 23));
            game.Camera.transform.SetPositionAndRotation(game.introCameraStart.position, game.introCameraStart.rotation);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), IntroPath);
        }
        static void CreateCastle()
        {
            guards.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting(false); var game = Systems(false);
            var world = Node("CASTLE — editable level").transform;
            var room1 = Node("01 — Караульный зал", world).transform;
            var room2 = Node("02 — Разрушенный мост", world).transform;
            var room3 = Node("03 — Зал ловушек", world).transform;
            var room4 = Node("04 — Тронный зал", world).transform;
            RoomSides(room1, 0, 20); RoomSides(room2, 20, 44); RoomSides(room3, 44, 65); RoomSides(room4, 65, 83);
            Slab(room1, 0, 20, 14); Slab(room2, 20, 25, 14);
            Slab(room2, 25, 29, 3.5f); Slab(room2, 31.2f, 34.5f, 3.5f);
            Slab(room2, 36.8f, 40, 3.5f); Slab(room2, 40, 44, 14);
            Slab(room3, 44, 65, 14); Slab(room4, 65, 83, 14);
            Wall(room1, new Vector3(0, 2.5f, 0), new Vector3(14, 5, 0.65f));
            Wall(room4, new Vector3(0, 2.5f, 83), new Vector3(14, 5, 0.65f));
            var keyGate = Gateway(room1, 20, true);
            Gateway(room2, 44, false);
            var finalGate = Gateway(room3, 65, true);
            var entry = keyGate.gameObject.AddComponent<JamInteractable>(); entry.kind = JamInteractable.Kind.LockedGate; entry.gate = keyGate;
            InteractionCollider(entry.gameObject, new Vector3(0, 1, -0.65f), new Vector3(4, 2, 1.4f));
            Key(room1, new Vector3(-4.6f, 0, 13.7f));
            Guard(room1, new Vector3(-1.5f, 0.05f, 10), new Vector3(-3, 0.05f, 9), new Vector3(3, 0.05f, 12));
            Guard(room1, new Vector3(3.5f, 0.05f, 16), new Vector3(1.7f, 0.05f, 14), new Vector3(4.3f, 0.05f, 17));
            Crate(room1, new Vector3(4.8f, 0, 6)); Crate(room1, new Vector3(-4.9f, 0, 5));
            Banner(room1, new Vector3(-6.55f, 2.8f, 10), 90); Banner(room1, new Vector3(6.55f, 2.8f, 10), -90);
            Checkpoint(room2, 22, 4, "Разрушенный мост", "Space — прыжок. Разбегись и перепрыгни провалы", new Vector3(0, 0.08f, 22));
            var pit = Node("Pit reset trigger", room2, new Vector3(0, -2, 32.5f));
            InteractionCollider(pit, Vector3.zero, new Vector3(15, 2, 16)); pit.AddComponent<JamZone>().kind = JamZone.Kind.Fall;
            Box("Pit bottom", room2, new Vector3(0, -4.5f, 32.5f), new Vector3(14, 0.5f, 15), darkStone);
            for (int i = 0; i < 3; i++)
                Shape("Bridge support", PrimitiveType.Cylinder, room2, new Vector3(0, -2.2f, 27 + i * 5.7f), new Vector3(1.5f, 2, 1.5f), darkStone);
            Checkpoint(room3, 45.7f, 5, "Зал ловушек", "Дождись зелёного сигнала на плитах. Рычаг — в конце зала", new Vector3(0, 0.08f, 46));
            Spikes(room3, 49.5f, 0); Spikes(room3, 54.5f, 1.2f); Spikes(room3, 59.5f, 2.4f);
            Guard(room3, new Vector3(2.6f, 0.05f, 62), new Vector3(-2.7f, 0.05f, 61.5f), new Vector3(3.5f, 0.05f, 62.5f));
            Lever(room3, new Vector3(-4.8f, 0, 62.7f), finalGate);
            Checkpoint(room4, 67, 7, "Тронный зал", "Подойди к принцессе и нажми E", new Vector3(0, 0.08f, 68));
            Box("Royal carpet", room4, new Vector3(0, 0.035f, 74), new Vector3(4, 0.035f, 17), red);
            Box("Carpet trim L", room4, new Vector3(-2, 0.058f, 74), new Vector3(0.08f, 0.012f, 17), gold);
            Box("Carpet trim R", room4, new Vector3(2, 0.058f, 74), new Vector3(0.08f, 0.012f, 17), gold);
            Throne(room4, new Vector3(0, 0, 80));
            Banner(room4, new Vector3(-3.8f, 3, 82.6f), 180); Banner(room4, new Vector3(3.8f, 3, 82.6f), 180);
            var princess = Node("Princess — E to talk", room4, new Vector3(0, 0.08f, 77.7f)); princess.layer = 2;
            princess.transform.rotation = Quaternion.Euler(0, 180, 0);
            var princessVisual = CharacterVisual(princess.transform, cfg.princess, violet, true, cfg.princessYaw);
            AttachAnimator(princessVisual);
            var talk = princess.AddComponent<JamInteractable>(); talk.kind = JamInteractable.Kind.Princess;
            InteractionCollider(princess, new Vector3(0, 1, 0), new Vector3(6, 2, 6));
            game.finalCamera = Marker("Finale camera", new Vector3(6, 3.1f, 78), new Vector3(0, 1.2f, 75.5f));
            game.finalPlayerSpot = Marker("Finale hero mark", new Vector3(0, 0.08f, 75));
            game.finalBars = Bars(room4, new Vector3(0, 5, 72.7f), 13.4f);
            // Extend the route while retaining the existing bridge/trap layouts.
            room2.position += Vector3.forward * 75;
            room3.position += Vector3.forward * 75;
            room4.position += Vector3.forward * 115;
            game.finalCamera.position += Vector3.forward * 115;
            game.finalPlayerSpot.position += Vector3.forward * 115;
            AddExtendedRoute(world);
            Player(game, new Vector3(0, 0.08f, 3));
            game.broom = Broom(game.Player.transform); game.broom.gameObject.SetActive(false);
            game.guards = guards.ToArray();
            game.Rig.Snap(true);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), CastlePath);
        }
        static void RoomSides(Transform root, float start, float end)
        {
            float center = (start + end) / 2;
            Wall(root, new Vector3(-7.35f, 2.5f, center), new Vector3(0.7f, 5, end - start));
            Wall(root, new Vector3(7.35f, 2.5f, center), new Vector3(0.7f, 5, end - start));
            for (float z = start + 2; z < end; z += 5)
            {
                foreach (float x in new[] { -6.75f, 6.75f })
                {
                    Box("Pillar", root, new Vector3(x, 2.3f, z), new Vector3(0.6f, 4.6f, 0.65f), trim, true);
                    Box("Pillar foot", root, new Vector3(x, 0.25f, z), new Vector3(0.9f, 0.5f, 0.9f), darkStone, true);
                    if (((int)z) % 2 == 0) Torch(root, new Vector3(x * 0.92f, 2, z));
                }
            }
        }
        static void Wall(Transform root, Vector3 p, Vector3 size)
        {
            var shell = Box("Wall collider", root, p, size, stone, true);
            if (cfg.wallModule != null)
            {
                shell.GetComponent<Renderer>().enabled = false;
                // A straight wall module is assumed to run along its local X axis.
                bool side = size.z > size.x;
                var holder = Node("Imported wall", root, p).transform;
                if (side) holder.localRotation = Quaternion.Euler(0, 90, 0);
                float length = side ? size.z : size.x;
                for (float x = -length / 2; x < length / 2 - 0.01f; x += 2.5f)
                {
                    float width = Mathf.Min(2.5f, length / 2 - x);
                    var art = FitVisual(cfg.wallModule, holder, new Vector3(x + width / 2, -size.y / 2, 0), new Vector3(width, size.y, side ? size.x : size.z), false);
                    if (cfg.suppliedArt) Paint(art, stone);
                }
            }
            else
            {
                if (size.x > size.z)
                    for (float x = -size.x / 2 + 1; x < size.x / 2; x += 2)
                        Box("Masonry cap", root, p + new Vector3(x, size.y / 2 + 0.15f, 0), new Vector3(1.85f, 0.3f, size.z + 0.15f), trim);
                else
                    for (float z = -size.z / 2 + 1; z < size.z / 2; z += 2)
                        Box("Masonry cap", root, p + new Vector3(0, size.y / 2 + 0.15f, z), new Vector3(size.x + 0.15f, 0.3f, 1.85f), trim);
            }
        }
        static void Slab(Transform root, float start, float end, float width)
        {
            var slab = Box("Floor collider", root, new Vector3(0, -0.3f, (start + end) / 2), new Vector3(width, 0.6f, end - start), floor, true);
            if (cfg.floorModule != null)
            {
                slab.GetComponent<Renderer>().enabled = false;
                for (float z = start; z < end - 0.01f; z += 2)
                    for (float x = -width / 2; x < width / 2 - 0.01f; x += 2)
                    {
                        float w = Mathf.Min(2, width / 2 - x), d = Mathf.Min(2, end - z);
                        var art = FitVisual(cfg.floorModule, root, new Vector3(x + w / 2, -0.3f, z + d / 2), new Vector3(w, 0.3f, d), false);
                        if (cfg.suppliedArt) Paint(art, floor);
                    }
            }
            else
            {
                for (float z = start + 2; z < end - 0.1f; z += 2)
                    Box("Tile seam", root, new Vector3(0, 0.006f, z), new Vector3(width, 0.008f, 0.024f), darkStone);
                for (float x = -width / 2 + 2; x < width / 2; x += 2)
                    Box("Tile seam", root, new Vector3(x, 0.006f, (start + end) / 2), new Vector3(0.024f, 0.008f, end - start), darkStone);
            }
            if (width < 5)
            {
                Box("Bridge edge L", root, new Vector3(-width / 2 + 0.07f, 0.02f, (start + end) / 2), new Vector3(0.14f, 0.06f, end - start), gold);
                Box("Bridge edge R", root, new Vector3(width / 2 - 0.07f, 0.02f, (start + end) / 2), new Vector3(0.14f, 0.06f, end - start), gold);
            }
        }
        static JamGate Gateway(Transform root, float z, bool withBars)
        {
            Wall(root, new Vector3(-4.75f, 2.5f, z), new Vector3(4.5f, 5, 0.8f));
            Wall(root, new Vector3(4.75f, 2.5f, z), new Vector3(4.5f, 5, 0.8f));
            Box("Lintel", root, new Vector3(0, 4.6f, z), new Vector3(5, 0.8f, 1), trim, true);
            if (cfg.doorwayModule != null)
            {
                var art = FitVisual(cfg.doorwayModule, root, new Vector3(0, 0, z), new Vector3(7.4f, 5.3f, 1.1f), false);
                if (cfg.suppliedArt) Paint(art, trim);
                foreach (var mesh in art.GetComponentsInChildren<MeshFilter>())
                {
                    mesh.gameObject.layer = 0;
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                }
            }
            var gate = Node("Gateway", root, new Vector3(0, 0, z)).AddComponent<JamGate>();
            if (withBars) gate.bars = Bars(gate.transform, Vector3.zero, 5);
            return gate;
        }
        static Transform Bars(Transform root, Vector3 p, float width)
        {
            var bars = Node("Portcullis", root, p).transform;
            // Invisible full collider prevents squeezing between the visual rods.
            var collider = bars.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 2.1f, 0); collider.size = new Vector3(width, 4.2f, 0.3f);
            for (float x = -width / 2 + 0.2f; x < width / 2; x += 0.45f)
                Box("Iron bar", bars, new Vector3(x, 2.1f, 0), new Vector3(0.11f, 4.2f, 0.14f), steel);
            Box("Crossbar", bars, new Vector3(0, 1.25f, 0), new Vector3(width, 0.12f, 0.18f), steel);
            Box("Crossbar", bars, new Vector3(0, 3.1f, 0), new Vector3(width, 0.12f, 0.18f), steel);
            return bars;
        }
        static void Torch(Transform root, Vector3 p)
        {
            var torch = Node("Torch", root, p).transform;
            Shape("Handle", PrimitiveType.Cylinder, torch, Vector3.zero, new Vector3(0.16f, 0.45f, 0.16f), wood);
            Shape("Flame", PrimitiveType.Sphere, torch, Vector3.up * 0.65f, new Vector3(0.30f, 0.55f, 0.30f), glow);
            var light = Node("Warm glow", torch, Vector3.up * 0.9f).AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1, 0.58f, 0.23f); light.range = 7; light.intensity = 2.3f; light.shadows = LightShadows.None;
        }
        static void Banner(Transform root, Vector3 p, float yaw)
        {
            var b = Node("Royal banner", root, p).transform; b.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Cloth", b, Vector3.zero, new Vector3(1.4f, 2.3f, 0.06f), red);
            Box("Gold emblem", b, new Vector3(0, 0.1f, -0.05f), new Vector3(0.6f, 0.6f, 0.06f), gold).transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        static void Crate(Transform root, Vector3 p)
        {
            BreakableCrate(root, p, new Vector3(1.3f, 1.2f, 1.3f), 1);
        }
        static void BreakableCrate(Transform root, Vector3 feet, Vector3 size, int hits)
        {
            var crate = Box("Breakable crate", root, feet + Vector3.up * size.y / 2, size, wood, true);
            crate.AddComponent<JamBreakable>().hits = hits;
            Box("Band", crate.transform, new Vector3(0, -0.3f, 0), new Vector3(1.02f, 0.08f, 1.02f), steel);
            Box("Band", crate.transform, new Vector3(0, 0.3f, 0), new Vector3(1.02f, 0.08f, 1.02f), steel);
        }
        static void AddExtendedRoute(Transform world)
        {
            var storage = Node("02 — Склад с разрушаемыми ящиками", world).transform;
            RoomSides(storage, 20, 50); Slab(storage, 20, 50, 14);
            Checkpoint(storage, 23, 2, "Склад", "", new Vector3(0, 0.08f, 24));
            foreach (float z in new[] { 31f, 42f })
            {
                Gateway(storage, z, false);
                for (int i = -1; i <= 1; i++)
                    BreakableCrate(storage, new Vector3(i * 1.68f, 0, z), new Vector3(1.68f, 3.3f, 1.3f), 2);
            }
            Guard(storage, new Vector3(3, .05f, 27), new Vector3(2, .05f, 26), new Vector3(4, .05f, 29));
            Guard(storage, new Vector3(-3, .05f, 46), new Vector3(-4, .05f, 45), new Vector3(-2, .05f, 48));
            var corridor = Node("03 — Извилистый коридор", world).transform;
            RoomSides(corridor, 50, 95); Slab(corridor, 50, 95, 14);
            Checkpoint(corridor, 52, 3, "Коридор", "", new Vector3(0, .08f, 53));
            for (int i = 0; i < 6; i++)
            {
                float z = 57 + i * 6;
                Wall(corridor, new Vector3(i % 2 == 0 ? -2 : 2, 2.5f, z), new Vector3(10, 5, .7f));
                if (i % 2 == 0)
                    Guard(corridor, new Vector3(3, .05f, z + 2), new Vector3(-2, .05f, z + 2), new Vector3(3, .05f, z + 3));
            }
            var reception = Node("06 — Приёмная", world).transform;
            RoomSides(reception, 140, 180); Slab(reception, 140, 180, 14);
            Checkpoint(reception, 143, 6, "Приёмная", "", new Vector3(0, .08f, 144));
            for (int i = 0; i < 4; i++)
            {
                float z = 150 + i * 7;
                Wall(reception, new Vector3(i % 2 == 0 ? 2 : -2, 2.5f, z), new Vector3(10, 5, .7f));
                if (i < 3)
                    Guard(reception, new Vector3(0, .05f, z + 3), new Vector3(-3, .05f, z + 2), new Vector3(3, .05f, z + 4));
            }
            Gateway(reception, 180, false);
        }
        static void Throne(Transform root, Vector3 p)
        {
            var t = Node("Throne", root, p).transform;
            Box("Seat", t, new Vector3(0, 0.65f, 0), new Vector3(2, 0.4f, 1.6f), gold, true);
            Box("Back", t, new Vector3(0, 1.9f, 0.65f), new Vector3(2, 2.4f, 0.35f), gold, true);
            Box("Cushion", t, new Vector3(0, 1.8f, 0.43f), new Vector3(1.5f, 1.65f, 0.2f), violet);
            Box("Crown detail", t, new Vector3(0, 3.1f, 0.65f), new Vector3(0.7f, 0.7f, 0.35f), gold).transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        static void InteractionCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>(); c.isTrigger = true; c.center = center; c.size = size;
            // Kinematic body makes trigger events reliable with CharacterController.
            var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
        }
        static void Key(Transform root, Vector3 p)
        {
            Box("Key pedestal", root, p + Vector3.up * 0.55f, new Vector3(1.1f, 1.1f, 1.1f), trim, true);
            var key = Node("Key — E to take", root, p + Vector3.up * 1.35f);
            var visible = Node("Rotating key", key.transform).transform;
            Shape("Key head", PrimitiveType.Sphere, visible, new Vector3(-0.23f, 0.12f, 0), new Vector3(0.4f, 0.4f, 0.16f), gold);
            Box("Key stem", visible, new Vector3(0.15f, 0.12f, 0), new Vector3(0.65f, 0.10f, 0.10f), gold);
            Box("Key tooth", visible, new Vector3(0.4f, -0.01f, 0), new Vector3(0.1f, 0.24f, 0.1f), gold);
            var item = key.AddComponent<JamInteractable>(); item.kind = JamInteractable.Kind.Key; item.movingPart = visible;
            InteractionCollider(key, Vector3.zero, Vector3.one * 0.8f);
            var light = key.AddComponent<Light>(); light.type = LightType.Point; light.range = 3; light.color = Color.yellow; light.intensity = 1.5f;
        }
        static void Lever(Transform root, Vector3 p, JamGate gate)
        {
            var lever = Node("Lever — E to open throne room", root, p);
            Shape("Base", PrimitiveType.Cylinder, lever.transform, new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.4f, 0.8f), trim, true);
            var arm = Node("Lever arm", lever.transform, Vector3.up * 0.9f).transform;
            arm.localRotation = Quaternion.Euler(-25, 0, 0);
            Box("Shaft", arm, Vector3.up * 0.35f, new Vector3(0.12f, 0.7f, 0.12f), steel);
            Shape("Handle", PrimitiveType.Sphere, arm, Vector3.up * 0.75f, Vector3.one * 0.3f, gold);
            var item = lever.AddComponent<JamInteractable>(); item.kind = JamInteractable.Kind.Lever; item.gate = gate; item.movingPart = arm;
            InteractionCollider(lever, Vector3.up, new Vector3(1, 2, 1));
        }
        static void Checkpoint(Transform root, float z, int chapter, string title, string objective, Vector3 spawnPosition)
        {
            var trigger = Node("Checkpoint — " + title, root, new Vector3(0, 1, z));
            InteractionCollider(trigger, Vector3.zero, new Vector3(12.5f, 3, 1.2f));
            var zone = trigger.AddComponent<JamZone>(); zone.chapter = chapter; zone.title = title; zone.objective = objective;
            zone.spawn = Node("Safe spawn", root, spawnPosition).transform;
        }
        static void Spikes(Transform root, float z, float offset)
        {
            var trap = Node("Timed spikes", root, new Vector3(0, 0, z));
            InteractionCollider(trap, new Vector3(0, 0.5f, 0), new Vector3(13.6f, 1, 1.2f));
            var plate = Box("Signal plate", trap.transform, new Vector3(0, 0.025f, 0), new Vector3(13.5f, 0.04f, 1.3f), steel);
            var tips = Node("Retracting spikes", trap.transform, new Vector3(0, -0.85f, 0)).transform;
            for (float x = -6.3f; x <= 6.4f; x += 0.6f)
            {
                var spike = Shape("Spike", PrimitiveType.Cube, tips, new Vector3(x, 0.45f, 0), new Vector3(0.15f, 0.8f, 0.15f), steel);
                spike.transform.localRotation = Quaternion.Euler(0, 45, 0);
            }
            var script = trap.AddComponent<JamSpikeTrap>(); script.spikes = tips; script.warning = plate.GetComponent<Renderer>(); script.offset = offset;
        }
        static void Player(JamGame game, Vector3 p)
        {
            var go = Node("PLAYER — controller", null, p); go.layer = 2;
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.85f; cc.radius = 0.34f; cc.center = new Vector3(0, 0.96f, 0); cc.stepOffset = 0.25f; cc.skinWidth = 0.04f;
            var player = go.AddComponent<JamPlayer>(); player.config = cfg;
            player.visual = CharacterVisual(go.transform, cfg.hero, blue, false, cfg.heroYaw);
            player.animator = AttachAnimator(player.visual);
            player.sword = Sword(go.transform);
            var rig = game.Camera.gameObject.AddComponent<JamCamera>(); rig.target = go.transform; rig.config = cfg;
            game.Camera.fieldOfView = 75;
            var hand = Node("First person hand and sword", game.Camera.transform, new Vector3(0.28f, -0.32f, 0.44f)).transform;
            hand.localScale = Vector3.one * 0.48f;
            Box("Sleeve", hand, new Vector3(0, -0.3f, 0), new Vector3(0.22f, 0.50f, 0.3f), blue);
            Shape("Hand", PrimitiveType.Sphere, hand, new Vector3(0, -0.02f, 0), new Vector3(0.24f, 0.24f, 0.27f), skin);
            var heldSword = Sword(hand); heldSword.localPosition = Vector3.zero;
            player.viewModel = hand;
            player.rig = rig; game.Player = player; game.Rig = rig;
        }
        static void Guard(Transform root, Vector3 p, Vector3 a, Vector3 b)
        {
            var go = Node("GUARD — one hit to knock out", root, p); go.layer = 2;
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.38f; cc.center = Vector3.up * 0.93f; cc.stepOffset = 0.25f;
            var guard = go.AddComponent<JamGuard>(); guard.config = cfg;
            guard.pointA = Node("Patrol A", root, a).transform; guard.pointB = Node("Patrol B", root, b).transform;
            guard.visual = CharacterVisual(go.transform, cfg.guard, red, false, cfg.guardYaw);
            guard.animator = AttachAnimator(guard.visual);
            string[] warnings = {
                "Стой! Принцесса велела никого не впускать!",
                "Не тревожь принцессу! Она занята!",
                "Да не нужна ей помощь!",
                "Кто потом будет убирать этот беспорядок?!",
                "Ещё можно развернуться и уйти!",
                "Нам же потом за тебя отвечать!"
            };
            guard.warningLine = warnings[guards.Count % warnings.Length];
            guards.Add(guard);
        }
        static Transform CharacterVisual(Transform parent, GameObject prefab, Material cloth, bool princess, float yaw)
        {
            var pivot = Node("Visual", parent).transform;
            if (prefab != null)
            {
                var facing = Node("Model facing", pivot).transform; facing.localRotation = Quaternion.Euler(0, yaw, 0);
                var art = FitVisual(prefab, facing, Vector3.zero, new Vector3(1.2f, 1.9f, 1.2f), true, true, princess);
                if (cfg.suppliedArt) AdaptCharacterMaterials(art);
            }
            else
            {
                Shape("Head", PrimitiveType.Sphere, pivot, new Vector3(0, 1.6f, 0), new Vector3(0.6f, 0.57f, 0.57f), skin);
                Box("Torso", pivot, new Vector3(0, 1.05f, 0), new Vector3(0.70f, 0.65f, 0.4f), cloth);
                if (princess)
                {
                    Shape("Dress", PrimitiveType.Cylinder, pivot, new Vector3(0, 0.52f, 0), new Vector3(1.05f, 0.5f, 1.05f), cloth);
                    Box("Hair", pivot, new Vector3(0, 1.58f, -0.2f), new Vector3(0.63f, 0.73f, 0.32f), wood);
                    Shape("Crown band", PrimitiveType.Cylinder, pivot, new Vector3(0, 1.93f, 0), new Vector3(0.57f, 0.08f, 0.57f), gold);
                    for (int i = -1; i <= 1; i++) Box("Crown tooth", pivot, new Vector3(i * 0.19f, 2.06f, 0.18f), new Vector3(0.08f, 0.21f, 0.1f), gold);
                }
                else
                {
                    Shape("Helmet", PrimitiveType.Sphere, pivot, new Vector3(0, 1.76f, -0.05f), new Vector3(0.7f, 0.48f, 0.62f), steel);
                    Box("Nose guard", pivot, new Vector3(0, 1.65f, 0.29f), new Vector3(0.08f, 0.35f, 0.1f), steel);
                    Box("Plume", pivot, new Vector3(0, 2.01f, -0.03f), new Vector3(0.13f, 0.25f, 0.49f), cloth);
                    foreach (float x in new[] { -0.2f, 0.2f })
                    {
                        Box("Leg", pivot, new Vector3(x, 0.40f, 0), new Vector3(0.26f, 0.65f, 0.29f), darkStone);
                        Box("Boot", pivot, new Vector3(x, 0.12f, 0.1f), new Vector3(0.29f, 0.24f, 0.48f), wood);
                    }
                    Box("Belt", pivot, new Vector3(0, 0.87f, 0), new Vector3(0.75f, 0.12f, 0.44f), gold);
                }
                foreach (float x in new[] { -0.47f, 0.47f })
                {
                    Box("Arm", pivot, new Vector3(x, 1.05f, 0), new Vector3(0.22f, 0.55f, 0.25f), cloth);
                    Shape("Hand", PrimitiveType.Sphere, pivot, new Vector3(x, 0.78f, 0.07f), Vector3.one * 0.25f, skin);
                    Box("Eye", pivot, new Vector3(x * 0.25f, 1.63f, 0.271f), new Vector3(0.055f, 0.055f, 0.04f), darkStone);
                }
            }
            return pivot;
        }
        static Transform Sword(Transform parent)
        {
            var sword = Node("Sword pivot", parent, new Vector3(0.6f, 0.86f, 0.18f)).transform;
            Box("Grip", sword, new Vector3(0, 0, 0), new Vector3(0.1f, 0.28f, 0.1f), wood);
            Box("Guard", sword, new Vector3(0, 0.19f, 0), new Vector3(0.42f, 0.08f, 0.12f), gold);
            Box("Blade", sword, new Vector3(0, 0.64f, 0), new Vector3(0.15f, 0.84f, 0.045f), steel);
            return sword;
        }
        static Transform Broom(Transform parent)
        {
            var broom = Node("Finale broom", parent, new Vector3(0.6f, 0, 0.15f)).transform;
            broom.localRotation = Quaternion.Euler(-12, 0, -12);
            Box("Handle", broom, Vector3.up, new Vector3(0.07f, 1.85f, 0.07f), wood);
            Box("Brush", broom, new Vector3(0, 0.15f, 0), new Vector3(0.65f, 0.26f, 0.30f), trim);
            for (int i = -3; i <= 3; i++) Box("Bristle", broom, new Vector3(i * 0.08f, 0.05f, 0), new Vector3(0.03f, 0.18f, 0.29f), gold);
            return broom;
        }
        static GameObject FitVisual(GameObject prefab, Transform parent, Vector3 feet, Vector3 targetSize, bool uniform, bool heightOnly = false, bool removeWeapons = false)
        {
            // Work on a scene instance only; vendor prefabs and their materials remain untouched.
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Art — " + prefab.name;
            model.transform.SetParent(parent, false); model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
            foreach (var c in model.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
            foreach (var l in model.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(l);
            foreach (var s in model.GetComponentsInChildren<AudioSource>(true)) s.enabled = false;
            foreach (var l in model.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) { a.applyRootMotion = false; a.enabled = false; }
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            if (removeWeapons) HideWeapons(model);
            var renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) { Debug.LogWarning("Mini Jam: prefab has no Renderer: " + prefab.name); return model; }
            Bounds bounds = LocalBounds(model.transform, renderers);
            Vector3 size = bounds.size;
            Vector3 scale = new Vector3(targetSize.x / Mathf.Max(size.x, 0.01f), targetSize.y / Mathf.Max(size.y, 0.01f), targetSize.z / Mathf.Max(size.z, 0.01f));
            if (uniform) scale = Vector3.one * (heightOnly ? scale.y : Mathf.Min(scale.x, scale.y, scale.z));
            model.transform.localScale = scale;
            model.transform.localPosition = feet - Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
            return model;
        }
        static void HideWeapons(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (IsWeaponName(t.name))
                    t.gameObject.SetActive(false);
            }
        }
        static bool IsWeaponName(string name)
        {
            string n = name.ToLowerInvariant();
            return n.StartsWith("ohs", StringComparison.Ordinal) || n.StartsWith("shield", StringComparison.Ordinal) || n.Contains("sword");
        }
        static void Paint(GameObject model, Material material)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => material).ToArray();
        }
        static void AdaptCharacterMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var list = renderer.sharedMaterials;
                for (int i = 0; i < list.Length; i++)
                {
                    var source = list[i]; if (source == null) continue;
                    if (!characterMaterials.TryGetValue(source, out Material converted))
                    {
                        string key = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                        if (string.IsNullOrEmpty(key)) key = source.GetInstanceID().ToString();
                        Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                        converted = Mat("Character_" + key, color, 0.1f);
                        Texture albedo = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                        if (albedo == null && source.HasProperty("_BaseMap")) albedo = source.GetTexture("_BaseMap");
                        if (converted.HasProperty("_BaseMap")) converted.SetTexture("_BaseMap", albedo);
                        if (converted.HasProperty("_MainTex")) converted.SetTexture("_MainTex", albedo);
                        EditorUtility.SetDirty(converted); characterMaterials[source] = converted;
                    }
                    list[i] = converted;
                }
                renderer.sharedMaterials = list;
            }
        }
        static Bounds LocalBounds(Transform model, Renderer[] renderers)
        {
            bool first = true; Bounds result = new Bounds();
            foreach (var r in renderers)
            {
                // Transform every local AABB corner; works for rotated imported meshes and skins.
                Bounds b = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = model.InverseTransformPoint(r.transform.TransformPoint(p));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            return result;
        }
        static void CreateAnimationController()
        {
            controller = null;
            if (cfg.idle == null && cfg.walk == null && cfg.attack == null) return;
            string path = cfg.suppliedArt ? Root + "/InteriorCharacter.controller" : Root + "/Character.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float); ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var machine = ac.layers[0].stateMachine;
            var idle = machine.AddState("Idle"); idle.motion = cfg.idle; machine.defaultState = idle;
            if (cfg.walk != null)
            {
                var walk = machine.AddState("Walk"); walk.motion = cfg.walk;
                var a = idle.AddTransition(walk); a.hasExitTime = false; a.duration = 0.12f; a.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                var b = walk.AddTransition(idle); b.hasExitTime = false; b.duration = 0.12f; b.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
            if (cfg.attack != null)
            {
                var attack = machine.AddState("Attack"); attack.motion = cfg.attack;
                var a = machine.AddAnyStateTransition(attack); a.hasExitTime = false; a.duration = 0.04f; a.canTransitionToSelf = false; a.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                var b = attack.AddTransition(idle); b.hasExitTime = true; b.exitTime = 0.9f; b.duration = 0.1f;
            }
            controller = ac;
        }
        static Animator AttachAnimator(Transform visual)
        {
            if (controller == null) return null;
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) { Debug.LogWarning("Mini Jam: no Animator on model. Using simple movement."); return null; }
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.enabled = true; return animator;
        }
        static void EnsureBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { IntroPath, CastlePath })
            {
                var existing = scenes.FirstOrDefault(s => s.path == path);
                if (existing != null) existing.enabled = true; else scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        [MenuItem("Mini Jam/2. Проверить сцены")]
        public static void Validate()
        {
            if (!File.Exists(IntroPath) || !File.Exists(CastlePath)) { Debug.LogError("Mini Jam: сначала создай сцены через Mini Jam → 1. Создать игру."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var errors = new List<string>();
            try
            {
                foreach (string path in new[] { IntroPath, CastlePath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    var games = roots.SelectMany(g => g.GetComponentsInChildren<JamGame>(true)).ToArray();
                    if (games.Length != 1) { errors.Add(path + ": нужен ровно один JamGame."); continue; }
                    var g = games[0];
                    if (g.config == null || g.Camera == null || g.Sound == null) errors.Add(path + ": отсутствует конфигурация, камера или звук.");
                    if (roots.SelectMany(o => o.GetComponentsInChildren<AudioListener>(true)).Count() != 1) errors.Add(path + ": нужен один AudioListener.");
                    foreach (var t in roots.SelectMany(o => o.GetComponentsInChildren<Transform>(true)))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) errors.Add(path + ": Missing Script на " + t.name);
                    if (g.introScene)
                    {
                        if (g.introCameraStart == null || g.introCameraEnd == null) errors.Add("Intro: отсутствуют позиции камеры.");
                    }
                    else
                    {
                        if (g.Player == null || g.Rig == null || g.finalBars == null || g.finalCamera == null || g.finalPlayerSpot == null || g.broom == null) errors.Add("Castle: отсутствуют ссылки игрока/финала.");
                        if (g.Player != null && g.Player.viewModel == null) errors.Add("Castle: отсутствует меч перед камерой.");
                        if (g.config != null && g.config.suppliedArt)
                        {
                            if (g.Video == null || !File.Exists("Assets/StreamingAssets/MiniJam/twist.mp4")) errors.Add("Castle: не подключено финальное видео.");
                            if (g.config.guard == null || g.config.princess == null || g.config.wallModule == null || g.config.floorModule == null) errors.Add("Castle: не назначены присланные модели.");
                            var princess = roots.SelectMany(o => o.GetComponentsInChildren<JamInteractable>(true)).FirstOrDefault(i => i.kind == JamInteractable.Kind.Princess);
                            if (princess == null || princess.GetComponent<BoxCollider>() == null || princess.GetComponent<BoxCollider>().size.z < 6) errors.Add("Castle: обнови интерьер для автоматического начала финала.");
                            if (roots.SelectMany(o => o.GetComponentsInChildren<JamBreakable>(true)).Count() < 8) errors.Add("Castle: отсутствуют разрушаемые ящики; обнови интерьер.");
                            if (g.guards == null || g.guards.Length != 11) errors.Add("Castle: ожидаются 11 стражников; обнови интерьер.");
                            if (princess != null && princess.GetComponentsInChildren<Renderer>().Any(r => r.enabled && IsWeaponName(r.name))) errors.Add("Castle: у принцессы осталось видимое оружие.");
                        }
                        foreach (var guard in g.guards)
                            if (guard == null || guard.pointA == null || guard.pointB == null || guard.visual == null) errors.Add("Castle: не настроен стражник.");
                        foreach (var item in roots.SelectMany(o => o.GetComponentsInChildren<JamInteractable>(true)))
                            if ((item.kind == JamInteractable.Kind.Lever || item.kind == JamInteractable.Kind.LockedGate) && item.gate == null) errors.Add("Castle: не назначена решётка для " + item.name);
                        foreach (var zone in roots.SelectMany(o => o.GetComponentsInChildren<JamZone>(true)))
                            if (zone.kind == JamZone.Kind.Checkpoint && zone.spawn == null) errors.Add("Castle: у checkpoint нет spawn.");
                    }
                }
                EnsureBuildScenes();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            if (errors.Count == 0) { Debug.Log("Mini Jam: ссылки и Build Settings проверены. Следующий шаг — Play и прохождение по CHECKLIST_RU.md."); EditorUtility.DisplayDialog("Проверка пройдена", "Ссылки компонентов и сцены сборки в порядке. Проверь прохождение в Play Mode.", "OK"); }
            else { foreach (string error in errors) Debug.LogError("Mini Jam: " + error); EditorUtility.DisplayDialog("Найдены проблемы", string.Join("\n", errors), "OK"); }
        }
        [MenuItem("Mini Jam/3. Собрать Windows")]
        public static void BuildWindows()
        {
            if (!File.Exists(IntroPath) || !File.Exists(CastlePath)) { Debug.LogError("Сначала создай сцены игры."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            { EditorUtility.DisplayDialog("Нужен модуль Windows", "Unity Hub → Installs → шестерёнка у Unity → Add modules → Windows Build Support.", "OK"); return; }
            string folder = EditorUtility.SaveFolderPanel("Куда сохранить игру", "", "SaveThePrincess_Build");
            if (string.IsNullOrEmpty(folder)) return;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { IntroPath, CastlePath },
                locationPathName = Path.Combine(folder, "SaveThePrincess.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result == BuildResult.Succeeded) { EditorUtility.RevealInFinder(Path.Combine(folder, "SaveThePrincess.exe")); Debug.Log("Mini Jam: Windows build ready."); }
            else Debug.LogError("Mini Jam: сборка не завершена, см. Console. " + report.summary.result);
        }
    }
}

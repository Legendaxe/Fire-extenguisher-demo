using System;
using System.IO;
using FireExtinguisher.Interaction;
using FireExtinguisher.Player;
using FireExtinguisher.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CameraShake = FireExtinguisher.Rocket.CameraFeedback;
using ExtinguisherEffect = FireExtinguisher.Rocket.ExtinguisherVFX;
using ExtinguisherItem = FireExtinguisher.Rocket.FireExtinguisher;
using ImpactEffects = FireExtinguisher.Rocket.FunnyImpactEffects;
using ImpactFx = FireExtinguisher.Rocket.FunnyImpactFX;
using RiderGate = FireExtinguisher.Rocket.RiderImpactGate;
using BreakableGlassPane = FireExtinguisher.Rocket.BreakableGlass;
using RocketController = FireExtinguisher.Rocket.RocketChairController;

namespace FireExtinguisher.EditorTools
{
    /// <summary>
    /// Builds every asset for the rocket chair feature: materials, textures, the fire extinguisher
    /// prefab, the impact FX prefab, chair and player prefab upgrades, and the scene wiring.
    /// Idempotent: rerunning replaces generated content instead of duplicating it.
    /// </summary>
    public static class RocketChairSetup
    {
        private const string MaterialsFolder = "Assets/Materials/RocketOffice";
        private const string TexturesFolder = "Assets/Textures/RocketOffice";
        private const string AudioFolder = "Assets/Audio/RocketOffice";
        private const string PrefabsFolder = "Assets/Prefabs/RocketOffice";
        private const string FxFolder = "Assets/Prefabs/RocketOffice/FX";
        private const string PhysicsFolder = "Assets/Physics/RocketOffice";
        private const string ChairsFolder = "Assets/Settings/Chairs";
        private const string ScenePath = "Assets/Scenes/RocketOffice.unity";

        private const string ChairPrefabPath = PrefabsFolder + "/PF_OfficeChair.prefab";
        private const string ExtinguisherPrefabPath = PrefabsFolder + "/PF_FireExtinguisher.prefab";
        private const string ImpactFxPrefabPath = FxFolder + "/PF_FunnyImpactFX.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
        private const string WheelPhysicsPath = PhysicsFolder + "/PM_ChairWheel.physicMaterial";
        private const string RocketConfigPath = ChairsFolder + "/ChairSittingConfig_Rocket.asset";
        private const string DefaultPropPhysicsPath = PhysicsFolder + "/PM_DefaultProp.physicMaterial";

        private const string DemoChairName = "RocketChair_Demo";
        private static readonly Vector3 DemoChairPosition = new Vector3(-2.6f, 0f, -13.6f);

        private const string GlassPartitionName = "GlassPartition";
        private static readonly Vector3 GlassPartitionPosition = new Vector3(5f, 0f, -2f);
        private const float GlassPartitionWidth = 4.4f;
        private const float GlassPartitionHeight = 2.4f;
        private const int GlassShardColumns = 8;
        private const int GlassShardRows = 6;

        private sealed class MaterialSet
        {
            public Material Red;
            public Material Dark;
            public Material Metal;
            public Material Label;
            public Material Gauge;
            public Material Puff;
            public Material Star;
            public Material Glass;
        }

        [MenuItem("Tools/Rocket Chair/Rebuild All Assets")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);
            EnsureFolder(AudioFolder);
            EnsureFolder(FxFolder);
            EnsureFolder(PhysicsFolder);
            EnsureFolder(ChairsFolder);

            MaterialSet materials = EnsureMaterials();
            AudioSet audio = EnsureAudio();
            PhysicsMaterial wheelPhysics = EnsureWheelPhysics();
            ChairSittingConfig rocketConfig = EnsureRocketConfig();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ImpactFx impactFx = BuildImpactFxPrefab(materials);
            ExtinguisherItem extinguisherPrefab = BuildExtinguisherPrefab(materials, audio);

            UpgradeChairPrefab(wheelPhysics, rocketConfig, impactFx, audio);
            UpgradePlayerPrefab();
            SetupScene(extinguisherPrefab);
            BuildGlassPartition(materials, audio);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("RocketChairSetup: finished. Extinguisher, chair, player and scene are wired.");
        }

        // ------------------------------------------------------------------ folders and assets

        /// <summary>Converts a project-relative asset path into an absolute file system path.</summary>
        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);

            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static MaterialSet EnsureMaterials()
        {
            Texture2D puffTexture = EnsureGeneratedTexture(
                TexturesFolder + "/T_SprayPuff.png", 64, PuffPixel);
            Texture2D starTexture = EnsureGeneratedTexture(
                TexturesFolder + "/T_ImpactStar.png", 64, StarPixel);
            Texture2D labelTexture = EnsureGeneratedTexture(
                TexturesFolder + "/T_WarningLabel.png", 128, LabelPixel);

            var set = new MaterialSet
            {
                Red = EnsureMaterial(MaterialsFolder + "/M_ExtinguisherRed.mat",
                    "Universal Render Pipeline/Lit", new Color(0.72f, 0.05f, 0.04f), 0.15f, 0.45f),
                Dark = EnsureMaterial(MaterialsFolder + "/M_ExtinguisherDark.mat",
                    "Universal Render Pipeline/Lit", new Color(0.055f, 0.055f, 0.065f), 0.25f, 0.35f),
                Metal = EnsureMaterial(MaterialsFolder + "/M_ExtinguisherMetal.mat",
                    "Universal Render Pipeline/Lit", new Color(0.62f, 0.64f, 0.68f), 0.85f, 0.7f),
                Gauge = EnsureMaterial(MaterialsFolder + "/M_ExtinguisherGauge.mat",
                    "Universal Render Pipeline/Lit", new Color(0.35f, 0.95f, 0.45f), 0.1f, 0.6f),
                Label = EnsureMaterial(MaterialsFolder + "/M_WarningLabel.mat",
                    "Universal Render Pipeline/Lit", Color.white, 0f, 0.3f, labelTexture),
                Puff = EnsureMaterial(MaterialsFolder + "/M_SprayPuff.mat",
                    "Universal Render Pipeline/Particles/Unlit", Color.white, 0f, 0.2f, puffTexture, true),
                Star = EnsureMaterial(MaterialsFolder + "/M_ImpactStar.mat",
                    "Universal Render Pipeline/Particles/Unlit", Color.white, 0f, 0.2f, starTexture, true),
                Glass = EnsureMaterial(MaterialsFolder + "/M_Glass.mat",
                    "Universal Render Pipeline/Lit", new Color(0.62f, 0.82f, 0.9f, 0.32f), 0.05f, 0.95f,
                    null, true)
            };

            return set;
        }

        private static Material EnsureMaterial(
            string path,
            string shaderName,
            Color color,
            float metallic,
            float smoothness,
            Texture2D texture = null,
            bool transparent = false)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Shader not found: " + shaderName);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }

            if (transparent)
                MakeTransparent(material);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        private static Texture2D EnsureGeneratedTexture(string path, int size, Func<int, int, int, Color> pixel)
        {
            string absolutePath = ToAbsolutePath(path);

            if (!File.Exists(absolutePath))
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                        pixels[y * size + x] = pixel(x, y, size);
                }

                texture.SetPixels(pixels);
                texture.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ToAbsolutePath(TexturesFolder));
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color PuffPixel(int x, int y, int size)
        {
            float half = size * 0.5f;
            float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / (size * 0.46f);
            float alpha = Mathf.Clamp01(1f - distance);
            alpha *= alpha;

            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color StarPixel(int x, int y, int size)
        {
            float center = size * 0.5f;
            float dx = x + 0.5f - center;
            float dy = y + 0.5f - center;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);

            float angle = Mathf.Atan2(dy, dx) + Mathf.PI * 0.5f;
            float sector = Mathf.PI * 2f / 5f;
            float wrapped = Mathf.Repeat(angle, sector);
            float point = Mathf.Abs(wrapped - sector * 0.5f) / (sector * 0.5f);
            float radius = Mathf.Lerp(size * 0.17f, size * 0.46f, point);

            float alpha = Mathf.Clamp01(radius - distance + 0.75f);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color LabelPixel(int x, int y, int size)
        {
            Color yellow = new Color(1f, 0.82f, 0.12f, 1f);
            Color black = new Color(0.05f, 0.05f, 0.05f, 1f);
            Vector2 point = new Vector2(x + 0.5f, y + 0.5f);

            // Border.
            if (x < 5 || y < 5 || x >= size - 5 || y >= size - 5)
                return black;

            // Warning triangle outline.
            Vector2 a = new Vector2(64f, 16f);
            Vector2 b = new Vector2(14f, 108f);
            Vector2 c = new Vector2(114f, 108f);

            if (SegmentDistance(point, a, b) < 4f ||
                SegmentDistance(point, b, c) < 4f ||
                SegmentDistance(point, c, a) < 4f)
                return black;

            // Exclamation mark.
            if (x >= 59 && x <= 68 && y >= 44 && y <= 80)
                return black;
            if (Vector2.Distance(point, new Vector2(63.5f, 92f)) <= 5f)
                return black;

            return yellow;
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = Mathf.Max(0.0001f, ab.sqrMagnitude);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            return Vector2.Distance(point, a + ab * t);
        }

        private static PhysicsMaterial EnsureWheelPhysics()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(WheelPhysicsPath);
            if (material == null)
            {
                material = new PhysicsMaterial("PM_ChairWheel");
                AssetDatabase.CreateAsset(material, WheelPhysicsPath);
            }

            material.dynamicFriction = 0.35f;
            material.staticFriction = 0.45f;
            material.bounciness = 0.02f;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static ChairSittingConfig EnsureRocketConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ChairSittingConfig>(RocketConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ChairSittingConfig>();
                AssetDatabase.CreateAsset(config, RocketConfigPath);
            }

            var serialized = new SerializedObject(config);
            serialized.FindProperty("sitPrompt").stringValue = "Sit";
            serialized.FindProperty("standPrompt").stringValue = "Stand";
            serialized.FindProperty("seatedEyeHeight").floatValue = 0.72f;
            serialized.FindProperty("seatedYawLimit").floatValue = 180f;
            serialized.FindProperty("standUpCooldown").floatValue = 0.25f;
            serialized.FindProperty("standProbeDistance").floatValue = 0.85f;
            serialized.FindProperty("standProbeLift").floatValue = 0.05f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(config);
            return config;
        }

        // ------------------------------------------------------------------ audio

        /// <summary>Generated placeholder clips handed to the prefab builders.</summary>
        private sealed class AudioSet
        {
            public AudioClip Discharge;
            public AudioClip Start;
            public AudioClip Stop;
            public AudioClip ImpactSmall;
            public AudioClip ImpactMedium;
            public AudioClip ImpactBig;
            public AudioClip GlassBreak;
        }

        /// <summary>
        /// Loads the WAVs written by "Tools/Audio/generate_rocket_audio.py" and pins import
        /// settings that suit short sound effects. A missing file logs a warning and leaves the
        /// slot empty instead of failing the whole rebuild.
        /// </summary>
        private static AudioSet EnsureAudio()
        {
            return new AudioSet
            {
                Discharge = LoadAudioClip("A_ExtinguisherDischarge.wav"),
                Start = LoadAudioClip("A_ExtinguisherStart.wav"),
                Stop = LoadAudioClip("A_ExtinguisherStop.wav"),
                ImpactSmall = LoadAudioClip("A_ImpactSmall.wav"),
                ImpactMedium = LoadAudioClip("A_ImpactMedium.wav"),
                ImpactBig = LoadAudioClip("A_ImpactBig.wav"),
                GlassBreak = LoadAudioClip("A_GlassBreak.wav")
            };
        }

        private static AudioClip LoadAudioClip(string fileName)
        {
            string path = AudioFolder + "/" + fileName;
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
            {
                Debug.LogWarning(
                    $"RocketChairSetup: missing audio clip '{path}'. Run " +
                    "\"python Tools/Audio/generate_rocket_audio.py\" and rebuild. " +
                    "The slot stays empty until then.");
                return null;
            }

            ConfigureAudioImport(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Mono, decompressed on load and preloaded: the right setup for short SFX.</summary>
        private static void ConfigureAudioImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool upToDate =
                importer.forceToMono &&
                !importer.loadInBackground &&
                settings.preloadAudioData &&
                settings.loadType == AudioClipLoadType.DecompressOnLoad &&
                settings.compressionFormat == AudioCompressionFormat.PCM &&
                settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate;

            if (upToDate)
                return;

            importer.forceToMono = true;
            importer.loadInBackground = false;

            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            settings.quality = 1f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ particle helpers

        private static ParticleSystem CreateParticles(
            Transform parent,
            string name,
            Vector3 localPosition,
            Material material,
            ParticleSystemSimulationSpace space)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;

            var system = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                renderer = go.AddComponent<ParticleSystemRenderer>();

            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingFudge = 2f;

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = space;
            main.maxParticles = 800;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.015f;

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        /// <summary>Continuous white powder stream fired along the nozzle's +Z.</summary>
        private static void ConfigureStream(ParticleSystem system)
        {
            var main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.95f),
                new Color(0.84f, 0.87f, 0.9f, 0.55f));
            main.gravityModifier = 0.06f;

            var emission = system.emission;
            emission.rateOverTime = 130f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.012f;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.9f));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.85f, 0.87f, 0.9f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.55f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
        }

        /// <summary>Small extra puff when the trigger is first pulled.</summary>
        private static void ConfigureBurst(ParticleSystem system)
        {
            var main = system.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.9f),
                new Color(0.9f, 0.92f, 0.95f, 0.6f));
            main.gravityModifier = 0.15f;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2.2f));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Smoke puffs left behind while the chair is moving fast.</summary>
        private static void ConfigureTrail(ParticleSystem system)
        {
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.95f, 0.96f, 1f, 0.35f),
                new Color(0.8f, 0.82f, 0.85f, 0.2f));
            main.gravityModifier = -0.03f;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.4f));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // ------------------------------------------------------------------ impact FX prefab

        private static ImpactFx BuildImpactFxPrefab(MaterialSet materials)
        {
            GameObject root = new GameObject("PF_FunnyImpactFX");

            var fx = root.AddComponent<ImpactFx>();

            ParticleSystem puff = CreateParticles(root.transform, "Puff", Vector3.zero, materials.Puff,
                ParticleSystemSimulationSpace.World);
            var puffMain = puff.main;
            puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1f);
            puffMain.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 4.2f);
            puffMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
            puffMain.gravityModifier = -0.12f;
            puffMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.98f, 0.9f, 0.9f),
                new Color(0.9f, 0.92f, 0.95f, 0.55f));
            var puffShape = puff.shape;
            puffShape.shapeType = ParticleSystemShapeType.Sphere;
            puffShape.radius = 0.14f;
            var puffSize = puff.sizeOverLifetime;
            puffSize.enabled = true;
            puffSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 2.1f));
            var puffColor = puff.colorOverLifetime;
            puffColor.enabled = true;
            var puffGradient = new Gradient();
            puffGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            puffColor.color = new ParticleSystem.MinMaxGradient(puffGradient);

            ParticleSystem stars = CreateParticles(root.transform, "Stars", Vector3.zero, materials.Star,
                ParticleSystemSimulationSpace.World);
            var starMain = stars.main;
            starMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            starMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8f);
            starMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            starMain.gravityModifier = 1.1f;
            starMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.9f, 0.25f, 1f),
                new Color(1f, 0.45f, 0.1f, 1f));
            var starShape = stars.shape;
            starShape.shapeType = ParticleSystemShapeType.Sphere;
            starShape.radius = 0.05f;
            var starRotation = stars.rotationOverLifetime;
            starRotation.enabled = true;
            starRotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var starColor = stars.colorOverLifetime;
            starColor.enabled = true;
            var starGradient = new Gradient();
            starGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            starColor.color = new ParticleSystem.MinMaxGradient(starGradient);

            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(root.transform, false);
            Light flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 4f;
            flash.intensity = 0f;
            flash.color = new Color(1f, 0.9f, 0.6f);
            flash.enabled = false;

            SetObject(fx, "puff", puff);
            SetObject(fx, "stars", stars);
            SetObject(fx, "flash", flash);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactFxPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            return prefab.GetComponent<ImpactFx>();
        }

        // ------------------------------------------------------------------ extinguisher prefab

        private static ExtinguisherItem BuildExtinguisherPrefab(MaterialSet materials, AudioSet audio)
        {
            PhysicsMaterial propPhysics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(DefaultPropPhysicsPath);

            GameObject root = new GameObject("PF_FireExtinguisher");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // Tank.
            var tank = new GameObject("Tank");
            tank.transform.SetParent(visual.transform, false);

            Primitive(PrimitiveType.Cylinder, "TankBody", tank.transform,
                new Vector3(0f, 0.27f, 0f), new Vector3(0.22f, 0.2f, 0.22f), materials.Red);
            Primitive(PrimitiveType.Sphere, "TankBottom", tank.transform,
                new Vector3(0f, 0.08f, 0f), new Vector3(0.21f, 0.07f, 0.21f), materials.Red);
            Primitive(PrimitiveType.Sphere, "TankTop", tank.transform,
                new Vector3(0f, 0.46f, 0f), new Vector3(0.21f, 0.07f, 0.21f), materials.Red);

            // Label sits proud of the curved tank so it reads from the front.
            Primitive(PrimitiveType.Cube, "Label", tank.transform,
                new Vector3(0f, 0.29f, 0.108f), new Vector3(0.115f, 0.175f, 0.004f), materials.Label);

            // Valve and neck.
            var valve = new GameObject("Valve");
            valve.transform.SetParent(visual.transform, false);

            Primitive(PrimitiveType.Cylinder, "Neck", valve.transform,
                new Vector3(0f, 0.485f, 0f), new Vector3(0.07f, 0.03f, 0.07f), materials.Metal);
            Primitive(PrimitiveType.Cube, "ValveBody", valve.transform,
                new Vector3(0f, 0.525f, 0f), new Vector3(0.1f, 0.05f, 0.085f), materials.Dark);
            Primitive(PrimitiveType.Cylinder, "ValveCap", valve.transform,
                new Vector3(0f, 0.556f, -0.008f), new Vector3(0.05f, 0.018f, 0.05f), materials.Dark);

            // Handle and trigger.
            var handle = new GameObject("Handle");
            handle.transform.SetParent(visual.transform, false);
            Primitive(PrimitiveType.Cube, "HandleBar", handle.transform,
                new Vector3(0f, 0.568f, -0.05f), new Vector3(0.1f, 0.02f, 0.14f), materials.Dark);
            Primitive(PrimitiveType.Cube, "HandleGrip", handle.transform,
                new Vector3(0f, 0.552f, -0.108f), new Vector3(0.095f, 0.045f, 0.03f), materials.Dark);

            Primitive(PrimitiveType.Cube, "Trigger", visual.transform,
                new Vector3(0f, 0.5f, 0.055f), new Vector3(0.035f, 0.055f, 0.028f), materials.Metal,
                Quaternion.Euler(12f, 0f, 0f));

            // Pressure gauge.
            var gauge = new GameObject("Gauge");
            gauge.transform.SetParent(visual.transform, false);
            Primitive(PrimitiveType.Cylinder, "GaugeRim", gauge.transform,
                new Vector3(0.042f, 0.42f, 0.108f), new Vector3(0.058f, 0.008f, 0.058f), materials.Metal,
                Quaternion.Euler(90f, 0f, 0f));
            Primitive(PrimitiveType.Cylinder, "GaugeFace", gauge.transform,
                new Vector3(0.042f, 0.42f, 0.118f), new Vector3(0.04f, 0.004f, 0.04f), materials.Gauge,
                Quaternion.Euler(90f, 0f, 0f));

            // Hose: a chain of short cylinders hanging down the right side.
            var hose = new GameObject("Hose");
            hose.transform.SetParent(visual.transform, false);

            Vector3[] hosePoints =
            {
                new Vector3(0.052f, 0.5f, 0.0f),
                new Vector3(0.108f, 0.455f, 0.012f),
                new Vector3(0.142f, 0.385f, 0.032f),
                new Vector3(0.15f, 0.31f, 0.045f),
                new Vector3(0.13f, 0.245f, 0.03f),
                new Vector3(0.105f, 0.205f, 0.0f)
            };

            for (int i = 0; i < hosePoints.Length - 1; i++)
            {
                CreateSegment(hose.transform, "HoseSegment_" + i, hosePoints[i], hosePoints[i + 1],
                    0.018f, materials.Dark);
            }

            // Nozzle, fixed forward. No independent aiming.
            var nozzle = new GameObject("Nozzle");
            nozzle.transform.SetParent(visual.transform, false);
            nozzle.transform.localPosition = new Vector3(0f, 0.505f, 0.105f);
            nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(PrimitiveType.Cylinder, "NozzleHorn", nozzle.transform,
                Vector3.zero, new Vector3(0.045f, 0.055f, 0.045f), materials.Dark,
                Quaternion.identity, parentAlreadyPositioned: true);
            Primitive(PrimitiveType.Cylinder, "NozzleTipRing", nozzle.transform,
                new Vector3(0f, 0.058f, 0f), new Vector3(0.055f, 0.02f, 0.055f), materials.Metal,
                Quaternion.identity, parentAlreadyPositioned: true);

            var nozzleTip = new GameObject("NozzleTip");
            nozzleTip.transform.SetParent(nozzle.transform, false);
            nozzleTip.transform.localPosition = new Vector3(0f, 0.062f, 0f);
            nozzleTip.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // Visual effects live at the nozzle mouth, pointing forward.
            var visualEffects = new GameObject("VisualEffects");
            visualEffects.transform.SetParent(root.transform, false);
            visualEffects.transform.localPosition = new Vector3(0f, 0.505f, 0.17f);

            ParticleSystem stream = CreateParticles(visualEffects.transform, "Spray", Vector3.zero,
                materials.Puff, ParticleSystemSimulationSpace.World);
            ConfigureStream(stream);

            ParticleSystem burst = CreateParticles(visualEffects.transform, "Burst", Vector3.zero,
                materials.Puff, ParticleSystemSimulationSpace.World);
            ConfigureBurst(burst);

            ParticleSystem trail = CreateParticles(visualEffects.transform, "SmokeTrail", Vector3.zero,
                materials.Puff, ParticleSystemSimulationSpace.World);
            ConfigureTrail(trail);

            var vfx = visualEffects.AddComponent<ExtinguisherEffect>();
            SetObject(vfx, "stream", stream);
            SetObject(vfx, "burst", burst);
            SetObject(vfx, "trail", trail);

            // Physics.
            var body = root.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.linearDamping = 0.1f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.24f, 0.6f, 0.24f);
            collider.center = new Vector3(0f, 0.3f, 0f);
            collider.material = propPhysics;

            // Audio: generated placeholder clips, see Tools/Audio/generate_rocket_audio.py.
            AudioSource discharge = AddAudioSource(root, "DischargeAudio", true, 0.8f);
            AudioSource start = AddAudioSource(root, "StartAudio", false, 0.9f);
            AudioSource stop = AddAudioSource(root, "StopAudio", false, 0.7f);

            var extinguisher = root.AddComponent<ExtinguisherItem>();
            SetObject(extinguisher, "vfx", vfx);
            SetObject(extinguisher, "body", body);
            SetObjectArray(extinguisher, "worldColliders", new UnityEngine.Object[] { collider });
            SetObject(extinguisher, "dischargeSource", discharge);
            SetObject(extinguisher, "startSource", start);
            SetObject(extinguisher, "stopSource", stop);
            SetObject(extinguisher, "dischargeClip", audio.Discharge);
            SetObject(extinguisher, "startClip", audio.Start);
            SetObject(extinguisher, "stopClip", audio.Stop);
            SetFloat(extinguisher, "fuelCapacity", 100f);
            SetFloat(extinguisher, "consumptionPerSecond", 7.5f);
            SetBoolean(extinguisher, "startFull", true);
            SetVector3(extinguisher, "handLocalPosition", Vector3.zero);
            SetVector3(extinguisher, "handLocalEulerAngles", new Vector3(0f, -20f, 0f));
            SetVector3(extinguisher, "handLocalScale", new Vector3(0.4f, 0.4f, 0.4f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExtinguisherPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            return prefab.GetComponent<ExtinguisherItem>();
        }

        private static AudioSource AddAudioSource(GameObject root, string name, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0.35f;
            source.dopplerLevel = 0f;
            return source;
        }

        private static void CreateSegment(
            Transform parent,
            string name,
            Vector3 from,
            Vector3 to,
            float radius,
            Material material)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.0001f)
                return;

            Primitive(PrimitiveType.Cylinder, name, parent, (from + to) * 0.5f,
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), material,
                Quaternion.FromToRotation(Vector3.up, direction / length));
        }

        private static GameObject Primitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null,
            bool keepCollider = false,
            bool parentAlreadyPositioned = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            if (!parentAlreadyPositioned)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPosition;
                go.transform.localRotation = localRotation ?? Quaternion.identity;
            }
            else
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPosition;
                go.transform.localRotation = localRotation ?? Quaternion.identity;
            }

            go.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }

        // ------------------------------------------------------------------ chair prefab

        private static void UpgradeChairPrefab(
            PhysicsMaterial wheelPhysics,
            ChairSittingConfig rocketConfig,
            ImpactFx impactFx,
            AudioSet audio)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ChairPrefabPath);

            try
            {
                RemoveMissingScripts(root);

                var body = root.GetComponent<Rigidbody>();
                if (body == null)
                    body = root.AddComponent<Rigidbody>();

                body.mass = 10f;
                body.linearDamping = 0.12f;
                body.angularDamping = 0.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.centerOfMass = new Vector3(0f, 0.38f, 0.02f);
                body.maxAngularVelocity = 25f;
                body.constraints = RigidbodyConstraints.None;
                body.useGravity = true;

                PhysicsMaterial propPhysics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(DefaultPropPhysicsPath);

                // Keep the visual prefab untouched; rebuild only the physics shapes.
                Collider[] existing = root.GetComponents<Collider>();
                for (int i = 0; i < existing.Length; i++)
                    UnityEngine.Object.DestroyImmediate(existing[i]);

                var seatCollider = root.AddComponent<BoxCollider>();
                seatCollider.size = new Vector3(0.54f, 0.1f, 0.5f);
                seatCollider.center = new Vector3(0f, 0.46f, 0.02f);
                seatCollider.material = propPhysics;

                var backCollider = root.AddComponent<BoxCollider>();
                backCollider.size = new Vector3(0.5f, 0.52f, 0.12f);
                backCollider.center = new Vector3(0f, 0.8f, -0.22f);
                backCollider.material = propPhysics;

                var columnCollider = root.AddComponent<CapsuleCollider>();
                columnCollider.radius = 0.055f;
                columnCollider.height = 0.34f;
                columnCollider.direction = 1;
                columnCollider.center = new Vector3(0f, 0.27f, 0.02f);
                columnCollider.material = propPhysics;

                // Five caster contacts so the chair rolls and can still be tipped.
                Transform wheelBase = root.transform.Find("WheelBase");
                if (wheelBase == null)
                {
                    var wheelBaseGo = new GameObject("WheelBase");
                    wheelBase = wheelBaseGo.transform;
                    wheelBase.SetParent(root.transform, false);
                }

                wheelBase.localPosition = new Vector3(0f, 0.07f, 0.02f);
                wheelBase.localRotation = Quaternion.identity;
                wheelBase.localScale = Vector3.one;

                for (int i = wheelBase.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(wheelBase.GetChild(i).gameObject);

                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2f / 5f;
                    Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 0.23f;

                    var wheel = new GameObject("Wheel_" + i);
                    wheel.transform.SetParent(wheelBase, false);
                    wheel.transform.localPosition = offset;
                    wheel.transform.localRotation = Quaternion.identity;

                    var sphere = wheel.AddComponent<SphereCollider>();
                    sphere.radius = 0.07f;
                    sphere.center = Vector3.zero;
                    sphere.material = wheelPhysics;
                }

                var seat = root.GetComponent<ChairSeat>();
                if (seat == null)
                    seat = root.AddComponent<ChairSeat>();

                SetObject(seat, "config", rocketConfig);

                var impacts = AddOrGet<ImpactEffects>(root);
                var impactAudio = root.GetComponent<AudioSource>();
                if (impactAudio == null)
                    impactAudio = root.AddComponent<AudioSource>();

                impactAudio.playOnAwake = false;
                impactAudio.spatialBlend = 1f;
                impactAudio.minDistance = 2f;
                impactAudio.maxDistance = 25f;
                impactAudio.rolloffMode = AudioRolloffMode.Linear;

                SetObject(impacts, "fxPrefab", impactFx);
                SetObject(impacts, "audioSource", impactAudio);
                SetObject(impacts, "smallClip", audio.ImpactSmall);
                SetObject(impacts, "mediumClip", audio.ImpactMedium);
                SetObject(impacts, "bigClip", audio.ImpactBig);

                // Impacts only react while a player is riding: pushing an empty chair around
                // should not shake the camera or throw sparks.
                var gate = AddOrGet<RiderGate>(root);
                SetObject(gate, "seat", seat);
                SetObject(impacts, "gateSource", gate);

                var rocket = AddOrGet<RocketController>(root);
                SetObject(rocket, "seat", seat);
                SetObject(rocket, "body", body);
                SetObject(rocket, "impacts", impacts);
                SetFloat(rocket, "emptyMass", 10f);
                SetFloat(rocket, "riderMass", 65f);
                SetFloat(rocket, "thrustForce", 1800f);
                SetFloat(rocket, "maxSpeed", 10f);
                SetFloat(rocket, "uprightAcceleration", 50f);
                SetFloat(rocket, "thrustUprightAcceleration", 200f);
                SetFloat(rocket, "uprightDeadZone", 5f);
                SetFloat(rocket, "uprightFullTilt", 25f);
                SetFloat(rocket, "uprightDamping", 4.5f);
                SetFloat(rocket, "thrustUprightDamping", 12f);
                SetFloat(rocket, "impactRecoverySeconds", 1f);

                PrefabUtility.SaveAsPrefabAsset(root, ChairPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ player prefab

        private static void UpgradePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                RemoveMissingScripts(root);

                Transform pivot = root.transform.Find("CameraPivot");
                if (pivot == null)
                    throw new InvalidOperationException("PF_Player has no CameraPivot.");

                Transform handAnchor = pivot.Find("HandAnchor");
                if (handAnchor == null)
                {
                    var anchorGo = new GameObject("HandAnchor");
                    handAnchor = anchorGo.transform;
                    handAnchor.SetParent(pivot, false);
                }

                handAnchor.localPosition = new Vector3(0.3f, -0.26f, 0.5f);
                handAnchor.localRotation = Quaternion.identity;
                handAnchor.localScale = Vector3.one;

                var equipment = AddOrGet<PlayerEquipment>(root);
                SetObject(equipment, "handAnchor", handAnchor);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ scene

        private static void SetupScene(ExtinguisherItem extinguisherPrefab)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.GetComponent<CameraShake>() == null)
                mainCamera.gameObject.AddComponent<CameraShake>();

            if (mainCamera != null && mainCamera.GetComponent<CameraFloorGuard>() == null)
                mainCamera.gameObject.AddComponent<CameraFloorGuard>();

            GameObject hud = GameObject.Find("HUD");
            if (hud != null)
                EnsureFuelGauge(hud);

            GameObject chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChairPrefabPath);

            DestroyIfExists(DemoChairName);
            GameObject chair = (GameObject)PrefabUtility.InstantiatePrefab(chairPrefab);
            chair.name = DemoChairName;
            chair.transform.SetPositionAndRotation(DemoChairPosition, Quaternion.identity);

            DestroyIfExists("FireExtinguisher_Demo");
            CreateExtinguisherInstance(extinguisherPrefab, "FireExtinguisher_Demo",
                new Vector3(-1.9f, 0.02f, -13.45f), 22f);

            DestroyIfExists("FireExtinguisher_WestWall");
            CreateExtinguisherInstance(extinguisherPrefab, "FireExtinguisher_WestWall",
                new Vector3(-15.4f, 0.02f, -0.9f), 90f);

            DestroyIfExists("FireExtinguisher_EastWall");
            CreateExtinguisherInstance(extinguisherPrefab, "FireExtinguisher_EastWall",
                new Vector3(15.4f, 0.02f, 3.6f), -90f);
        }

        /// <summary>
        /// Builds the breakable glass partition on the east side of the room. The intact pane is a
        /// single solid object; the shards are inactive children that take over when it shatters.
        /// </summary>
        private static void BuildGlassPartition(MaterialSet materials, AudioSet audio)
        {
            BuildGlassWall(
                GlassPartitionName, GlassPartitionPosition, Quaternion.identity,
                GlassPartitionWidth, GlassPartitionHeight,
                GlassShardColumns, GlassShardRows, materials, audio);
        }

        /// <summary>
        /// Builds one breakable glass wall. Shared with the arena layout so extra barriers use the
        /// same pane, frame, shard and audio setup as the office partition. Loads the already
        /// generated materials and clip without rewriting them, so laying out extra walls never
        /// touches the shared assets.
        /// </summary>
        public static GameObject BuildGlassWall(
            string name,
            Vector3 position,
            Quaternion rotation,
            float width,
            float height,
            int columns,
            int rows)
        {
            var materials = new MaterialSet
            {
                Metal = LoadRequiredMaterial(MaterialsFolder + "/M_ExtinguisherMetal.mat"),
                Glass = LoadRequiredMaterial(MaterialsFolder + "/M_Glass.mat")
            };

            var audio = new AudioSet
            {
                GlassBreak = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/A_GlassBreak.wav")
            };

            return BuildGlassWall(name, position, rotation, width, height, columns, rows, materials, audio);
        }

        private static Material LoadRequiredMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new InvalidOperationException(
                    $"RocketChairSetup: missing material '{path}'. " +
                    "Run Tools > Rocket Chair > Rebuild All Assets first.");

            return material;
        }

        private static GameObject BuildGlassWall(
            string name,
            Vector3 position,
            Quaternion rotation,
            float width,
            float height,
            int columns,
            int rows,
            MaterialSet materials,
            AudioSet audio)
        {
            DestroyIfExists(name);

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);
            Transform rootTransform = root.transform;

            var frame = new GameObject("Frame");
            frame.transform.SetParent(rootTransform, false);

            float halfWidth = width * 0.5f;
            float postHeight = height + 0.1f;
            Vector3 postSize = new Vector3(0.09f, postHeight, 0.16f);

            Primitive(PrimitiveType.Cube, "Post_Left", frame.transform,
                new Vector3(-halfWidth - 0.045f, postHeight * 0.5f, 0f), postSize, materials.Metal, null, true);
            Primitive(PrimitiveType.Cube, "Post_Right", frame.transform,
                new Vector3(halfWidth + 0.045f, postHeight * 0.5f, 0f), postSize, materials.Metal, null, true);
            Primitive(PrimitiveType.Cube, "Rail_Top", frame.transform,
                new Vector3(0f, height + 0.04f, 0f),
                new Vector3(width + 0.18f, 0.09f, 0.16f), materials.Metal, null, true);
            Primitive(PrimitiveType.Cube, "Rail_Bottom", frame.transform,
                new Vector3(0f, 0.04f, 0f),
                new Vector3(width + 0.18f, 0.08f, 0.16f), materials.Metal, null, true);

            // The intact pane is the only collider the chair can hit before the break.
            GameObject pane = Primitive(PrimitiveType.Cube, "Pane", rootTransform,
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, 0.06f),
                materials.Glass, null, true);
            DisableShadowCasting(pane);

            var shardsRoot = new GameObject("Shards");
            shardsRoot.transform.SetParent(rootTransform, false);

            float cellWidth = width / columns;
            float cellHeight = height / rows;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    GameObject shard = Primitive(PrimitiveType.Cube,
                        $"Shard_{row}_{column}", shardsRoot.transform,
                        new Vector3(-halfWidth + cellWidth * (column + 0.5f), cellHeight * (row + 0.5f), 0f),
                        new Vector3(cellWidth * 0.98f, cellHeight * 0.98f, 0.06f),
                        materials.Glass, null, true);
                    DisableShadowCasting(shard);

                    var shardBody = shard.AddComponent<Rigidbody>();
                    shardBody.mass = 0.35f;
                    shardBody.interpolation = RigidbodyInterpolation.None;
                    shardBody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                    shard.SetActive(false);
                }
            }

            shardsRoot.SetActive(false);

            AudioSource audioSource = AddAudioSource(root, "BreakAudio", false, 0.9f);

            // The collision callback only reaches components on the collider's own GameObject,
            // so the glass behaviour lives on the pane, not on the partition root.
            var glass = pane.AddComponent<BreakableGlassPane>();
            SetObject(glass, "paneCollider", pane.GetComponent<Collider>());
            SetObject(glass, "paneRenderer", pane.GetComponent<Renderer>());
            SetObject(glass, "shardsRoot", shardsRoot);
            SetObject(glass, "audioSource", audioSource);
            SetObject(glass, "breakClip", audio.GlassBreak);
            SetFloat(glass, "minimumSpeed", 4f);
            SetFloat(glass, "breakScore", 15f);
            SetFloat(glass, "shardLifetime", 6f);
            SetFloat(glass, "shardSpeed", 2.4f);
            SetFloat(glass, "shardSpin", 9f);
            SetFloat(glass, "cameraTrauma", 0.45f);
            SetFloat(glass, "shakeRange", 20f);

            return root;
        }

        private static void DisableShadowCasting(GameObject target)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void CreateExtinguisherInstance(
            ExtinguisherItem prefab,
            string name,
            Vector3 position,
            float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        private static void DestroyIfExists(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
        }

        private static void EnsureFuelGauge(GameObject hud)
        {
            Transform existing = hud.transform.Find("FuelGauge");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            Text sampleText = UnityEngine.Object.FindFirstObjectByType<Text>();
            Font font = sampleText != null
                ? sampleText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var gauge = new GameObject("FuelGauge", typeof(RectTransform));
            gauge.transform.SetParent(hud.transform, false);

            var gaugeRect = gauge.GetComponent<RectTransform>();
            gaugeRect.anchorMin = new Vector2(0f, 0f);
            gaugeRect.anchorMax = new Vector2(0f, 0f);
            gaugeRect.pivot = new Vector2(0f, 0f);
            gaugeRect.anchoredPosition = new Vector2(40f, 40f);
            gaugeRect.sizeDelta = new Vector2(360f, 56f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(gauge.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(360f, 26f);

            Text label = labelGo.GetComponent<Text>();
            label.font = font;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 1f, 1f, 0.92f);
            label.alignment = TextAnchor.MiddleLeft;
            label.text = "FUEL 100%";
            label.raycastTarget = false;

            var backgroundGo = new GameObject("BarBackground",
                typeof(RectTransform), typeof(Image));
            backgroundGo.transform.SetParent(gauge.transform, false);
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundRect.anchorMax = new Vector2(0f, 0f);
            backgroundRect.pivot = new Vector2(0f, 0f);
            backgroundRect.anchoredPosition = new Vector2(0f, 2f);
            backgroundRect.sizeDelta = new Vector2(360f, 18f);

            Image background = backgroundGo.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;

            var fillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(backgroundGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            Image fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.35f, 0.9f, 0.4f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var gaugeUI = hud.GetComponent<FuelGaugeUI>();
            if (gaugeUI == null)
                gaugeUI = hud.AddComponent<FuelGaugeUI>();

            SetObject(gaugeUI, "equipment", UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>());
            SetObject(gaugeUI, "gaugeRoot", gauge);
            SetObject(gaugeUI, "fillImage", fill);
            SetObject(gaugeUI, "label", label);

            gauge.SetActive(false);
        }

        // ------------------------------------------------------------------ serialized field helpers

        /// <summary>
        /// Drops components whose script asset no longer exists. Setup deletes obsolete runtime
        /// scripts, and the next rebuild must not leave "Missing (Mono Script)" behind on prefabs.
        /// </summary>
        private static void RemoveMissingScripts(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
        }

        private static T AddOrGet<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
                component = root.AddComponent<T>();

            return component;
        }

        private static SerializedProperty FindProperty(Component target, string fieldName)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return null;
            }

            return property;
        }

        private static void SetObject(Component target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Component target, string fieldName, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Component target, string fieldName, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBoolean(Component target, string fieldName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(Component target, string fieldName, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Component target, string fieldName, Vector3 value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"RocketChairSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

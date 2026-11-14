using System;
using System.Collections.Generic;
using FireExtinguisher.Interaction;
using FireExtinguisher.Player;
using FireExtinguisher.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time setup for the chair interaction system: chair prefab seat anchors, the player prefab,
/// the scene player instance, the reused first-person camera and the interaction HUD.
/// Run with: unity command script run --file AgentScripts/BuildChairInteraction.cs --entry BuildChairInteraction.Build
/// </summary>
public static class BuildChairInteraction
{
    const string ChairPrefabPath = "Assets/Prefabs/RocketOffice/PF_OfficeChair.prefab";
    const string ConfigDir = "Assets/Settings/Chairs";
    const string ConfigPath = ConfigDir + "/ChairSittingConfig_Office.asset";
    const string PlayerPrefabDir = "Assets/Prefabs/Player";
    const string PlayerPrefabPath = PlayerPrefabDir + "/PF_Player.prefab";
    const string ScenePath = "Assets/Scenes/RocketOffice.unity";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    const string PropPhysicsPath = "Assets/Physics/RocketOffice/PM_DefaultProp.physicMaterial";

    public static string Build()
    {
        var report = new List<string>();

        EnsureFolders();

        ChairSittingConfig config = EnsureConfig();
        report.Add($"config={ConfigPath}");

        UpdateChairPrefab(config);
        report.Add($"chairPrefab={ChairPrefabPath}");

        GameObject playerPrefab = BuildPlayerPrefab(config);
        report.Add($"playerPrefab={PlayerPrefabPath}");

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (scene.isDirty)
                throw new Exception($"Open scene '{scene.name}' has unsaved changes. Save or discard them, then rerun.");

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject player = PlacePlayer(playerPrefab, scene);
        Camera viewCamera = ReparentCamera(player);
        GameObject hud = BuildHud(player);
        WirePlayerCamera(player, viewCamera);
        WireHud(hud, player);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        report.Add($"player={player.name} position={player.transform.position}");
        report.Add($"camera={viewCamera.name} parent={viewCamera.transform.parent.name}");
        report.Add($"hud={hud.name}");
        return string.Join("\n", report);
    }

    // ------------------------------------------------------------------ assets

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets", "Settings");
        EnsureFolder("Assets/Settings", "Chairs");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Player");
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static ChairSittingConfig EnsureConfig()
    {
        ChairSittingConfig config = AssetDatabase.LoadAssetAtPath<ChairSittingConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ChairSittingConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        var serialized = new SerializedObject(config);
        serialized.FindProperty("sitPrompt").stringValue = "Sit";
        serialized.FindProperty("standPrompt").stringValue = "Stand";
        serialized.FindProperty("seatedEyeHeight").floatValue = 0.68f;
        serialized.FindProperty("seatedYawLimit").floatValue = 80f;
        serialized.FindProperty("standUpCooldown").floatValue = 0.25f;
        serialized.FindProperty("standProbeDistance").floatValue = 0.8f;
        serialized.FindProperty("standProbeLift").floatValue = 0.05f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(config);
        return config;
    }

    // ------------------------------------------------------------------ chairs

    private static void UpdateChairPrefab(ChairSittingConfig config)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ChairPrefabPath))
            throw new Exception($"Missing chair prefab at {ChairPrefabPath}");

        GameObject root = PrefabUtility.LoadPrefabContents(ChairPrefabPath);
        try
        {
            Transform sitPoint = root.transform.Find("SitPoint");
            if (sitPoint == null)
            {
                var sitPointGo = new GameObject("SitPoint");
                sitPointGo.transform.SetParent(root.transform, false);
                sitPoint = sitPointGo.transform;
            }

            sitPoint.localPosition = new Vector3(0f, 0.58f, 0.05f);
            sitPoint.localRotation = Quaternion.identity;
            sitPoint.localScale = Vector3.one;

            ChairSeat seat = root.GetComponent<ChairSeat>();
            if (seat == null)
                seat = root.AddComponent<ChairSeat>();

            SetObject(seat, "sitPoint", sitPoint);
            SetObject(seat, "config", config);

            PrefabUtility.SaveAsPrefabAsset(root, ChairPrefabPath, out bool saved);
            if (!saved)
                throw new Exception($"Failed to save {ChairPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ player

    private static GameObject BuildPlayerPrefab(ChairSittingConfig config)
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions == null)
            throw new Exception($"Missing input actions at {InputActionsPath}");

        var root = new GameObject("PF_Player");
        try
        {
            root.tag = "Player";

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 70f;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PropPhysicsPath);

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            pivot.transform.localRotation = Quaternion.identity;

            PlayerInputReader reader = root.AddComponent<PlayerInputReader>();
            PlayerMotor motor = root.AddComponent<PlayerMotor>();
            PlayerLook look = root.AddComponent<PlayerLook>();
            SeatController seating = root.AddComponent<SeatController>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();

            SetObject(reader, "actions", actions);
            SetObject(motor, "input", reader);
            SetObject(motor, "body", body);
            SetObject(motor, "capsule", capsule);
            SetObject(look, "input", reader);
            SetObject(look, "cameraPivot", pivot.transform);
            SetObject(look, "body", body);
            SetObject(seating, "body", body);
            SetObject(seating, "bodyCollider", capsule);
            SetObject(seating, "motor", motor);
            SetObject(seating, "look", look);
            SetObject(seating, "interactor", interactor);
            SetObject(seating, "defaultConfig", config);
            SetObject(interactor, "input", reader);
            SetObject(interactor, "seating", seating);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool saved);
            if (!saved)
                throw new Exception($"Failed to save {PlayerPrefabPath}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
    }

    private static GameObject PlacePlayer(GameObject playerPrefab, Scene scene)
    {
        GameObject existing = GameObject.Find("Player");
        if (existing != null && existing.scene.IsValid())
            UnityEngine.Object.DestroyImmediate(existing);

        GameObject spawn = GameObject.Find("PlayerSpawn");
        Vector3 position = spawn != null ? spawn.transform.position : new Vector3(0f, 0f, -8.4f);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        instance.name = "Player";
        instance.transform.SetPositionAndRotation(position, Quaternion.identity);

        GameObject systems = GameObject.Find("Systems");
        if (systems != null)
            instance.transform.SetParent(systems.transform, true);

        return instance;
    }

    private static Camera ReparentCamera(GameObject player)
    {
        Camera viewCamera = Camera.main;
        if (viewCamera == null)
            viewCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();

        if (viewCamera == null)
            throw new Exception("No camera in the scene to use as the player's view.");

        Transform pivot = player.transform.Find("CameraPivot");
        if (pivot == null)
            throw new Exception("Player prefab is missing its CameraPivot.");

        viewCamera.transform.SetParent(pivot, false);
        viewCamera.transform.localPosition = Vector3.zero;
        viewCamera.transform.localRotation = Quaternion.identity;

        return viewCamera;
    }

    private static void WirePlayerCamera(GameObject player, Camera viewCamera)
    {
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null)
            SetObject(interactor, "viewCamera", viewCamera);
    }

    // --------------------------------------------------------------------- hud

    private static GameObject BuildHud(GameObject player)
    {
        GameObject existing = GameObject.Find("HUD");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var promptGo = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        promptGo.transform.SetParent(canvasGo.transform, false);

        var promptRect = promptGo.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 64f);
        promptRect.sizeDelta = new Vector2(560f, 56f);

        var background = promptGo.GetComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);
        background.raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(promptGo.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 4f);
        labelRect.offsetMax = new Vector2(-16f, -4f);

        var label = labelGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "Sit (E / Gamepad Y)";
        label.raycastTarget = false;

        var outline = labelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var promptUI = canvasGo.AddComponent<InteractionPromptUI>();
        SetObject(promptUI, "interactor", player.GetComponent<PlayerInteractor>());
        SetObject(promptUI, "seating", player.GetComponent<SeatController>());
        SetObject(promptUI, "promptRoot", promptGo);
        SetObject(promptUI, "promptLabel", label);

        promptGo.SetActive(false);
        return canvasGo;
    }

    private static void WireHud(GameObject hud, GameObject player)
    {
        var promptUI = hud.GetComponent<InteractionPromptUI>();
        if (promptUI == null)
            return;

        SetObject(promptUI, "interactor", player.GetComponent<PlayerInteractor>());
        SetObject(promptUI, "seating", player.GetComponent<SeatController>());
    }

    // ----------------------------------------------------------------- helpers

    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new Exception($"Field '{field}' was not found on {target.GetType().Name}.");

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

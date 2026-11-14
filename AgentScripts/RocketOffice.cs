using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the RocketOffice greybox scene and its reusable physics prop prefabs.
/// Run with: unity command script run --file AgentScripts/RocketOffice.cs --entry RocketOfficeBuilder.Build
/// </summary>
public static class RocketOfficeBuilder
{
    const string ModelDir = "Assets/Lowpoly office assets";
    const string MaterialDir = "Assets/Materials/RocketOffice";
    const string PrefabDir = "Assets/Prefabs/RocketOffice";
    const string PhysicsDir = "Assets/Physics/RocketOffice";
    const string ScenePath = "Assets/Scenes/RocketOffice.unity";

    // Room
    const float Room = 20f;
    const float WallHeight = 3.6f;
    const float WallThickness = 0.2f;
    const float DoorWidth = 2.4f;

    static Material _floor, _wall, _accent, _platform, _ramp, _cardboard, _cardboardLight, _dark, _wood, _rug;
    static PhysicsMaterial _propPhysics;
    static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
    static readonly List<string> Report = new List<string>();

    // ------------------------------------------------------------------ entry

    public static string Build()
    {
        Report.Clear();
        EnsureFolders();
        CreateMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "RocketOffice";

        BuildPrefabs();

        var root = new GameObject("RocketOffice").transform;
        var env = Group("Environment", root);
        var furniture = Group("Furniture", root);
        var props = Group("Props", root);
        var lighting = Group("Lighting", root);
        var systems = Group("Systems", root);

        BuildEnvironment(env);
        BuildFurniture(furniture);
        BuildProps(props);
        BuildLighting(lighting);
        BuildSystems(systems);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();

        AssetDatabase.SaveAssets();
        Report.Add($"scene={ScenePath}");
        Report.Add($"roots={scene.GetRootGameObjects().Length} totalObjects={Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}");
        return string.Join("\n", Report);
    }

    // -------------------------------------------------------------- materials

    static void EnsureFolders()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "RocketOffice");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "RocketOffice");
        EnsureFolder("Assets", "Physics");
        EnsureFolder("Assets/Physics", "RocketOffice");
        EnsureFolder("Assets", "Scenes");
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }

    static void CreateMaterials()
    {
        _floor = GetOrCreateMaterial("M_Floor", new Color(0.52f, 0.50f, 0.47f), 0f, 0.12f);
        _wall = GetOrCreateMaterial("M_Wall", new Color(0.78f, 0.77f, 0.74f), 0f, 0.08f);
        _accent = GetOrCreateMaterial("M_Wall_Accent", new Color(0.26f, 0.47f, 0.46f), 0f, 0.15f);
        _platform = GetOrCreateMaterial("M_Platform", new Color(0.52f, 0.50f, 0.46f), 0f, 0.12f);
        _ramp = GetOrCreateMaterial("M_Ramp", new Color(0.82f, 0.50f, 0.24f), 0f, 0.20f);
        _cardboard = GetOrCreateMaterial("M_Cardboard", new Color(0.55f, 0.38f, 0.22f), 0f, 0.10f);
        _cardboardLight = GetOrCreateMaterial("M_Cardboard_Light", new Color(0.68f, 0.51f, 0.32f), 0f, 0.10f);
        _dark = GetOrCreateMaterial("M_Trim_Dark", new Color(0.14f, 0.15f, 0.17f), 0f, 0.25f);
        _wood = GetOrCreateMaterial("M_Wood", new Color(0.40f, 0.29f, 0.20f), 0f, 0.18f);
        _rug = GetOrCreateMaterial("M_Rug", new Color(0.24f, 0.38f, 0.50f), 0f, 0.05f);

        var matPath = $"{PhysicsDir}/PM_DefaultProp.physicMaterial";
        _propPhysics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(matPath);
        if (_propPhysics == null)
        {
            _propPhysics = new PhysicsMaterial("PM_DefaultProp")
            {
                dynamicFriction = 0.55f,
                staticFriction = 0.65f,
                bounciness = 0.03f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(_propPhysics, matPath);
        }
    }

    static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness)
    {
        var path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- prefabs

    static void BuildPrefabs()
    {
        CreateModelProp("PF_OfficeChair", "office_chair", 9f, root =>
        {
            AddBox(root, new Vector3(0f, 0.30f, 0f), new Vector3(0.66f, 0.60f, 0.68f));
            AddBox(root, new Vector3(0f, 0.85f, -0.22f), new Vector3(0.50f, 0.50f, 0.18f));
        });

        CreateModelProp("PF_TrashBin", "trash_bin", 3f, root =>
        {
            AddBox(root, new Vector3(0f, 0.26f, 0f), new Vector3(0.33f, 0.52f, 0.33f));
        });

        CreateModelProp("PF_Monitor", "monitor", 4f, root =>
        {
            AddBox(root, new Vector3(0f, 0.255f, -0.03f), new Vector3(0.59f, 0.50f, 0.17f));
        });

        CreateModelProp("PF_WaterCooler", "water_cooler", 16f, root =>
        {
            AddBox(root, new Vector3(0f, 0.675f, 0.015f), new Vector3(0.31f, 1.33f, 0.34f));
            var rb = root.GetComponent<Rigidbody>();
            rb.centerOfMass = new Vector3(0f, 0.45f, 0f);
        });

        CreateModelProp("PF_OfficePlant", "office_plant", 5f, root =>
        {
            AddBox(root, new Vector3(-0.02f, 0.316f, -0.005f), new Vector3(0.28f, 0.62f, 0.28f));
        });

        CreateModelProp("PF_CoffeeMug", "coffee_mug", 0.35f, root =>
        {
            AddBox(root, new Vector3(-0.021f, 0.045f, 0f), new Vector3(0.115f, 0.088f, 0.075f));
        });

        CreateModelProp("PF_Laptop", "laptop", 1.6f, root =>
        {
            AddBox(root, new Vector3(0f, 0.117f, -0.014f), new Vector3(0.33f, 0.22f, 0.26f));
        });

        CreateModelProp("PF_Keyboard", "keyboard", 0.7f, root =>
        {
            AddBox(root, new Vector3(0f, 0.016f, 0f), new Vector3(0.44f, 0.03f, 0.14f));
        });

        CreateBoxProp("PF_CardboardBox_Small", new Vector3(0.42f, 0.42f, 0.42f), 1f, _cardboardLight);
        CreateBoxProp("PF_CardboardBox_Medium", new Vector3(0.62f, 0.62f, 0.62f), 2.5f, _cardboard);
        CreateBoxProp("PF_CardboardBox_Large", new Vector3(0.85f, 0.55f, 0.70f), 4.5f, _cardboard);

        Report.Add($"prefabs={AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }).Length}");
    }

    static void CreateModelProp(string prefabName, string modelName, float mass, Action<GameObject> buildColliders)
    {
        var root = new GameObject(prefabName);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(Model(modelName), root.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        var rb = root.AddComponent<Rigidbody>();
        ConfigureBody(rb, mass);

        buildColliders(root);
        SavePrefab(root, prefabName);
    }

    static void CreateBoxProp(string prefabName, Vector3 size, float mass, Material material)
    {
        var root = new GameObject(prefabName);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Visual";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;

        var rb = root.AddComponent<Rigidbody>();
        ConfigureBody(rb, mass);

        var box = root.AddComponent<BoxCollider>();
        box.size = size;
        box.material = _propPhysics;

        SavePrefab(root, prefabName);
    }

    static void ConfigureBody(Rigidbody rb, float mass)
    {
        rb.mass = mass;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.15f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxAngularVelocity = 12f;
    }

    static BoxCollider AddBox(GameObject root, Vector3 center, Vector3 size)
    {
        var box = root.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        box.material = _propPhysics;
        return box;
    }

    static void SavePrefab(GameObject root, string prefabName)
    {
        var path = $"{PrefabDir}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
        if (!success) throw new Exception($"Failed to save prefab {path}");
        Object.DestroyImmediate(root);
    }

    // -------------------------------------------------------------- environment

    static void BuildEnvironment(Transform parent)
    {
        var floor = Primitive("Floor", PrimitiveType.Cube, parent, new Vector3(0f, -0.1f, 0f), new Vector3(Room, 0.2f, Room), _floor);
        MarkStatic(floor);

        var walls = Group("Walls", parent);
        float half = Room * 0.5f;
        float halfDoor = DoorWidth * 0.5f;
        float sideWidth = half - halfDoor;

        Wall(walls, "Wall_North", new Vector3(0f, WallHeight * 0.5f, half - WallThickness * 0.5f), new Vector3(Room, WallHeight, WallThickness), _accent);
        Wall(walls, "Wall_South_Left", new Vector3(-(halfDoor + sideWidth * 0.5f), WallHeight * 0.5f, -half + WallThickness * 0.5f), new Vector3(sideWidth, WallHeight, WallThickness), _wall);
        Wall(walls, "Wall_South_Right", new Vector3(halfDoor + sideWidth * 0.5f, WallHeight * 0.5f, -half + WallThickness * 0.5f), new Vector3(sideWidth, WallHeight, WallThickness), _wall);
        Wall(walls, "Wall_South_Header", new Vector3(0f, 3.1f, -half + WallThickness * 0.5f), new Vector3(DoorWidth, 1.0f, WallThickness), _wall);
        Wall(walls, "Wall_East", new Vector3(half - WallThickness * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, Room), _wall);
        Wall(walls, "Wall_West", new Vector3(-half + WallThickness * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, Room), _wall);

        // Door frame trim
        var trim = Group("DoorFrame", walls);
        Primitive("Frame_Left", PrimitiveType.Cube, trim, new Vector3(-halfDoor - 0.06f, 1.3f, -half + WallThickness + 0.01f), new Vector3(0.12f, 2.6f, 0.06f), _dark);
        Primitive("Frame_Right", PrimitiveType.Cube, trim, new Vector3(halfDoor + 0.06f, 1.3f, -half + WallThickness + 0.01f), new Vector3(0.12f, 2.6f, 0.06f), _dark);
        Primitive("Frame_Top", PrimitiveType.Cube, trim, new Vector3(0f, 2.66f, -half + WallThickness + 0.01f), new Vector3(DoorWidth + 0.24f, 0.12f, 0.06f), _dark);
        Primitive("Doormat", PrimitiveType.Cube, trim, new Vector3(0f, 0.012f, -half + 1.1f), new Vector3(1.8f, 0.024f, 1.2f), _dark).GetComponent<BoxCollider>().enabled = false;

        // Skirting boards
        var skirt = Group("Skirting", walls);
        Primitive("Skirt_North", PrimitiveType.Cube, skirt, new Vector3(0f, 0.06f, half - WallThickness - 0.02f), new Vector3(Room - 0.1f, 0.12f, 0.04f), _dark);
        Primitive("Skirt_East", PrimitiveType.Cube, skirt, new Vector3(half - WallThickness - 0.02f, 0.06f, 0f), new Vector3(0.04f, 0.12f, Room - 0.1f), _dark);
        Primitive("Skirt_West", PrimitiveType.Cube, skirt, new Vector3(-half + WallThickness + 0.02f, 0.06f, 0f), new Vector3(0.04f, 0.12f, Room - 0.1f), _dark);
        Primitive("Skirt_South_Left", PrimitiveType.Cube, skirt, new Vector3(-(halfDoor + sideWidth * 0.5f), 0.06f, -half + WallThickness + 0.02f), new Vector3(sideWidth - 0.06f, 0.12f, 0.04f), _dark);
        Primitive("Skirt_South_Right", PrimitiveType.Cube, skirt, new Vector3(halfDoor + sideWidth * 0.5f, 0.06f, -half + WallThickness + 0.02f), new Vector3(sideWidth - 0.06f, 0.12f, 0.04f), _dark);

        // Raised platform + ramp, north-west corner
        var stage = Group("RaisedPlatform", parent);
        Primitive("Platform", PrimitiveType.Cube, stage, new Vector3(-7.7f, 0.225f, 6.3f), new Vector3(4.2f, 0.45f, 4.2f), _platform);
        Primitive("Platform_Edge", PrimitiveType.Cube, stage, new Vector3(-7.7f, 0.46f, 4.24f), new Vector3(4.24f, 0.04f, 0.08f), _dark);
        float rampAngle = Mathf.Atan2(0.45f, 2.4f) * Mathf.Rad2Deg;
        var ramp = Primitive("Ramp", PrimitiveType.Cube, stage, new Vector3(-7.7f, 0.166f, 3.0f), new Vector3(2.4f, 0.12f, 2.442f), _ramp);
        ramp.transform.localRotation = Quaternion.Euler(-rampAngle, 0f, 0f);

        // Ceiling stays open so the greybox reads as a dollhouse cutaway.
    }

    static void Wall(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var wall = Primitive(name, PrimitiveType.Cube, parent, position, size, material);
        MarkStatic(wall);
    }

    // --------------------------------------------------------------- furniture

    static void BuildFurniture(Transform parent)
    {
        var desks = Group("DeskClusters", parent);

        BuildDeskStation(Group("DeskStation_West_A", desks), new Vector3(-9.3f, 0f, -5.0f), -90f, false);
        BuildDeskStation(Group("DeskStation_West_B", desks), new Vector3(-9.3f, 0f, -3.2f), -90f, true);
        BuildDeskStation(Group("DeskStation_East_A", desks), new Vector3(9.3f, 0f, -5.0f), 90f, false);
        BuildDeskStation(Group("DeskStation_East_B", desks), new Vector3(9.3f, 0f, -3.2f), 90f, false);

        var storage = Group("Storage", parent);
        StaticModel(storage, "bookshelf", "Bookshelf", new Vector3(-4.6f, 0f, 9.6f), 180f, fitCollider: true);
        StaticModel(storage, "filing_cabinet", "FilingCabinet_North", new Vector3(6.0f, 0f, 9.45f), 180f, fitCollider: true);
        StaticModel(storage, "filing_cabinet", "FilingCabinet_East_A", new Vector3(9.47f, 0f, 2.6f), -90f, fitCollider: true);
        StaticModel(storage, "filing_cabinet", "FilingCabinet_East_B", new Vector3(9.47f, 0f, -4.2f), -90f, fitCollider: true);
        StaticModel(storage, "server_rack", "ServerRack", new Vector3(9.38f, 0f, 6.4f), -90f, fitCollider: true);
        StaticModel(storage, "photocopier", "Photocopier", new Vector3(9.47f, 0f, -1.0f), -90f, fitCollider: true);

        var printers = Group("Printers", parent);
        StaticModel(printers, "printer", "Printer_North", new Vector3(6.0f, 1.3f, 9.45f), 180f, fitCollider: true);
        StaticModel(printers, "printer", "Printer_East", new Vector3(9.47f, 1.3f, -4.2f), -90f, fitCollider: true);

        var decor = Group("WallDecor", parent);
        StaticModel(decor, "whiteboard", "Whiteboard", new Vector3(-2.4f, 0f, 9.55f), 180f, fitCollider: true);
        StaticModel(decor, "wall_clock", "WallClock", new Vector3(0.6f, 2.5f, 9.77f), 180f, fitCollider: false);
        StaticModel(storage, "wifi_router", "WifiRouter", new Vector3(-4.6f, 1.8f, 9.6f), 180f, fitCollider: false);

        var meeting = Group("MeetingArea", parent);
        StaticModel(meeting, "projector", "Projector", new Vector3(-7.7f, 0.45f, 7.3f), 180f, fitCollider: true);
        StaticModel(meeting, "office_plant", "Plant_Stage", new Vector3(-9.2f, 0.45f, 7.9f), 0f, fitCollider: true);
        BuildMeetingTable(meeting);
        BuildReception(parent);

        // Small details on the north filing cabinet
        StaticModel(storage, "paper_stack", "PaperStack_Cabinet", new Vector3(6.35f, 1.3f, 9.5f), 20f, fitCollider: false);
        StaticModel(storage, "pen_cup", "PenCup_Cabinet", new Vector3(5.75f, 1.3f, 9.55f), 0f, fitCollider: false);
    }

    static void BuildMeetingTable(Transform parent)
    {
        var table = Group("MeetingTable", parent);
        Primitive("Rug", PrimitiveType.Cube, table, new Vector3(0.6f, 0.011f, 1.3f), new Vector3(5.2f, 0.02f, 3.6f), _rug).GetComponent<BoxCollider>().enabled = false;
        Primitive("TableTop", PrimitiveType.Cube, table, new Vector3(0.6f, 0.72f, 1.3f), new Vector3(2.8f, 0.09f, 1.4f), _wood);
        Primitive("TableLeg_A", PrimitiveType.Cube, table, new Vector3(-0.5f, 0.36f, 1.3f), new Vector3(0.14f, 0.72f, 1.15f), _dark);
        Primitive("TableLeg_B", PrimitiveType.Cube, table, new Vector3(1.7f, 0.36f, 1.3f), new Vector3(0.14f, 0.72f, 1.15f), _dark);

        Prop("PF_Laptop", table, "Laptop", new Vector3(0.1f, 0.765f, 1.35f), 15f);
        Prop("PF_CoffeeMug", table, "Mug", new Vector3(1.2f, 0.765f, 1.0f), 0f);
        StaticModel(table, "paper_stack", "PaperStack", new Vector3(1.3f, 0.765f, 1.6f), 20f, fitCollider: false);
        StaticModel(table, "pen_cup", "PenCup", new Vector3(-0.25f, 0.765f, 1.05f), 0f, fitCollider: false);

        Prop("PF_OfficeChair", table, "Chair_North_A", new Vector3(0.0f, 0f, 2.30f), 180f);
        Prop("PF_OfficeChair", table, "Chair_North_B", new Vector3(1.2f, 0f, 2.30f), 180f);
        Prop("PF_OfficeChair", table, "Chair_South_A", new Vector3(0.0f, 0f, 0.30f), 0f);
        Prop("PF_OfficeChair", table, "Chair_South_B", new Vector3(1.2f, 0f, 0.30f), 0f);
    }

    static void BuildReception(Transform parent)
    {
        var reception = Group("Reception", parent);
        Primitive("Counter", PrimitiveType.Cube, reception, new Vector3(-4.8f, 0.525f, -7.6f), new Vector3(2.6f, 1.05f, 0.75f), _wood);
        Primitive("CounterTop", PrimitiveType.Cube, reception, new Vector3(-4.8f, 1.08f, -7.6f), new Vector3(2.8f, 0.06f, 0.95f), _dark);

        Prop("PF_Monitor", reception, "Monitor", new Vector3(-4.8f, 1.11f, -7.72f), 180f);
        Prop("PF_CoffeeMug", reception, "Mug", new Vector3(-5.6f, 1.11f, -7.45f), 0f);
        StaticModel(reception, "pen_cup", "PenCup", new Vector3(-4.0f, 1.11f, -7.45f), 0f, fitCollider: false);
        StaticModel(reception, "paper_stack", "PaperStack", new Vector3(-5.6f, 1.11f, -7.75f), 10f, fitCollider: false);
        Prop("PF_OfficeChair", reception, "ReceptionChair", new Vector3(-4.8f, 0f, -8.55f), 0f);
    }

    static void BuildDeskStation(Transform station, Vector3 position, float yaw, bool withLaptop)
    {
        station.localPosition = position;
        station.localRotation = Quaternion.Euler(0f, yaw, 0f);

        StaticModel(station, "office_desk", "Desk", Vector3.zero, 0f, fitCollider: true);

        // Desk surface is 0.75 m high.
        var monitor = Prop("PF_Monitor", station, "Monitor", new Vector3(0f, 0.75f, 0.14f), 180f);
        Prop("PF_Keyboard", station, "Keyboard", new Vector3(0f, 0.75f, -0.02f), 0f);
        Prop("PF_CoffeeMug", station, "Mug", new Vector3(0.52f, 0.75f, 0.16f), 35f);

        StaticModel(station, "computer_mouse", "Mouse", new Vector3(0.32f, 0.75f, -0.02f), -15f, fitCollider: false);
        StaticModel(station, "desk_lamp", "DeskLamp", new Vector3(-0.62f, 0.75f, 0.18f), 180f, fitCollider: false);
        StaticModel(station, "pen_cup", "PenCup", new Vector3(-0.36f, 0.75f, -0.04f), 0f, fitCollider: false);
        StaticModel(station, "paper_stack", "PaperStack", new Vector3(0.56f, 0.75f, -0.18f), 12f, fitCollider: false);

        if (withLaptop)
            Prop("PF_Laptop", station, "Laptop", new Vector3(-0.42f, 0.75f, 0.1f), 25f);

        // Chair sits in front of the desk, facing it.
        Prop("PF_OfficeChair", station, "OfficeChair", new Vector3(0f, 0f, -0.88f), 0f);
    }

    // ------------------------------------------------------------------- props

    static void BuildProps(Transform parent)
    {
        var bins = Group("TrashBins", parent);
        Prop("PF_TrashBin", bins, "TrashBin_West", new Vector3(-8.2f, 0f, -1.6f), 15f);
        Prop("PF_TrashBin", bins, "TrashBin_East", new Vector3(8.3f, 0f, 0.2f), -20f);
        Prop("PF_TrashBin", bins, "TrashBin_Door", new Vector3(2.2f, 0f, -8.6f), 40f);

        var boxes = Group("CardboardBoxes", parent);
        Prop("PF_CardboardBox_Medium", boxes, "Box_Floor_A", new Vector3(7.1f, 0.31f, -8.4f), 8f);
        Prop("PF_CardboardBox_Small", boxes, "Box_Floor_B", new Vector3(6.35f, 0.21f, -8.95f), 27f);
        Prop("PF_CardboardBox_Medium", boxes, "Box_Floor_C", new Vector3(8.0f, 0.31f, -8.9f), -14f);
        Prop("PF_CardboardBox_Large", boxes, "Box_Stack_Top", new Vector3(7.1f, 0.93f, -8.4f), -8f);
        Prop("PF_CardboardBox_Small", boxes, "Box_Stack_Small", new Vector3(7.05f, 1.42f, -8.35f), 41f);
        Prop("PF_CardboardBox_Small", boxes, "Box_NearDoor", new Vector3(5.6f, 0.21f, -8.7f), -22f);

        var coolers = Group("WaterCoolers", parent);
        Prop("PF_WaterCooler", coolers, "WaterCooler", new Vector3(3.6f, 0f, 9.6f), 180f);

        var plants = Group("Plants", parent);
        Prop("PF_OfficePlant", plants, "Plant_SouthWest", new Vector3(-9.0f, 0f, -8.9f), 20f);
        Prop("PF_OfficePlant", plants, "Plant_NorthEast", new Vector3(9.0f, 0f, 9.2f), -35f);
        Prop("PF_OfficePlant", plants, "Plant_Door", new Vector3(3.4f, 0f, -8.9f), 0f);

        var loose = Group("LooseItems", parent);
        Prop("PF_Monitor", loose, "Monitor_Spare", new Vector3(-6.6f, 0f, -8.7f), 125f);
        Prop("PF_Laptop", loose, "Laptop_Lounge", new Vector3(3.2f, 0f, -6.4f), 200f);
        Prop("PF_CoffeeMug", loose, "Mug_Floor", new Vector3(0.9f, 0f, -4.2f), 0f);
    }

    // ---------------------------------------------------------------- lighting

    static void BuildLighting(Transform parent)
    {
        var sunGo = new GameObject("Directional Light");
        sunGo.transform.SetParent(parent, false);
        sunGo.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.90f);
        sun.intensity = 0.9f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;
        RenderSettings.sun = sun;

        var fixtures = Group("CeilingLights", parent);
        var positions = new[]
        {
            new Vector3(-5f, 0f, -4.5f), new Vector3(5f, 0f, -4.5f),
            new Vector3(-5f, 0f, 4.5f), new Vector3(5f, 0f, 4.5f)
        };

        for (var i = 0; i < positions.Length; i++)
        {
            var p = positions[i];

            var lightGo = new GameObject($"CeilingLight_{i + 1}");
            lightGo.transform.SetParent(fixtures, false);
            lightGo.transform.localPosition = new Vector3(p.x, 3.12f, p.z);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.94f, 0.86f);
            light.intensity = 14f;
            light.range = 8f;
            light.shadows = LightShadows.None;
        }

        var volumeGo = new GameObject("Global Volume");
        volumeGo.transform.SetParent(parent, false);
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
    }

    // ----------------------------------------------------------------- systems

    static void BuildSystems(Transform parent)
    {
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(parent, false);
        spawn.transform.localPosition = new Vector3(0f, 0f, -8.4f);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(parent, false);
        var camPos = new Vector3(0f, 22.5f, -14f);
        var target = new Vector3(0f, 0.5f, 1.5f);
        camGo.transform.localPosition = camPos;
        camGo.transform.localRotation = Quaternion.LookRotation(target - camPos);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
    }

    // ----------------------------------------------------------------- helpers

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        MarkStatic(go);
        return go;
    }

    static GameObject StaticModel(Transform parent, string modelName, string name, Vector3 localPosition, float yaw, bool fitCollider)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Model(modelName), parent);
        go.name = name;
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one;
        if (fitCollider) FitBoxCollider(go);
        MarkStatic(go);
        return go;
    }

    static GameObject Prop(string prefabName, Transform parent, string name, Vector3 localPosition, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
        if (prefab == null) throw new Exception($"Missing prefab {prefabName}");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    static BoxCollider FitBoxCollider(GameObject go)
    {
        var bounds = LocalBounds(go);
        var box = go.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        return box;
    }

    static Bounds LocalBounds(GameObject go)
    {
        var result = new Bounds();
        var has = false;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var b = renderer.bounds;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var local = go.transform.InverseTransformPoint(corner);
                if (!has) { result = new Bounds(local, Vector3.zero); has = true; }
                else result.Encapsulate(local);
            }
        }
        return result;
    }

    static GameObject Model(string name)
    {
        if (Models.TryGetValue(name, out var model)) return model;
        model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
        if (model == null) throw new Exception($"Missing model '{name}' in {ModelDir}");
        Models[name] = model;
        return model;
    }

    static void MarkStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

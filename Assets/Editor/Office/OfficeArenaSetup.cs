using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireExtinguisher.EditorTools
{
    /// <summary>
    /// Remakes RocketOffice.unity as a chair-friendly arena. The room grows from 20 x 20 m to
    /// 32 x 32 m with 5.5 m walls, so the rocket chair has room to reach speed and fly; the
    /// existing office block stays in the middle as an obstacle course and the clear ring around
    /// it becomes the driving lane. Adds a start paddock by the south door, raises the ceiling
    /// lights and builds three extra breakable glass walls on the ring.
    ///
    /// Idempotent: absolute transforms are rewritten and the generated groups are recreated, so
    /// running it twice produces the same scene. Run it after Tools > Rocket Chair > Rebuild All
    /// Assets if the chair or glass partition was rebuilt.
    /// </summary>
    public static class OfficeArenaSetup
    {
        private const string ScenePath = "Assets/Scenes/RocketOffice.unity";

        private const float HalfSize = 16f;
        private const float WallHeight = 5.5f;
        private const float WallThickness = 0.2f;
        private const float DoorHalfWidth = 1.2f;

        private const string EnvironmentRoot = "RocketOffice/Environment";
        private const string WallsRoot = EnvironmentRoot + "/Walls";
        private const string LightsRoot = "RocketOffice/Lighting";
        private const string SystemsRoot = "RocketOffice/Systems";

        private const string GlassBarriersRootName = "GlassBarriers";
        private const string FillLightsRootName = "ArenaFillLights";

        private static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -14.2f);

        private static readonly Vector3[] FillLightPositions =
        {
            new Vector3(-11.5f, 4.8f, -11.5f),
            new Vector3(11.5f, 4.8f, -11.5f),
            new Vector3(-11.5f, 4.8f, 11.5f),
            new Vector3(11.5f, 4.8f, 11.5f)
        };

        [MenuItem("Tools/Rocket Chair/Remake Office Arena")]
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
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ExpandRoom();
            MoveStartPaddock();
            RaiseCeilingLights();
            BuildFillLights();
            int shards = BuildGlassBarriers();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"OfficeArenaSetup: remade '{ScenePath}' as a {HalfSize * 2f:0} x {HalfSize * 2f:0} m arena " +
                $"with {WallHeight:0.#} m walls, 3 extra glass walls ({shards} shards) and a start paddock " +
                $"at {PlayerStart}. Existing office content is untouched.");
        }

        // ------------------------------------------------------------------ room shell

        private static void ExpandRoom()
        {
            // Floor, walls, skirting, door frame and mat all move together so the office keeps
            // its look; only the dimensions change.
            SetBox(EnvironmentRoot + "/Floor",
                new Vector3(0f, -0.1f, 0f), new Vector3(HalfSize * 2f, 0.2f, HalfSize * 2f));

            float wallCenterY = WallHeight * 0.5f;
            float wallZ = HalfSize - WallThickness * 0.5f;
            float southWidth = HalfSize - DoorHalfWidth;

            SetBox(WallsRoot + "/Wall_North",
                new Vector3(0f, wallCenterY, wallZ),
                new Vector3(HalfSize * 2f, WallHeight, WallThickness));
            SetBox(WallsRoot + "/Wall_East",
                new Vector3(wallZ, wallCenterY, 0f),
                new Vector3(WallThickness, WallHeight, HalfSize * 2f));
            SetBox(WallsRoot + "/Wall_West",
                new Vector3(-wallZ, wallCenterY, 0f),
                new Vector3(WallThickness, WallHeight, HalfSize * 2f));
            SetBox(WallsRoot + "/Wall_South_Left",
                new Vector3(-DoorHalfWidth - southWidth * 0.5f, wallCenterY, -wallZ),
                new Vector3(southWidth, WallHeight, WallThickness));
            SetBox(WallsRoot + "/Wall_South_Right",
                new Vector3(DoorHalfWidth + southWidth * 0.5f, wallCenterY, -wallZ),
                new Vector3(southWidth, WallHeight, WallThickness));
            SetBox(WallsRoot + "/Wall_South_Header",
                new Vector3(0f, WallHeight - 0.5f, -wallZ),
                new Vector3(DoorHalfWidth * 2f, 1f, WallThickness));

            float skirtX = HalfSize - WallThickness - 0.02f;
            float skirtY = 0.06f;
            float skirtLength = HalfSize * 2f - 0.1f;

            SetBox(WallsRoot + "/Skirting/Skirt_North",
                new Vector3(0f, skirtY, skirtX), new Vector3(skirtLength, 0.12f, 0.04f));
            SetBox(WallsRoot + "/Skirting/Skirt_East",
                new Vector3(skirtX, skirtY, 0f), new Vector3(0.04f, 0.12f, skirtLength));
            SetBox(WallsRoot + "/Skirting/Skirt_West",
                new Vector3(-skirtX, skirtY, 0f), new Vector3(0.04f, 0.12f, skirtLength));
            SetBox(WallsRoot + "/Skirting/Skirt_South_Left",
                new Vector3(-DoorHalfWidth - southWidth * 0.5f, skirtY, -skirtX),
                new Vector3(southWidth - 0.06f, 0.12f, 0.04f));
            SetBox(WallsRoot + "/Skirting/Skirt_South_Right",
                new Vector3(DoorHalfWidth + southWidth * 0.5f, skirtY, -skirtX),
                new Vector3(southWidth - 0.06f, 0.12f, 0.04f));

            float doorZ = -HalfSize + WallThickness + 0.01f;
            SetLocalPosition(WallsRoot + "/DoorFrame/Frame_Left", new Vector3(-DoorHalfWidth - 0.06f, 1.3f, doorZ));
            SetLocalPosition(WallsRoot + "/DoorFrame/Frame_Right", new Vector3(DoorHalfWidth + 0.06f, 1.3f, doorZ));
            SetLocalPosition(WallsRoot + "/DoorFrame/Frame_Top", new Vector3(0f, 2.66f, doorZ));
            SetLocalPosition(WallsRoot + "/DoorFrame/Doormat", new Vector3(0f, 0.01f, -HalfSize + 0.9f));
        }

        // ------------------------------------------------------------------ start paddock

        private static void MoveStartPaddock()
        {
            Quaternion facing = Quaternion.identity;

            SetPose(SystemsRoot + "/PlayerSpawn", PlayerStart, facing);
            SetPose(SystemsRoot + "/Player", PlayerStart, facing);

            // Chair and the first extinguisher sit beside the spawn; the other two stay as
            // mid-track pickups against the new walls.
            SetPose("RocketChair_Demo", new Vector3(-2.6f, 0f, -13.6f), facing);
            SetPose("FireExtinguisher_Demo", new Vector3(-1.9f, 0.02f, -13.45f), Quaternion.Euler(0f, 22f, 0f));
            SetPose("FireExtinguisher_WestWall", new Vector3(-15.4f, 0.02f, -0.9f), Quaternion.Euler(0f, 90f, 0f));
            SetPose("FireExtinguisher_EastWall", new Vector3(15.4f, 0.02f, 3.6f), Quaternion.Euler(0f, -90f, 0f));
        }

        // ------------------------------------------------------------------ lights

        private static void RaiseCeilingLights()
        {
            Transform lights = FindRequired(LightsRoot + "/CeilingLights").transform;
            for (int i = 0; i < lights.childCount; i++)
            {
                Transform light = lights.GetChild(i);
                light.localPosition = new Vector3(light.localPosition.x, 4.2f, light.localPosition.z);

                Light component = light.GetComponent<Light>();
                if (component != null)
                    component.range = 12f;
            }
        }

        private static void BuildFillLights()
        {
            Transform lighting = FindRequired(LightsRoot).transform;
            DestroyChild(lighting, FillLightsRootName);

            var group = new GameObject(FillLightsRootName);
            group.transform.SetParent(lighting, false);

            for (int i = 0; i < FillLightPositions.Length; i++)
            {
                var go = new GameObject("Fill_" + i);
                go.transform.SetParent(group.transform, false);
                go.transform.localPosition = FillLightPositions[i];

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.94f, 0.84f);
                light.range = 22f;
                light.intensity = 18f;
                light.shadows = LightShadows.None;
            }
        }

        // ------------------------------------------------------------------ glass barriers

        /// <summary>Builds three extra breakable walls on the ring; returns the shard total.</summary>
        private static int BuildGlassBarriers()
        {
            Transform office = FindRequired("RocketOffice").transform;
            DestroyChild(office, GlassBarriersRootName);

            var group = new GameObject(GlassBarriersRootName);
            group.transform.SetParent(office, false);

            var shards = 0;

            // Both straights get a partial wall with gaps on either side; the extra height over
            // the office partition means a chair has to commit to flying over or smashing through.
            shards += Attach(group, RocketChairSetup.BuildGlassWall(
                "GlassBarrier_North", new Vector3(0f, 0f, 12.7f), Quaternion.identity,
                9f, 3f, 12, 6));

            shards += Attach(group, RocketChairSetup.BuildGlassWall(
                "GlassBarrier_East", new Vector3(13f, 0f, -7.5f), Quaternion.Euler(0f, 90f, 0f),
                8f, 3f, 10, 6));

            shards += Attach(group, RocketChairSetup.BuildGlassWall(
                "GlassBarrier_West", new Vector3(-13f, 0f, 7f), Quaternion.Euler(0f, 90f, 0f),
                8f, 3f, 10, 6));

            return shards;
        }

        private static int Attach(GameObject parent, GameObject wall)
        {
            wall.transform.SetParent(parent.transform, true);

            var panes = wall.GetComponentsInChildren<FireExtinguisher.Rocket.BreakableGlass>(true);
            int shards = 0;
            for (int i = 0; i < panes.Length; i++)
                shards += panes[i].ShardCount;

            return shards;
        }

        // ------------------------------------------------------------------ helpers

        private static void SetBox(string path, Vector3 position, Vector3 scale)
        {
            Transform target = FindRequired(path).transform;
            target.position = position;
            target.localScale = scale;
        }

        private static void SetLocalPosition(string path, Vector3 position)
        {
            FindRequired(path).transform.localPosition = position;
        }

        private static void SetPose(string path, Vector3 position, Quaternion rotation)
        {
            FindRequired(path).transform.SetPositionAndRotation(position, rotation);
        }

        private static GameObject FindRequired(string path)
        {
            GameObject target = GameObject.Find(path);
            if (target == null)
                throw new InvalidOperationException(
                    $"OfficeArenaSetup: '{path}' is missing from '{ScenePath}'. " +
                    "Restore the office scene or run Tools > Rocket Chair > Rebuild All Assets.");

            return target;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
}

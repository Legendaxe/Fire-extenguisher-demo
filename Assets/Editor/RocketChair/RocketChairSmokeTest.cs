using System.Collections;
using System.IO;
using System.Text;
using FireExtinguisher.Interaction;
using FireExtinguisher.Player;
using FireExtinguisher.Rocket;
using FireExtinguisher.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ExtinguisherItem = FireExtinguisher.Rocket.FireExtinguisher;

namespace FireExtinguisher.EditorTools
{
    /// <summary>
    /// Headless Play Mode smoke test for the rocket chair. Runs when Unity is started with the
    /// "-rocketSmoke" argument or from Tools/Rocket Chair/Run Smoke Test. It picks the demo
    /// extinguisher up through the normal interaction path, sits the real scene player on the
    /// demo chair, fires with a virtual button and checks carrying, seated camera, fuel, speed,
    /// spray direction, impact feedback, the breakable glass and the drop button.
    ///
    /// Command line:
    /// Unity.exe -batchmode -projectPath "&lt;project&gt;" -rocketSmoke
    ///   -executeMethod FireExtinguisher.EditorTools.RocketChairSmokeTest.RunFromCommandLine
    ///   -logFile "&lt;project&gt;/Temp/smoke.log"
    ///
    /// The report is also written to "&lt;project&gt;/Logs/RocketChairSmokeTest.log".
    /// </summary>
    public static class RocketChairSmokeTest
    {
        private const string Argument = "-rocketSmoke";
        private const string SessionKey = "FireExtinguisher.RocketChairSmokeTest.Pending";
        private const string ReportPath = "Logs/RocketChairSmokeTest.log";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!ShouldRun())
                return;

            var runner = new GameObject("RocketChairSmokeTest");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<Runner>();
        }

        /// <summary>Runs the test from the open editor without quitting it.</summary>
        [MenuItem("Tools/Rocket Chair/Run Smoke Test")]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // SessionState survives the domain reload that entering Play Mode performs.
            SessionState.SetBool(SessionKey, true);
            RunFromCommandLine();
        }

        /// <summary>Entry point for -executeMethod. Enters Play Mode; the runner exits the editor in batchmode.</summary>
        public static void RunFromCommandLine()
        {
            // Batchmode starts on an empty untitled scene, so load the game scene explicitly.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/RocketOffice.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            Debug.Log("RocketChairSmokeTest: entering Play Mode.");
            EditorApplication.EnterPlaymode();
        }

        private static bool ShouldRun()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, false);
                return true;
            }

            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], Argument, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private sealed class Runner : MonoBehaviour
        {
            private readonly StringBuilder _report = new StringBuilder();
            private int _failures;

            private IEnumerator Start()
            {
                // Let the scene finish its first physics step and Start callbacks.
                yield return null;
                yield return new WaitForFixedUpdate();

                yield return RunTest();

                Debug.Log("RocketChairSmokeTest report:\n" + _report);
                WriteReportFile();

                if (_failures == 0)
                    Debug.Log("RocketChairSmokeTest: PASS");
                else
                    Debug.LogError($"RocketChairSmokeTest: FAIL ({_failures} problem(s))");

                yield return null;

                if (Application.isBatchMode)
                    EditorApplication.Exit(_failures == 0 ? 0 : 1);
                else
                    EditorApplication.ExitPlaymode();
            }

            private IEnumerator RunTest()
            {
                Check(Camera.main != null, "Main Camera exists.");

                CameraFeedback feedback = Camera.main != null
                    ? Camera.main.GetComponent<CameraFeedback>()
                    : null;
                Check(feedback != null, "Main Camera has CameraFeedback.");

                CameraFloorGuard floorGuard = Camera.main != null
                    ? Camera.main.GetComponent<CameraFloorGuard>()
                    : null;
                Check(floorGuard != null, "Main Camera has a floor guard.");

                SeatController seatController = Object.FindFirstObjectByType<SeatController>();
                Check(seatController != null, "Scene player has SeatController.");

                PlayerEquipment equipment = seatController != null
                    ? seatController.GetComponent<PlayerEquipment>()
                    : null;
                Check(equipment != null, "Scene player has PlayerEquipment.");
                Check(equipment != null && equipment.HandAnchor != null, "Player hand anchor is wired.");

                GameObject demoChair = GameObject.Find("RocketChair_Demo");
                Check(demoChair != null, "Demo chair exists in the scene.");

                ChairSeat seat = demoChair != null ? demoChair.GetComponent<ChairSeat>() : null;
                RocketChairController rocket = demoChair != null ? demoChair.GetComponent<RocketChairController>() : null;
                FunnyImpactEffects impacts = demoChair != null ? demoChair.GetComponent<FunnyImpactEffects>() : null;
                RiderImpactGate gate = demoChair != null ? demoChair.GetComponent<RiderImpactGate>() : null;
                Rigidbody chairBody = demoChair != null ? demoChair.GetComponent<Rigidbody>() : null;

                Check(seat != null, "Demo chair has ChairSeat.");
                Check(rocket != null, "Demo chair has RocketChairController.");
                Check(impacts != null, "Demo chair has FunnyImpactEffects.");
                Check(chairBody != null, "Demo chair has Rigidbody.");

                // Regression for "impacts fire when a player pushes the empty chair": feedback is
                // gated on a rider, so an unoccupied chair stays silent even when it hits a desk.
                Check(gate != null, "Demo chair has a rider impact gate.");
                Check(impacts != null && !impacts.ImpactsAllowed,
                    "Empty chair produces no impact feedback.");

                Check(demoChair != null && demoChair.GetComponent<ISeatInteraction>() == null,
                    "Demo chair no longer grabs nearby extinguishers.");

                GameObject prop = GameObject.Find("FireExtinguisher_Demo");
                ExtinguisherItem extinguisher = prop != null ? prop.GetComponent<ExtinguisherItem>() : null;
                Check(extinguisher != null, "Demo extinguisher prop exists in the scene.");

                GameObject glassObject = GameObject.Find("GlassPartition");
                BreakableGlass glass = glassObject != null
                    ? glassObject.GetComponentInChildren<BreakableGlass>()
                    : null;
                Check(glass != null, "Glass partition exists in the scene.");
                Check(glass != null && !glass.IsBroken, "Glass partition starts intact.");

                if (seatController == null || equipment == null || demoChair == null ||
                    seat == null || rocket == null || impacts == null || gate == null ||
                    chairBody == null || extinguisher == null || equipment.HandAnchor == null ||
                    Camera.main == null || glass == null)
                {
                    yield break;
                }

                Check(seat.Config != null && seat.Config.SeatedYawLimit >= 180f,
                    "Demo chair uses the 180 degree rocket sitting config.");

                // The generated placeholder clips must reach the prefabs and the pane, otherwise
                // the game silently loses its sound when the audio folder is rebuilt or missing.
                AudioClip dischargeClip = ReadClip(extinguisher, "dischargeClip");
                AudioClip startClip = ReadClip(extinguisher, "startClip");
                AudioClip stopClip = ReadClip(extinguisher, "stopClip");
                Check(dischargeClip != null && dischargeClip.length > 0.5f,
                    "Extinguisher has a looping discharge clip.");
                Check(startClip != null && startClip.length > 0.1f,
                    "Extinguisher has a start clip.");
                Check(stopClip != null && stopClip.length > 0.1f,
                    "Extinguisher has a stop clip.");
                Check(ReadClip(impacts, "smallClip") != null, "Impact feedback has a small clip.");
                Check(ReadClip(impacts, "mediumClip") != null, "Impact feedback has a medium clip.");
                Check(ReadClip(impacts, "bigClip") != null, "Impact feedback has a big clip.");
                Check(ReadClip(glass, "breakClip") != null, "Glass has a break clip.");

                // Pick up through the real interaction entry point, exactly like pressing Interact.
                extinguisher.Interact(seatController.gameObject);
                yield return null;

                Check(equipment.Equipped == extinguisher, "Interacting equips the extinguisher.");
                Check(extinguisher.IsEquipped, "Equipped extinguisher reports itself as carried.");
                Check(extinguisher.transform.parent == equipment.HandAnchor,
                    "Equipped extinguisher is parented to the hand anchor.");

                Rigidbody heldBody = extinguisher.GetComponent<Rigidbody>();
                Check(heldBody != null && heldBody.isKinematic &&
                      heldBody.interpolation == RigidbodyInterpolation.None,
                    "Held extinguisher is kinematic with interpolation off, so the hand owns it.");

                // Regression for "picked up prop stays where it was": the prop must ride the hand.
                yield return CheckCarryFollow(seatController, equipment, extinguisher);

                extinguisher.Refill();

                if (!seatController.TrySit(seat))
                {
                    Check(false, "Player could sit on the demo chair.");
                    yield break;
                }

                yield return null;
                Check(seatController.IsSeated, "Player is seated.");
                Check(equipment.Equipped == extinguisher, "Extinguisher stays in hand after sitting.");
                Check(impacts.ImpactsAllowed, "Ridden chair reports impact feedback again.");

                Check(Vector3.Distance(seatController.transform.position, seat.SitPoint.position) < 0.03f,
                    "Seated body sits on the chair anchor.");

                // HUD should become visible and readable once something is carried.
                FuelGaugeUI gauge = Object.FindFirstObjectByType<FuelGaugeUI>();
                Transform gaugeRoot = gauge != null ? gauge.transform.Find("FuelGauge") : null;
                Transform labelTransform = gaugeRoot != null ? gaugeRoot.Find("Label") : null;
                Text gaugeLabel = labelTransform != null ? labelTransform.GetComponent<Text>() : null;

                yield return null;
                Check(gauge != null, "HUD has FuelGaugeUI.");
                Check(gaugeRoot != null && gaugeRoot.gameObject.activeSelf, "Fuel gauge shows while carrying.");
                Check(gaugeLabel != null && gaugeLabel.text.StartsWith("FUEL"), "Fuel label shows FUEL.");

                PlayerInputReader input = seatController.GetComponent<PlayerInputReader>();
                Check(input != null, "Scene player has PlayerInputReader.");

                if (input == null)
                {
                    yield break;
                }

                // Stage the thrust measurement on the clear south straight of the arena ring:
                // the start paddock sits close to the south wall, and firing towards the door
                // would take the chair out of the room before the test can measure speed.
                chairBody.position = new Vector3(-12f, 0.15f, -13.5f);
                chairBody.MoveRotation(Quaternion.Euler(0f, -90f, 0f));
                chairBody.linearVelocity = Vector3.zero;
                chairBody.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();

                float fuelBefore = extinguisher.Fuel;
                Vector3 startPosition = demoChair.transform.position;
                float uprightnessBefore = Vector3.Dot(demoChair.transform.up, Vector3.up);
                Vector3 viewForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;

                input.FireOverride = true;
                yield return new WaitForSeconds(0.9f);

                float fuelAfterBurn = extinguisher.Fuel;
                Vector3 velocity = chairBody.linearVelocity;
                float speedWhileFiring = velocity.magnitude;
                float uprightness = Vector3.Dot(demoChair.transform.up, Vector3.up);
                bool wasSpraying = extinguisher.IsSpraying;
                float sprayAlignment = MeasureSprayAlignment(FindSpray(extinguisher), Camera.main.transform.forward);
                Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
                float thrustAlignment = planarVelocity.sqrMagnitude > 0.01f
                    ? Vector3.Dot(planarVelocity.normalized, viewForward)
                    : 0f;

                input.FireOverride = false;
                yield return new WaitForSeconds(0.45f);

                float fuelAfterRelease = extinguisher.Fuel;
                float travel = Vector3.Distance(startPosition, demoChair.transform.position);

                Append($"fuel {fuelBefore:0.0} -> burn {fuelAfterBurn:0.0} -> release {fuelAfterRelease:0.0}");
                Append($"upright before firing {uprightnessBefore:0.00}");
                Append($"speed while firing {speedWhileFiring:0.00} m/s, travelled {travel:0.00} m");
                Append($"upright alignment while firing {uprightness:0.00}");
                Append($"thrust alignment with the view {thrustAlignment:0.00} (negative = opposite)");
                Append($"spray alignment with the view {sprayAlignment:0.00}");

                Check(wasSpraying, "Triggers a spray while Attack is held.");
                Check(fuelBefore - fuelAfterBurn > 2f, "Fuel drains while firing.");
                Check(speedWhileFiring > 1.5f, "Thrust accelerates the chair.");
                Check(uprightness > 0.85f, "Chair stays upright while thrusting.");
                Check(thrustAlignment < -0.7f, "Thrust pushes the chair opposite the view direction.");
                Check(sprayAlignment > 0.9f, "Spray leaves the nozzle along the camera view.");
                Check(travel > 0.4f, "Chair actually moves through the world.");
                Check(Mathf.Abs(fuelAfterRelease - fuelAfterBurn) < 0.35f, "Fuel stops draining after release.");
                Check(!extinguisher.IsSpraying, "Spray stops after release.");
                Check(equipment.Equipped != null, "Extinguisher is still carried after firing.");

                // Empty tank: thrust must cut off and the extinguisher must not spray.
                extinguisher.SetFuel(0f);
                input.FireOverride = true;
                yield return new WaitForSeconds(0.3f);

                Vector3 emptyStart = demoChair.transform.position;
                yield return new WaitForSeconds(0.3f);

                float emptyDrift = Vector3.Distance(emptyStart, demoChair.transform.position);
                Append($"coasting distance after empty {emptyDrift:0.00} m");

                Check(!extinguisher.IsSpraying, "Empty extinguisher does not spray.");
                Check(!rocket.IsThrusting, "Empty extinguisher produces no thrust.");

                input.FireOverride = false;
                yield return null;

                int impactsBefore = impacts.ImpactCount;
                impacts.PlayTestImpact(3);
                yield return null;
                Check(impacts.ImpactCount == impactsBefore + 1, "Impact feedback fires.");
                Check(impacts.LastImpactLevel == 3, "Big impact reports level 3.");

                // Regression for "glass wall broken by high impact".
                yield return CheckGlassSlam(glass, chairBody, demoChair);

                // Regression for "camera should be attached to the chair and rotate with it".
                // Runs last so tilting the chair cannot disturb the thrust measurements.
                yield return CheckSeatedCamera(chairBody, seatController, demoChair, seat);

                // Standing up must keep the prop in hand and leave the body upright again.
                seatController.StandUp();
                yield return null;

                Check(!seatController.IsSeated, "Player can stand up.");
                Check(equipment.Equipped != null, "Extinguisher stays in hand after standing.");
                Check(Vector3.Dot(seatController.transform.up, Vector3.up) > 0.99f,
                    "Standing up restores an upright body.");
                Check(!impacts.ImpactsAllowed, "Chair goes quiet again once the rider stands up.");

                // Regression for "button to drop the extinguisher".
                Check(input.HasDropAction, "Input asset has the Drop action.");

                input.DropOverride = true;
                yield return null;
                yield return null;
                input.DropOverride = false;

                Collider droppedCollider = extinguisher.GetComponent<Collider>();
                Rigidbody droppedBody = extinguisher.GetComponent<Rigidbody>();

                Append($"drop: equipped={equipment.HasEquipment}, parent=" +
                       $"{(extinguisher.transform.parent != null ? extinguisher.transform.parent.name : "world")}");

                Check(!equipment.HasEquipment, "Drop button releases the extinguisher.");
                Check(!extinguisher.IsEquipped && extinguisher.transform.parent == null,
                    "Dropped extinguisher goes back into the world.");
                Check(droppedBody != null && !droppedBody.isKinematic &&
                      droppedCollider != null && droppedCollider.enabled,
                    "Dropped extinguisher gets its physics and colliders back.");
            }

            /// <summary>Reads a private serialized reference, used to assert generated clips.</summary>
            private static AudioClip ReadClip(Object target, string fieldName)
            {
                if (target == null)
                    return null;

                var serialized = new SerializedObject(target);
                SerializedProperty property = serialized.FindProperty(fieldName);
                return property != null ? property.objectReferenceValue as AudioClip : null;
            }

            /// <summary>Finds the powder stream particle system on an extinguisher.</summary>
            private static ParticleSystem FindSpray(ExtinguisherItem extinguisher)
            {
                ParticleSystem[] systems = extinguisher.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i].name == "Spray")
                        return systems[i];
                }

                return null;
            }

            /// <summary>Average direction the live powder particles are moving in, as a dot with the view.</summary>
            private static float MeasureSprayAlignment(ParticleSystem spray, Vector3 viewForward)
            {
                if (spray == null)
                    return 0f;

                var buffer = new ParticleSystem.Particle[Mathf.Max(64, spray.main.maxParticles)];
                int count = spray.GetParticles(buffer);
                if (count == 0)
                    return 0f;

                Vector3 sum = Vector3.zero;
                for (int i = 0; i < count; i++)
                    sum += buffer[i].velocity;

                if (sum.sqrMagnitude < 0.0001f)
                    return 0f;

                return Vector3.Dot(sum.normalized, viewForward.normalized);
            }

            /// <summary>Drives the chair into the glass and checks that it shatters into shards.</summary>
            private IEnumerator CheckGlassSlam(BreakableGlass glass, Rigidbody chairBody, GameObject demoChair)
            {
                Append($"glass: slow bump {glass.WouldBreak(3f, 75f)}, chair slam {glass.WouldBreak(9f, 75f)}");
                Check(!glass.WouldBreak(3f, 75f), "Rolling into the glass at walking speed does not break it.");
                Check(glass.WouldBreak(9f, 75f), "A chair slam is fast enough to break the glass.");

                Transform glassRoot = glass.transform;
                // Start close enough that wheel friction cannot eat the slam speed before contact.
                Vector3 staging = glassRoot.position - glassRoot.forward * 1f;
                staging.y = 0.15f;

                chairBody.WakeUp();
                chairBody.linearVelocity = Vector3.zero;
                chairBody.angularVelocity = Vector3.zero;
                chairBody.position = staging;
                chairBody.MoveRotation(Quaternion.Euler(0f, glassRoot.eulerAngles.y, 0f));
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                chairBody.WakeUp();
                chairBody.linearVelocity = glassRoot.forward * 9f;
                yield return new WaitForSeconds(0.7f);

                float travelled = Vector3.Distance(staging, chairBody.position);
                Append($"glass: chair travelled {travelled:0.00} m from {staging} to {chairBody.position}");

                int activeShards = 0;
                Transform shards = glass.ShardsRoot;
                if (shards != null)
                {
                    for (int i = 0; i < shards.childCount; i++)
                    {
                        Transform shard = shards.GetChild(i);
                        if (!shard.gameObject.activeSelf)
                            continue;

                        Rigidbody shardBody = shard.GetComponent<Rigidbody>();
                        if (shardBody != null && !shardBody.isKinematic)
                            activeShards++;
                    }
                }

                Append($"glass: broken={glass.IsBroken}, flying shards={activeShards}");
                Check(glass.IsBroken, "Chair slam shatters the glass.");
                Check(activeShards > 4, "Broken glass turns into physical shards.");

                // Put the chair back on its wheels in the open part of the room.
                chairBody.linearVelocity = Vector3.zero;
                chairBody.angularVelocity = Vector3.zero;
                chairBody.position = new Vector3(0f, 0.15f, -6.3f);
                chairBody.MoveRotation(Quaternion.identity);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForSeconds(0.3f);
            }

            /// <summary>Moves the player and checks the held prop follows the hand anchor exactly.</summary>
            private IEnumerator CheckCarryFollow(
                SeatController seatController,
                PlayerEquipment equipment,
                ExtinguisherItem extinguisher)
            {
                Transform anchor = equipment.HandAnchor;
                Rigidbody playerBody = seatController.GetComponent<Rigidbody>();

                Vector3 anchorBefore = anchor.position;
                Vector3 extinguisherBefore = extinguisher.transform.position;

                bool wasKinematic = playerBody.isKinematic;
                playerBody.isKinematic = true;

                seatController.transform.position += new Vector3(2f, 0f, 0f);
                Physics.SyncTransforms();
                yield return null;
                yield return null;

                float anchorTravel = Vector3.Distance(anchorBefore, anchor.position);
                float propTravel = Vector3.Distance(extinguisherBefore, extinguisher.transform.position);
                float anchorGap = Vector3.Distance(anchor.position, extinguisher.transform.position);

                Append($"carry: anchor moved {anchorTravel:0.00} m, prop moved {propTravel:0.00} m, gap {anchorGap:0.000} m");

                Check(anchorTravel > 1.5f, "Player moved while carrying the extinguisher.");
                Check(propTravel > 1.5f, "Held extinguisher moves with the player.");
                Check(anchorGap < 0.02f, "Held extinguisher stays at the hand anchor.");

                seatController.transform.position -= new Vector3(2f, 0f, 0f);
                Physics.SyncTransforms();

                playerBody.isKinematic = wasKinematic;
                if (!wasKinematic)
                    playerBody.WakeUp();

                yield return null;
            }

            /// <summary>Tilts the chair and checks the player body and camera ride along with it.</summary>
            private IEnumerator CheckSeatedCamera(
                Rigidbody chairBody,
                SeatController seatController,
                GameObject demoChair,
                ChairSeat seat)
            {
                float yaw = demoChair.transform.eulerAngles.y;
                Quaternion tilt = Quaternion.Euler(0f, yaw, 25f);
                chairBody.MoveRotation(tilt);

                // The player body copies the seat in LateUpdate, so read the camera after a
                // rendered frame instead of in the middle of Update with a stale transform.
                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();

                Transform camera = Camera.main.transform;
                float upDot = Vector3.Dot(camera.up, demoChair.transform.up);
                float worldTilt = Vector3.Angle(camera.up, Vector3.up);

                Append($"seated camera: up dot chair {upDot:0.000}, world tilt {worldTilt:0.0} deg");

                Check(upDot > 0.99f, "Seated camera rotates with the chair.");
                Check(worldTilt > 10f, "Seated camera actually tilts with the chair.");

                // Roll the chair far enough that the seated eye would be under the floor, which
                // is the "camera clips through the floor while rolling" case.
                chairBody.linearVelocity = Vector3.zero;
                chairBody.angularVelocity = Vector3.zero;
                chairBody.MoveRotation(Quaternion.Euler(0f, yaw, 150f));
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();

                float rolledCameraY = Camera.main.transform.position.y;
                Append($"seated camera: world y at 150 deg roll {rolledCameraY:0.00}");
                Check(rolledCameraY > 0.1f, "Camera stays above the floor while the chair rolls.");

                // Put the chair back on its wheels, at rest, for whatever runs next.
                chairBody.linearVelocity = Vector3.zero;
                chairBody.angularVelocity = Vector3.zero;
                chairBody.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
                yield return new WaitForFixedUpdate();
                yield return new WaitForSeconds(0.35f);

                Check(Vector3.Distance(seatController.transform.position, seat.SitPoint.position) < 0.05f,
                    "Seated body follows the seat anchor in position.");
            }

            private void WriteReportFile()
            {
                try
                {
                    string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    string path = Path.Combine(projectRoot, ReportPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);
                    File.WriteAllText(path, _report.ToString());
                    Debug.Log($"RocketChairSmokeTest: report written to {path}");
                }
                catch (IOException exception)
                {
                    Debug.LogWarning($"RocketChairSmokeTest: could not write the report file: {exception.Message}");
                }
            }

            private void Check(bool condition, string description)
            {
                if (condition)
                {
                    Append("PASS  " + description);
                    return;
                }

                _failures++;
                Append("FAIL  " + description);
            }

            private void Append(string line)
            {
                _report.Append("  ").Append(line).Append('\n');
            }
        }
    }
}

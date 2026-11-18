# Rocket Chair

The office chair is the vehicle: sit down, take a fire extinguisher into your hands and hold
the fire button to fly. The extinguisher sprays away from you, so the thrust pushes the chair
in the direction directly opposite the camera, through the chair's center of mass. Steering
is just looking: point where you want the exhaust to go and the chair goes the other way.

## Controls

| Action | Keyboard / Mouse | Gamepad |
| --- | --- | --- |
| Sit / Stand | `E` (`Interact`) | `Y` |
| Pick up extinguisher (on foot) | `E` (`Interact`) | `Y` |
| Drop extinguisher | `G` (`Drop`) | `East` (`B` / `Circle`) |
| Fire extinguisher (thrust) | Left mouse button (`Attack`) | `West` (`X` / `Square`) |
| Look / steer | Mouse | Right stick |

The fire button reuses the existing `Attack` action in `Assets/InputSystem_Actions.inputactions`
(`Player` map). A `Drop` action was added to the same map with `<Keyboard>/g` and
`<Gamepad>/buttonEast` bindings.

## Gameplay rules

- The extinguisher is only ever attached to the player's hand (`CameraPivot/HandAnchor`).
  It is never parented to the chair, and there is no "grab the nearest extinguisher" shortcut
  while seated: you pick one up on foot, then sit down with it.
- While carried the prop's `Rigidbody` is kinematic with interpolation off and its colliders
  are disabled, so the hand anchor fully owns the transform. Physics never writes the prop
  back to an old position, which is what used to leave it behind when the player walked.
- Thrust is `Rigidbody.AddForce(-camera.forward * thrustForce, ForceMode.Force)` in
  `FixedUpdate`, applied at the center of mass. No thrust torque is added.
- The powder leaves the nozzle along the camera's forward axis (`ExtinguisherVFX.aimSource`
  falls back to `Camera.main`), so the stream always points at the crosshair instead of at
  twice the prop's rotation.
- Dropping (`Drop`, `G` / gamepad East) calls `PlayerEquipment.Unequip`, which stops the spray,
  re-parents the prop back into the world above/in front of the player and restores its
  rigidbody, colliders and world scale. It works standing and while seated.
- While seated the player body follows the chair's `SitPoint` in position **and rotation**, so
  the camera is attached to the chair and rolls/pitches with it. Look yaw is a clamped offset
  around the chair's own up axis (`ChairSittingConfig.SeatedYawLimit`).
- A camera floor guard (`CameraFloorGuard` on `Main Camera`, running after every other
  `LateUpdate`) keeps the eye above the surface below it while the chair tips and rolls. It
  probes down past the rider and the ridden chair, so a chair on its side or upside down can no
  longer push the camera through the floor or cut it with the near plane. The clamp uses the
  near clip plane plus a small margin and only moves the camera, never the seat or the physics.
- Velocity is clamped to `maxSpeed`.
- Fuel is consumed only while spraying. At zero fuel the spray and the thrust stop and the
  chamber will not refill itself (`Refill()` exists for future pickups).
- A strong upright assist keeps the chair flyable even under full reverse thrust. It fades in
  above `uprightDeadZone`, is suspended for `impactRecoverySeconds` after a medium/big impact,
  and never blocks rolling or the yaw spin, so chairs can still tip over and tumble.
- Impact feedback (burst, sound, camera shake) only plays while somebody is riding the chair.
  `RiderImpactGate` implements `IImpactGate` and reports `ChairSeat.IsOccupied`, so shoving an
  empty chair into a desk moves it but stays silent. `FunnyImpactEffects.PlayTestImpact` (the
  context menu / smoke test hook) deliberately bypasses the gate.

## The office arena

`Assets/Scenes/RocketOffice.unity` is laid out as a driving arena for the chair. The room is
32 x 32 m with 5.5 m walls (it used to be a 20 x 20 m office with 3.6 m walls), so there is room
to reach `maxSpeed` and enough headroom to fly over the furniture.

- The hand-authored office block - desks, storage, meeting area, reception, printers, wall
  decor, props and the glass partition - stays in the middle as the obstacle course.
- The clear ring between that block and the outer walls is the driving lane. The south straight
  is the widest and shortest way out of the start paddock.
- The start paddock sits just inside the south door: player spawn `(0, 0, -14.2)` facing north,
  the demo chair at `(-2.6, 0, -13.6)` and the demo extinguisher beside it at
  `(-1.9, 0.02, -13.45)`. The other two extinguishers are pickups against the new walls, on the
  west ring at `(-15.4, 0.02, -0.9)` and the east ring at `(15.4, 0.02, 3.6)`.
- Ceiling lights moved up to y = 4.2 with a 12 m range, and four shadowless fill lights at
  `(+-11.5, 4.8, +-11.5)` (22 m range) keep the enlarged ring readable without touching the
  office lighting.
- The south door keeps its 2.4 m opening and header, so the doorway still reads as an exit.

Rebuilding the room: **Tools > Rocket Chair > Remake Office Arena**
(`FireExtinguisher.EditorTools.OfficeArenaSetup`). It writes absolute transforms and recreates
its generated groups (`GlassBarriers`, `ArenaFillLights`) every run, so it is idempotent and
never touches the hand-authored office content. It only *loads* the shared materials and the
glass break clip, so laying out walls does not rewrite assets. Run **Rebuild All Assets** first
if the chair, extinguisher or glass partition were rebuilt.

## Breakable glass

The office partition at `(5, 0, -2)` (`GlassPartition`, 4.4 x 2.4 m, 48 shards) splits the east
side of the office block. The arena adds three bigger barriers on the ring, so a driver can
either fly over them or commit to smashing through:

| Barrier | Position | Rotation | Size | Shards |
| --- | --- | --- | --- | --- |
| `GlassPartition` | `(5, 0, -2)` | 0 deg | 4.4 x 2.4 m | 48 |
| `GlassBarrier_North` | `(0, 0, 12.7)` | 0 deg | 9 x 3 m | 72 |
| `GlassBarrier_East` | `(13, 0, -7.5)` | 90 deg | 8 x 3 m | 60 |
| `GlassBarrier_West` | `(-13, 0, 7)` | 90 deg | 8 x 3 m | 60 |

Each barrier is a frame plus one solid pane; the 240 shard rigidbodies in total are inactive
children that take over on break. They are solid until something hits them hard:
`BreakableGlass` scores the impact the same way as `FunnyImpactEffects` (closing speed times a
mass factor), so a chair at full thrust (75 kg with rider, ~9 m/s) shatters one while walking or
coasting into it does not. On break the pane's collider and renderer switch off, the shards take
over with spread/impact velocities and a camera shake, and they are cleaned up after
`shardLifetime`. All four walls have `A_GlassBreak.wav` assigned.

All barriers come from the same builder, `RocketChairSetup.BuildGlassWall(name, position,
rotation, width, height, columns, rows)`, so the office partition and the extra walls share
their pane, frame, shard and audio setup.

## Scripts

| File | Responsibility |
| --- | --- |
| `Assets/Scripts/RocketChair/FireExtinguisher.cs` | Fuel, spray state, hand attachment, pickup interaction. |
| `Assets/Scripts/RocketChair/ExtinguisherVFX.cs` | Powder stream, start burst, high-speed smoke trail. |
| `Assets/Scripts/RocketChair/RocketChairController.cs` | Thrust, speed clamp, upright assist, rider resolution. |
| `Assets/Scripts/RocketChair/FunnyImpactEffects.cs` | Reusable collision scoring (3 levels), cooldown, FX/sound/shake. |
| `Assets/Scripts/RocketChair/IImpactGate.cs` | Optional gate a prop can use to veto impact feedback. |
| `Assets/Scripts/RocketChair/RiderImpactGate.cs` | Chair gate: impacts only while the seat is occupied. |
| `Assets/Scripts/RocketChair/FunnyImpactFX.cs` | One-shot puff + stars + flash spawned at the contact point. |
| `Assets/Scripts/RocketChair/CameraFeedback.cs` | Impact trauma and continuous rocket rumble on the main camera. |
| `Assets/Scripts/RocketChair/BreakableGlass.cs` | Impact-scored glass pane that shatters into physical shards. |
| `Assets/Scripts/Player/PlayerEquipment.cs` | Hand anchor owner (`IEquipmentHolder`); picks up and drops the prop. |
| `Assets/Scripts/Player/SeatController.cs` | Seated state; copies the seat anchor position and rotation every frame. |
| `Assets/Scripts/Player/CameraFloorGuard.cs` | Keeps the camera out of the floor when the chair tips and rolls. |
| `Assets/Scripts/Player/PlayerLook.cs` | Standing yaw/pitch; seated look offset on top of the chair rotation. |
| `Assets/Scripts/UI/FuelGaugeUI.cs` | HUD fuel bar and percentage. |
| `Assets/Editor/RocketChair/RocketChairSetup.cs` | Builds every generated asset, prefab and scene object. |
| `Assets/Editor/Office/OfficeArenaSetup.cs` | Remakes `RocketOffice.unity` as the 32 x 32 m arena (menu + command line). |
| `Assets/Editor/Office/DeskLampModelFix.cs` | Import hook that repairs the broken arm and shade of `desk_lamp.fbx`. |
| `Assets/Editor/RocketChair/RocketChairSmokeTest.cs` | Headless Play Mode smoke test. |

`PlayerInputReader` gained `FireHeld` (bound to `Attack`), `DropWasPressed` (bound to `Drop`)
plus `FireOverride`/`DropOverride` properties used by the headless test.

## Generated content

Menu: **Tools > Rocket Chair > Rebuild All Assets**. The setup is idempotent and creates:

- `Assets/Materials/RocketOffice/` - extinguisher red/dark/metal/gauge, warning label,
  spray puff, impact star and glass materials.
- `Assets/Textures/RocketOffice/` - generated puff, star and warning-label textures.
- `Assets/Prefabs/RocketOffice/PF_FireExtinguisher.prefab` - physical prop plus the
  first-person model, particle systems and audio sources.
- `Assets/Prefabs/RocketOffice/FX/PF_FunnyImpactFX.prefab` - impact burst.
- `Assets/Physics/RocketOffice/PM_ChairWheel.physicMaterial` - low-friction caster contact.
- `Assets/Audio/RocketOffice/` - the seven generated placeholder WAVs (see the Audio section).
- `Assets/Settings/Chairs/ChairSittingConfig_Rocket.asset` - 180 degree seated yaw limit.
- Updates `PF_OfficeChair.prefab` (wheel colliders, anchors, controller, feedback) and
  `PF_Player.prefab` (`HandAnchor`, `PlayerEquipment`).
- Updates `Assets/Scenes/RocketOffice.unity` (`RocketChair_Demo` chair in the start paddock,
  three extinguisher props, the `GlassPartition` breakable wall, fuel gauge on the HUD,
  `CameraFeedback` and `CameraFloorGuard` on `Main Camera`).

Menu: **Tools > Rocket Chair > Remake Office Arena** (`OfficeArenaSetup`) then rebuilds the
scene shell on top of that content:

- Floor and outer walls at 32 x 32 m, walls 5.5 m high, plus matching skirting, door frame and
  doormat.
- Start paddock (player spawn, demo chair, demo extinguisher) inside the south door.
- `RocketOffice/ArenaFillLights` - four shadowless fill lights for the ring.
- `RocketOffice/GlassBarriers` - three extra breakable glass walls (`GlassBarrier_North`,
  `GlassBarrier_East`, `GlassBarrier_West`).

Both menus are idempotent, so they can be re-run at any time. `OfficeArenaSetup` writes
absolute transforms and recreates only its own generated groups; it does not rewrite materials,
prefabs or audio.

### Desk lamp repair

`Assets/Lowpoly office assets/desk_lamp.fbx` ships with its steel arm and orange shade detached
from the post, so every one of the four office lamps looked exploded. `DeskLampModelFix` is an
`AssetPostprocessor` that rewrites that mesh on import: the arm is hinged at the top of the post
and aimed up over the desk, and the shade hangs from the arm tip with its opening facing down.
The bulb is re-centred on the shade's axis so it no longer pokes through the cone. It is mesh
data only - pivot, scale, materials and the four scene instances are untouched, and no extra
mesh or prefab is generated.

Unity logs a one-line `Importer(FBXImporter) generated inconsistent result` warning for that FBX
because a postprocessor changes imported mesh data; it is harmless and does not loop. Edit the
`ArmPose` / `ShadePose` constants at the top of the file to re-pose the lamp.

All existing `PF_OfficeChair` instances also become rocket chairs because the components live
on the shared prefab.

## Audio

The project had no audio assets. Rather than inventing credits or downloading packs, the
placeholder clips are **generated by a script** and committed with the project:

- Generator: `Tools/Audio/generate_rocket_audio.py` (Python standard library only, deterministic
  seeds). Run `python Tools/Audio/generate_rocket_audio.py` from the project root to regenerate
  or tweak the sounds.
- Output: `Assets/Audio/RocketOffice/`, 44.1 kHz mono 16-bit WAV, imported as PCM,
  decompressed on load, mono and preloaded (`RocketChairSetup.ConfigureAudioImport`).
- `RocketChairSetup` assigns them on every rebuild, so **Tools > Rocket Chair > Rebuild All
  Assets** keeps the slots filled. Missing WAVs log a warning and leave the slot empty.

| File | Slot | Sound |
| --- | --- | --- |
| `A_ExtinguisherDischarge.wav` | `FireExtinguisher.dischargeClip` | 2 s seamless powder hiss (loop) |
| `A_ExtinguisherStart.wav` | `FireExtinguisher.startClip` | valve clunk + pressure burst |
| `A_ExtinguisherStop.wav` | `FireExtinguisher.stopClip` | valve tick + falling hiss |
| `A_ImpactSmall.wav` | `FunnyImpactEffects.smallClip` | light knock |
| `A_ImpactMedium.wav` | `FunnyImpactEffects.mediumClip` | heavier thud with a ring |
| `A_ImpactBig.wav` | `FunnyImpactEffects.bigClip` | boom, crash and comic spring |
| `A_GlassBreak.wav` | `BreakableGlass.breakClip` | crack plus 52 glass tinkles |

These are synthesized placeholders: serviceable in game, but replace them with recorded SFX
when you have them. Drop a new clip on the same slot (or point the generator at better sample
sources) and nothing else needs to change.

## Tuning

Selected on the chair prefab (`RocketChairController`):

- `emptyMass` 10, `riderMass` 65 - the rider mass is added while seated.
- `thrustForce` 1800 N, `maxSpeed` 10 m/s.
- `uprightAcceleration` 50 and `uprightDamping` 4.5 keep the chair soft and tippy while
  coasting. `thrustUprightAcceleration` 200 and `thrustUprightDamping` 12 take over while the
  extinguisher fires: full reverse thrust accelerates at about 2.4 g, which would otherwise
  flip the chair over backwards. `uprightDeadZone` 5 deg, `uprightFullTilt` 25 deg,
  `impactRecoverySeconds` 1.
- `firingShake` 0..1 scales the continuous camera rumble.

On the extinguisher prefab (`FireExtinguisher`):

- `fuelCapacity` 100, `consumptionPerSecond` 7.5 (about 13 seconds of continuous burn).
- `handLocalPosition/EulerAngles/Scale` control how the first-person model sits in the hand.
  The default is `(0, 0, 0)`, `(0, -20, 0)` and `0.4` so the tank reads as a held prop instead
  of filling the screen.

On `FunnyImpactEffects`:

- `minimumSpeed` 3, `mediumSpeed` 6, `bigSpeed` 9 m/s, scaled by the other body's mass.
- `cooldown` 0.35 s and pair de-duplication keep impacts readable.

On the glass panes (`BreakableGlass`, on each barrier's `Pane`):

- `minimumSpeed` 4 m/s, `breakScore` 15 (speed x mass factor). A seated chair (75 kg) breaks
  it at about 7.5 m/s; the player on foot (70 kg, 7 m/s sprint) sits just under the threshold.
- `shardLifetime` 6 s, `shardSpeed` 2.4 m/s, `shardSpin` 9, `cameraTrauma` 0.45.

On the player (`PlayerEquipment`):

- `dropForwardDistance` 0.6 m and `dropHeight` 0.8 m place the released prop in front of the
  player, out of their own capsule.

## Automated verification

Headless Play Mode test (picks the scene extinguisher up through the real interaction path,
checks it rides the hand while the player moves, checks every generated audio clip is wired,
sits the real scene player on the demo chair, fires, measures fuel/speed/thrust direction and
spray direction, empties the tank, triggers an impact, rams the glass partition, checks the
seated camera and the floor guard at a 150 degree chair roll, stands up and drops the prop):

```
"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode ^
  -projectPath "<project>" -rocketSmoke ^
  -executeMethod FireExtinguisher.EditorTools.RocketChairSmokeTest.RunFromCommandLine ^
  -logFile "<project>\Temp\smoke.log"
```

Exit code 0 and `RocketChairSmokeTest: PASS` in the log mean every check passed. The report is
also written to `<project>/Logs/RocketChairSmokeTest.log`.

The same test can be run from an open Editor with **Tools > Rocket Chair > Run Smoke Test**
(it enters Play Mode, logs the report and leaves Play Mode again without quitting the Editor).

The test drives thrust and the drop button through `PlayerInputReader.FireOverride` and
`DropOverride` because a batchmode editor has no focused Game view, so simulated input state is
reset every frame by the Input System. The real bindings (`Attack` = `/Mouse/leftButton`,
`Drop` = `<Keyboard>/g` and `<Gamepad>/buttonEast`) exist in the input asset and are asserted by
the test.

## Manual checks that cannot be automated here

1. Visual look of the first-person extinguisher model, spray and impact particles.
2. Audio mix and how the generated placeholders sound in a session.
3. Camera feel: impact trauma, rocket rumble, motion sickness.
4. Flight tuning in the real 32 x 32 m arena (thrust, max speed, upright assist strength,
   whether the ring is wide enough to build speed before a barrier).
5. How the glass shatter reads in motion (shard size, spread and lifetime) on the three extra
   3 m-high barriers, which a chair can also fly over.

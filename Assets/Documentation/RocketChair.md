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

## Breakable glass

The scene contains a framed glass partition at `(5, 0, -2)` (`GlassPartition`) that splits the
east side of the office. It is solid until something hits it hard: `BreakableGlass` scores the
impact the same way as `FunnyImpactEffects` (closing speed times a mass factor), so a chair at
full thrust (75 kg with rider, ~9 m/s) shatters it while walking or coasting into it does not.
On break the pane's collider and renderer switch off, 48 pre-built shard rigidbodies take over
with spread/impact velocities and a camera shake, and the shards are cleaned up after
`shardLifetime`. Break audio is a slot, not a clip: assign `breakClip` to hear it.

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
| `Assets/Scripts/Player/PlayerLook.cs` | Standing yaw/pitch; seated look offset on top of the chair rotation. |
| `Assets/Scripts/UI/FuelGaugeUI.cs` | HUD fuel bar and percentage. |
| `Assets/Editor/RocketChair/RocketChairSetup.cs` | Builds every generated asset, prefab and scene object. |
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
- Updates `Assets/Scenes/RocketOffice.unity` (`RocketChair_Demo` chair, three extinguisher
  props, the `GlassPartition` breakable wall, fuel gauge on the HUD, `CameraFeedback` on
  `Main Camera`).

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

On the glass pane (`BreakableGlass`, on `GlassPartition/Pane`):

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
seated camera, stands up and drops the prop):

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
4. Flight tuning in a real 20x20 m room (thrust, max speed, upright assist strength).
5. How the glass shatter reads in motion (shard size, spread and lifetime).

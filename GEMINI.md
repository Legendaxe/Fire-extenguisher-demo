
# Fire extinguisher - Project Instructions

## Project
Unity 6, 3D URP, physics-based co-op friendslop.

## Architecture
- Use modular MonoBehaviours.
- Prefer composition over inheritance.
- Use interfaces for interactions.
- Use ScriptableObjects for configurable gameplay data.
- Keep gameplay logic separate from visual effects.
- Use the Unity Input System.

## Physics
- Rigidbody-based movement.
- Avoid transform-based movement for physics objects.
- Use FixedUpdate for physics.
- Use AddForce and AddTorque for propulsion.
- Avoid unnecessary Rigidbody constraints.
- Prefer primitive colliders.

## Code Quality
- Small, focused components.
- Serialized private fields.
- Clear naming.
- No unnecessary dependencies.
- Avoid creating duplicate scripts or components.
- Do not change existing architecture without asking.

## Workflow
1. Inspect the existing project.
2. Explain the implementation plan.
3. Implement one feature at a time.
4. Compile and inspect Console errors.
5. Test in Play Mode.
6. Summarize changed files and remaining issues.

Never delete assets, scenes or project settings
without explicit approval.

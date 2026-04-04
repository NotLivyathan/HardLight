using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._HL.Cleanup;

/// <summary>
/// Content-side mitigation for stale physics entries during entity teardown.
/// Ensures terminating entities stop colliding immediately before components are removed.
/// </summary>
public sealed class PhysicsTerminationCleanupSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PhysicsComponent, EntityTerminatingEvent>(OnPhysicsTerminating);
    }

    private void OnPhysicsTerminating(EntityUid uid, PhysicsComponent component, ref EntityTerminatingEvent args)
    {
        if (!component.CanCollide)
            return;

        _physics.SetCanCollide(uid, false, dirty: false, body: component);
    }
}

using Content.Server.Movement.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Throwing;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Movement.Systems;

public sealed class JumpCollisionKnockbackSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JumpCollisionKnockbackComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<JumpCollisionKnockbackComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<ActiveLeaperComponent>(ent))
            return;

        if (!TryComp<ThrownItemComponent>(ent, out var thrown) || thrown.Landed)
            return;

        var other = args.OtherEntity;
        if (HasComp<MapGridComponent>(other) || HasComp<MapComponent>(other))
            return;

        if (!HasComp<MobStateComponent>(other))
            return;

        if (!TryComp<PhysicsComponent>(other, out var physics) || !physics.Hard)
            return;

        var ourPos = _transform.GetWorldPosition(ent);
        var otherPos = _transform.GetWorldPosition(other);

        _throwing.TryThrow(other, otherPos - ourPos, baseThrowSpeed: ent.Comp.ThrowForce, user: ent);
    }
}
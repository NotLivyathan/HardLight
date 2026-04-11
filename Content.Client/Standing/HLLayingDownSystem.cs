using System.Numerics;
using Content.Shared.Buckle;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Standing;

/// <summary>
/// Restores directional lying visuals by selecting a horizontal angle from current facing.
/// </summary>
public sealed class HLLayingDownSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StandingStateComponent, MoveEvent>(OnMovementInput);
        SubscribeLocalEvent<StandingStateComponent, DownedEvent>(OnDowned);
    }

    private void OnDowned(Entity<StandingStateComponent> ent, ref DownedEvent args)
    {
        UpdateHorizontalRotation(ent.Owner, ignoreAnimationLock: true);
    }

    private void OnMovementInput(Entity<StandingStateComponent> ent, ref MoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        UpdateHorizontalRotation(ent.Owner);
    }

    private void UpdateHorizontalRotation(EntityUid uid, bool ignoreAnimationLock = false)
    {
        if (!_standing.IsDown(uid)
            || _buckle.IsBuckled(uid)
            || (!ignoreAnimationLock && _animation.HasRunningAnimation(uid, "rotate"))
            || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
        {
            return;
        }

        var targetRotation = GetTargetHorizontalRotation(uid);

        if (rotationVisuals.HorizontalRotation == targetRotation)
            return;

        rotationVisuals.HorizontalRotation = targetRotation;
    }

    private Angle GetTargetHorizontalRotation(EntityUid uid)
    {
        // Use current movement intent first. Toggling crawl while moving can happen before facing updates.
        if (TryComp<InputMoverComponent>(uid, out var mover) && mover.WishDir != Vector2.Zero)
        {
            if (mover.WishDir.X > 0f)
                return Angle.FromDegrees(270);

            if (mover.WishDir.X < 0f)
                return Angle.FromDegrees(90);
        }

        var rotation = _xform.GetWorldRotation(uid);
        return rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North
            ? Angle.FromDegrees(270)
            : Angle.FromDegrees(90);
    }
}

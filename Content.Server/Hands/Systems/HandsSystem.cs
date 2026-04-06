using System.Numerics;
using Content.Server.Stack;
using Content.Server.Stunnable;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems; // Shitmed
using Content.Shared._Shitmed.Body.Events; // Shitmed
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem; // HardLight
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Hands.Systems
{
    public sealed class HandsSystem : SharedHandsSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly PullingSystem _pullingSystem = default!;
        [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
        [Dependency] private readonly SharedBodySystem _bodySystem = default!; // Shitmed

        private EntityQuery<PhysicsComponent> _physicsQuery;

        /// <summary>
        /// Items dropped when the holder falls down will be launched in
        /// a direction offset by up to this many degrees from the holder's
        /// movement direction.
        /// </summary>
        private const float DropHeldItemsSpread = 45;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HandsComponent, DisarmedEvent>(OnDisarmed, before: new[] {typeof(StunSystem), typeof(SharedStaminaSystem)});

            SubscribeLocalEvent<HandsComponent, BodyPartAddedEvent>(HandleBodyPartAdded);
            SubscribeLocalEvent<HandsComponent, BodyPartRemovedEvent>(HandleBodyPartRemoved);

            SubscribeLocalEvent<HandsComponent, ComponentGetState>(GetComponentState);

            SubscribeLocalEvent<HandsComponent, BeforeExplodeEvent>(OnExploded);
            SubscribeLocalEvent<HandsComponent, BodyPartEnabledEvent>(HandleBodyPartEnabled); // Shitmed
            SubscribeLocalEvent<HandsComponent, BodyPartDisabledEvent>(HandleBodyPartDisabled); // Shitmed

            SubscribeLocalEvent<HandsComponent, DropHandItemsEvent>(OnDropHandItems);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.ThrowItemInHand, new PointerInputCmdHandler(HandleThrowItem))
                .Register<HandsSystem>();

            _physicsQuery = GetEntityQuery<PhysicsComponent>();
        }

        public override void Shutdown()
        {
            base.Shutdown();

            CommandBinds.Unregister<HandsSystem>();
        }

        private void GetComponentState(EntityUid uid, HandsComponent hands, ref ComponentGetState args)
        {
            args.State = new HandsComponentState(hands);
        }


        private void OnExploded(Entity<HandsComponent> ent, ref BeforeExplodeEvent args)
        {
            if (ent.Comp.DisableExplosionRecursion)
                return;

            foreach (var held in EnumerateHeld(ent.AsNullable()))
            {
                args.Contents.Add(held);
            }
        }

        private void OnDisarmed(EntityUid uid, HandsComponent component, DisarmedEvent args)
        {
            if (args.Handled)
                return;

            // Break any pulls
            if (TryComp(uid, out PullerComponent? puller) && TryComp(puller.Pulling, out PullableComponent? pullable))
                _pullingSystem.TryStopPull(puller.Pulling.Value, pullable, ignoreGrab: true); // Goobstation: Added check for grab

            var offsetRandomCoordinates = _transformSystem.GetMoverCoordinates(args.Target).Offset(_random.NextVector2(1f, 1.5f));
            if (!ThrowHeldItem(args.Target, offsetRandomCoordinates))
                return;


            args.Handled = true; // no shove/stun.
        }

        // Shitmed start
        private void TryAddHand(EntityUid uid, HandsComponent component, Entity<BodyPartComponent> part, string slot)
        {
            if (part.Comp is null
                || part.Comp.PartType != BodyPartType.Hand)
                return;

            // If this annoys you, which it should.
            // Ping Smugleaf.
            var location = part.Comp.Symmetry switch
            {
                BodyPartSymmetry.None => HandLocation.Middle,
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(part.Comp.Symmetry))
            };

            if (part.Comp.Enabled
                && _bodySystem.TryGetParentBodyPart(part, out var _, out var parentPartComp)
                && parentPartComp.Enabled)
                AddHand(uid, slot, location);
        }

        private void HandleBodyPartAdded(Entity<HandsComponent> ent, ref BodyPartAddedEvent args)
        {
            TryAddHand(ent.AsNullable(), ent.Comp, args.Part, args.Slot); // HardLight: component<ent.Comp
        }

        private void HandleBodyPartRemoved(EntityUid uid, HandsComponent component, ref BodyPartRemovedEvent args)
        {
            if (args.Part.Comp is null
                || args.Part.Comp.PartType != BodyPartType.Hand)
                return;
            RemoveHand(uid, args.Slot);
        }

        private void HandleBodyPartEnabled(EntityUid uid, HandsComponent component, ref BodyPartEnabledEvent args) =>
            TryAddHand(uid, component, args.Part, SharedBodySystem.GetPartSlotContainerId(args.Part.Comp.ParentSlot?.Id ?? string.Empty));

        private void HandleBodyPartDisabled(EntityUid uid, HandsComponent component, ref BodyPartDisabledEvent args)
        {
            if (TerminatingOrDeleted(uid)
                || args.Part.Comp is null
                || args.Part.Comp.PartType != BodyPartType.Hand)
                return;

            RemoveHand(uid, SharedBodySystem.GetPartSlotContainerId(args.Part.Comp.ParentSlot?.Id ?? string.Empty));
        }
        // Shitmed end

        #region interactions

        private bool HandleThrowItem(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            // Goobstation start
            if (TryGetActiveItem(player, out var item) && TryComp<VirtualItemComponent>(item, out var virtComp))
            {
                var userEv = new VirtualItemDropAttemptEvent(virtComp.BlockingEntity, player, item.Value, true);
                RaiseLocalEvent(player, userEv);

                var targEv = new VirtualItemDropAttemptEvent(virtComp.BlockingEntity, player, item.Value, true);
                RaiseLocalEvent(virtComp.BlockingEntity, targEv);

                if (userEv.Cancelled || targEv.Cancelled)
                    return false;
            }
            // Goobstation end

            return ThrowHeldItem(player, coordinates);
        }

        /// <summary>
        /// Throw the player's currently held item.
        /// </summary>
        public bool ThrowHeldItem(EntityUid player, EntityCoordinates coordinates, float minDistance = 0.1f)
        {
            if (ContainerSystem.IsEntityInContainer(player) ||
                !TryComp(player, out HandsComponent? hands) ||
                !TryGetActiveItem((player, hands), out var throwEnt) ||
                !_actionBlockerSystem.CanThrow(player, throwEnt.Value))
                return false;

           // Goobstation start: Added throwing for grabbed mobs, mnoved direction.
            var direction = _transformSystem.ToMapCoordinates(coordinates).Position - _transformSystem.GetWorldPosition(player);

            if (TryComp<VirtualItemComponent>(throwEnt, out var virt))
            {
                var userEv = new VirtualItemThrownEvent(virt.BlockingEntity, player, throwEnt.Value, direction); // HardLight: Added .Value
                RaiseLocalEvent(player, userEv);

                var targEv = new VirtualItemThrownEvent(virt.BlockingEntity, player, throwEnt.Value, direction); // HardLight: Added .Value
                RaiseLocalEvent(virt.BlockingEntity, targEv);
            }
            // Goobstation end

            if (_timing.CurTime < hands.NextThrowTime)
                return false;
            hands.NextThrowTime = _timing.CurTime + hands.ThrowCooldown;

            if (EntityManager.TryGetComponent(throwEnt, out StackComponent? stack) && stack.Count > 1 && stack.ThrowIndividually)
            {
                var splitStack = _stackSystem.Split(throwEnt.Value, 1, EntityManager.GetComponent<TransformComponent>(player).Coordinates, stack);

                if (splitStack is not { Valid: true })
                    return false;

                throwEnt = splitStack.Value;
            }

            if (direction == Vector2.Zero)
                return true;

            var length = direction.Length();
            var distance = Math.Clamp(length, minDistance, hands.ThrowRange);
            direction *= distance / length;

            var throwSpeed = hands.BaseThrowspeed;

            // Let other systems change the thrown entity (useful for virtual items)
            // or the throw strength.
            var itemEv = new BeforeGettingThrownEvent(throwEnt.Value, direction, throwSpeed, player); // HardLight: Added .Value
            RaiseLocalEvent(player, ref itemEv);

            if (itemEv.Cancelled)
                return true;

            var ev = new BeforeThrowEvent(throwEnt.Value, direction, throwSpeed, player);
            RaiseLocalEvent(player, ref ev);

            if (ev.Cancelled)
                return true;

            // This can grief the above event so we raise it afterwards
            if (IsHolding((player, hands), throwEnt, out _) && !TryDrop(player, throwEnt.Value))
                return false;

            _throwingSystem.TryThrow(ev.ItemUid, ev.Direction, ev.ThrowSpeed, ev.PlayerUid, compensateFriction: !HasComp<LandAtCursorComponent>(ev.ItemUid));

            return true;
        }

        private void OnDropHandItems(Entity<HandsComponent> entity, ref DropHandItemsEvent args)
        {
            // If the holder doesn't have a physics component, they ain't moving
            var holderVelocity = _physicsQuery.TryComp(entity, out var physics) ? physics.LinearVelocity : Vector2.Zero;
            var spreadMaxAngle = Angle.FromDegrees(DropHeldItemsSpread);

            var fellEvent = new FellDownEvent(entity);
            RaiseLocalEvent(entity, fellEvent);

            foreach (var hand in entity.Comp.Hands.Keys)
            {
                if (!TryGetHeldItem(entity.AsNullable(), hand, out var heldEntity))
                    continue;

                var throwAttempt = new FellDownThrowAttemptEvent(entity);
                RaiseLocalEvent(heldEntity.Value, ref throwAttempt);

                if (throwAttempt.Cancelled)
                    continue;

                if (!TryDrop(entity.AsNullable(), hand, checkActionBlocker: false))
                    continue;

                // Rotate the item's throw vector a bit for each item
                var angleOffset = _random.NextAngle(-spreadMaxAngle, spreadMaxAngle);
                // Rotate the holder's velocity vector by the angle offset to get the item's velocity vector
                var itemVelocity = angleOffset.RotateVec(holderVelocity);
                // Decrease the distance of the throw by a random amount
                itemVelocity *= _random.NextFloat(1f);
                // Heavier objects don't get thrown as far
                // If the item doesn't have a physics component, it isn't going to get thrown anyway, but we'll assume infinite mass
                itemVelocity *= _physicsQuery.TryComp(heldEntity, out var heldPhysics) ? heldPhysics.InvMass : 0;
                // Throw at half the holder's intentional throw speed and
                // vary the speed a little to make it look more interesting
                var throwSpeed = entity.Comp.BaseThrowspeed * _random.NextFloat(0.45f, 0.55f);

                _throwingSystem.TryThrow(heldEntity.Value,
                    itemVelocity,
                    throwSpeed,
                    entity,
                    pushbackRatio: 0,
                    compensateFriction: false
                );
            }
        }

        #endregion
    }
}

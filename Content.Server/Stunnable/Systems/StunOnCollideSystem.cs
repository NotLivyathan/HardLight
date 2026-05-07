using Content.Server.Stunnable.Components;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Shared.Physics.Dynamics;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Stunnable
{
    [UsedImplicitly]
    internal sealed class StunOnCollideSystem : EntitySystem
    {
        [Dependency] private readonly StunSystem _stunSystem = default!;
        [Dependency] private readonly InventorySystem _inventory = default!; // Hardlight
        [Dependency] private readonly TagSystem _tag = default!; // Hardlight;
        private static readonly ProtoId<TagPrototype> IgnoreKnockdown = "KnockdownImmune";

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StunOnCollideComponent, StartCollideEvent>(HandleCollide);
            SubscribeLocalEvent<StunOnCollideComponent, ThrowDoHitEvent>(HandleThrow);
        }

        private void TryDoCollideStun(EntityUid uid, StunOnCollideComponent component, EntityUid target)
        {

            if (EntityManager.TryGetComponent<StatusEffectsComponent>(target, out var status))
            {
                if (_inventory.TryGetSlotEntity(target, "outerClothing", out var armour)) // Hardlight start
                {
                    if (_tag.HasTag(armour.Value, IgnoreKnockdown))
                    {
                        return;
                    }
                } // Hardlight end

                _stunSystem.TryStun(target, TimeSpan.FromSeconds(component.StunAmount), true, status);

                _stunSystem.TryKnockdown(target, TimeSpan.FromSeconds(component.KnockdownAmount), true,
                    status: status);

                _stunSystem.TrySlowdown(target, TimeSpan.FromSeconds(component.SlowdownAmount), true,
                    component.WalkSpeedMultiplier, component.RunSpeedMultiplier, status);
            }
        }
        private void HandleCollide(EntityUid uid, StunOnCollideComponent component, ref StartCollideEvent args)
        {
            if (args.OurFixtureId != component.FixtureID)
                return;

            TryDoCollideStun(uid, component, args.OtherEntity);
        }

        private void HandleThrow(EntityUid uid, StunOnCollideComponent component, ThrowDoHitEvent args)
        {
            TryDoCollideStun(uid, component, args.Target);
        }
    }
}

using Content.Server.Radiation.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Radiation.Events;

namespace Content.Server.Radiation.Systems;

public sealed class RadiationHealingSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadiationHealingComponent, OnIrradiatedEvent>(OnIrradiated);
    }

    private void OnIrradiated(Entity<RadiationHealingComponent> ent, ref OnIrradiatedEvent args)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        if (damageable.TotalDamage <= 0)
            return;

        var remainingHealing = args.TotalRads * ent.Comp.HealPerRad;
        if (remainingHealing <= 0f)
            return;

        var healing = new DamageSpecifier();

        foreach (var (type, amount) in damageable.Damage.DamageDict)
        {
            if (remainingHealing <= 0f)
                break;

            if (amount <= 0)
                continue;

            var healAmount = FixedPoint2.New(-Math.Min(amount.Float(), remainingHealing));
            if (healAmount == FixedPoint2.Zero)
                continue;

            healing.DamageDict[type] = healAmount;
            remainingHealing += healAmount.Float();
        }

        if (healing.DamageDict.Count == 0)
            return;

        _damageable.TryChangeDamage(ent, healing, true, false, damageable, origin: args.Origin, canSever: false);
    }
}
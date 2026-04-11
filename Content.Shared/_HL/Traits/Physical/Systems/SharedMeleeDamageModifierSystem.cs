using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._HL.Traits.Physical.Systems;

/// <summary>
/// Applies trait-based flat melee damage bonuses/penalties to the attacker.
/// Shared so client prediction and server authority stay in sync.
/// </summary>
public sealed class SharedMeleeDamageModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<MeleeWeaponComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (!TryComp<MeleeDamageModifierComponent>(args.User, out var modifier)
            || modifier.FlatBonus == 0)
        {
            return;
        }

        var bonusDamage = new DamageSpecifier();
        bonusDamage.DamageDict[modifier.DamageType] = modifier.FlatBonus;
        args.BonusDamage += bonusDamage;
    }
}

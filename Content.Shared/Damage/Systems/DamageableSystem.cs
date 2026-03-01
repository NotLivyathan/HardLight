using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
// Shitmed Change
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Random;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedBodySystem _body = default!; // Shitmed Change
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedChemistryGuideDataSystem _chemistryGuideData = default!;

    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<MindContainerComponent> _mindContainerQuery;

    public float UniversalAllDamageModifier { get; private set; } = 1f;
    public float UniversalAllHealModifier { get; private set; } = 1f;
    public float UniversalMeleeDamageModifier { get; private set; } = 1f;
    public float UniversalProjectileDamageModifier { get; private set; } = 1f;
    public float UniversalHitscanDamageModifier { get; private set; } = 1f;
    public float UniversalReagentDamageModifier { get; private set; } = 1f;
    public float UniversalReagentHealModifier { get; private set; } = 1f;
    public float UniversalExplosionDamageModifier { get; private set; } = 1f;
    public float UniversalThrownDamageModifier { get; private set; } = 1f;
    public float UniversalTopicalsHealModifier { get; private set; } = 1f;
    public float UniversalMobDamageModifier { get; private set; } = 1f;

    /// <summary>
    ///     Directly sets the damage specifier of a damageable component.
    /// </summary>
    /// <remarks>
    ///     Useful for some unfriendly folk. Also ensures that cached values are updated and that a damage changed
    ///     event is raised.
    /// </remarks>
    public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
    {
        damageable.Damage = damage;
        DamageChanged(uid, damageable);
    }

    /// <summary>
    ///     If the damage in a DamageableComponent was changed, this function should be called.
    /// </summary>
    /// <remarks>
    ///     This updates cached damage information, flags the component as dirty, and raises a damage changed event.
    ///     The damage changed event is used by other systems, such as damage thresholds.
    /// </remarks>
    public void DamageChanged(EntityUid uid, DamageableComponent component, DamageSpecifier? damageDelta = null,
        bool interruptsDoAfters = true, EntityUid? origin = null, bool? canSever = null) // Shitmed Change
    {
        component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
        component.TotalDamage = component.Damage.GetTotal();
        Dirty(uid, component);

        if (_appearanceQuery.TryGetComponent(uid, out var appearance) && damageDelta != null)
        {
            var data = new DamageVisualizerGroupData(component.DamagePerGroup.Keys.ToList());
            _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, data, appearance);
        }
        RaiseLocalEvent(uid, new DamageChangedEvent(component, damageDelta, interruptsDoAfters, origin, canSever ?? true)); // Shitmed Change
    }

    /// <summary>
    ///     Applies damage specified via a <see cref="DamageSpecifier"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="DamageSpecifier"/> is effectively just a dictionary of damage types and damage values. This
    ///     function just applies the container's resistances (unless otherwise specified) and then changes the
    ///     stored damage data. Division of group damage into types is managed by <see cref="DamageSpecifier"/>.
    /// </remarks>
    /// <returns>
    ///     Returns a <see cref="DamageSpecifier"/> with information about the actual damage changes. This will be
    ///     null if the user had no applicable components that can take damage.
    /// </returns>
    public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
        bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null,
        // Shitmed Change
        bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f, TargetBodyPart? targetPart = null, EntityUid? tool = null, float armorPenetration = 0)
    {
        if (!uid.HasValue || !_damageableQuery.Resolve(uid.Value, ref damageable, false))
        {
            // TODO BODY SYSTEM pass damage onto body system
            return null;
        }

        if (damage.Empty)
        {
            return damage;
        }

        var before = new BeforeDamageChangedEvent(damage, origin, targetPart); // Shitmed Change
        RaiseLocalEvent(uid.Value, ref before);

        if (before.Cancelled)
            return null;

        // Shitmed Change Start
        var partDamage = new TryChangePartDamageEvent(damage, origin, targetPart, ignoreResistances, canSever ?? true, canEvade ?? false, partMultiplier ?? 1.00f);
        RaiseLocalEvent(uid.Value, ref partDamage);

        if (partDamage.Evaded || partDamage.Cancelled)
            return null;

        // Shitmed Change End

        // Apply resistances
        if (!ignoreResistances)
        {
            if (damageable.DamageModifierSetId != null &&
                _prototypeManager.TryIndex<DamageModifierSetPrototype>(damageable.DamageModifierSetId, out var modifierSet))
            {
                // TODO DAMAGE PERFORMANCE
                // use a local private field instead of creating a new dictionary here..
                // TODO: We need to add a check to see if the given armor covers the targeted part (if any) to modify or not.
                damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
            }

            var ev = new DamageModifyEvent(damage, origin, armorPenetration, targetPart, tool); // Shitmed Change
            RaiseLocalEvent(uid.Value, ev);
            damage = ev.Damage;

            if (damage.Empty)
            {
                return damage;
            }
        }

        damage = ApplyUniversalAllModifiers(damage);

        // TODO DAMAGE PERFORMANCE
        // Consider using a local private field instead of creating a new dictionary here.
        // Would need to check that nothing ever tries to cache the delta.
        var delta = new DamageSpecifier();
        delta.DamageDict.EnsureCapacity(damage.DamageDict.Count);

        var dict = damageable.Damage.DamageDict;
        foreach (var (type, value) in damage.DamageDict)
        {
            // CollectionsMarshal my beloved.
            if (!dict.TryGetValue(type, out var oldValue))
                continue;

            var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
            if (newValue == oldValue)
                continue;

            dict[type] = newValue;
            delta.DamageDict[type] = newValue - oldValue;
        }

        if (delta.DamageDict.Count > 0)
            DamageChanged(uid.Value, damageable, delta, interruptsDoAfters, origin, canSever); // Shitmed Change

        return delta;
    }

    /// <summary>
    ///     Sets all damage types supported by a <see cref="DamageableComponent"/> to the specified value.
    /// </summary>
    /// <remakrs>
    ///     Does nothing If the given damage value is negative.
    /// </remakrs>
    public void SetAllDamage(EntityUid uid, DamageableComponent component, FixedPoint2 newValue)
    {
        if (newValue < 0)
        {
            // invalid value
            return;
        }

        foreach (var type in component.Damage.DamageDict.Keys)
        {
            component.Damage.DamageDict[type] = newValue;
        }

        // Setting damage does not count as 'dealing' damage, even if it is set to a larger value, so we pass an
        // empty damage delta.
        DamageChanged(uid, component, new DamageSpecifier());

        // Shitmed Change Start
        if (HasComp<TargetingComponent>(uid))
        {
            foreach (var (part, _) in _body.GetBodyChildren(uid))
            {
                if (!TryComp(part, out DamageableComponent? damageComp))
                    continue;

                SetAllDamage(part, damageComp, newValue);
            }
        }
        // Shitmed Change End
    }

    public void ChangeAllDamage(EntityUid uid, DamageableComponent component, FixedPoint2 addedValue)
    {
        foreach (var type in component.Damage.DamageDict.Keys)
        {
            component.Damage.DamageDict[type] += addedValue;
            if (component.Damage.DamageDict[type] < 0)
                component.Damage.DamageDict[type] = 0;
        }

        // Changing damage does not count as 'dealing' damage, even if it is set to a larger value, so we pass an
        // empty damage delta.
        DamageChanged(uid, component, new DamageSpecifier());

        // Shitmed Change Start
        if (!HasComp<TargetingComponent>(uid))
            return;

        foreach (var (part, _) in _body.GetBodyChildren(uid))
        {
            if (!TryComp(part, out DamageableComponent? damageComp))
                continue;

            ChangeAllDamage(part, damageComp, addedValue);
        }
        // Shitmed Change End
    }

    public void SetDamageModifierSetId(EntityUid uid, string? damageModifierSetId, DamageableComponent? comp = null)
    {
        if (!_damageableQuery.Resolve(uid, ref comp))
            return;

        comp.DamageModifierSetId = damageModifierSetId;
        Dirty(uid, comp);
    }
}

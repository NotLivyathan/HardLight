using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<DamageableComponent, ComponentInit>(DamageableInit);
        SubscribeLocalEvent<DamageableComponent, ComponentHandleState>(DamageableHandleState);
        SubscribeLocalEvent<DamageableComponent, ComponentGetState>(DamageableGetState);
        SubscribeLocalEvent<DamageableComponent, OnIrradiatedEvent>(OnIrradiated);
        SubscribeLocalEvent<DamageableComponent, RejuvenateEvent>(OnRejuvenate);

        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _mindContainerQuery = GetEntityQuery<MindContainerComponent>();

        // Damage modifier CVars are updated and stored here to be queried in other systems.
        // Note that certain modifiers requires reloading the guidebook.
        Subs.CVar(
            _config,
            CCVars.PlaytestAllDamageModifier,
            value =>
            {
                UniversalAllDamageModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestAllHealModifier,
            value =>
            {
                UniversalAllHealModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestProjectileDamageModifier,
            value => UniversalProjectileDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestMeleeDamageModifier,
            value => UniversalMeleeDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestProjectileDamageModifier,
            value => UniversalProjectileDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestHitscanDamageModifier,
            value => UniversalHitscanDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestReagentDamageModifier,
            value =>
            {
                UniversalReagentDamageModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestReagentHealModifier,
            value =>
            {
                UniversalReagentHealModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestExplosionDamageModifier,
            value => UniversalExplosionDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestThrownDamageModifier,
            value => UniversalThrownDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestTopicalsHealModifier,
            value => UniversalTopicalsHealModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestMobDamageModifier,
            value => UniversalMobDamageModifier = value,
            true
        );
    }

    /// <summary>
    ///     Initialize a damageable component
    /// </summary>
    private void DamageableInit(EntityUid uid, DamageableComponent component, ComponentInit _)
    {
        if (component.DamageContainerID != null &&
            _prototypeManager.TryIndex<DamageContainerPrototype>(component.DamageContainerID,
                out var damageContainerPrototype))
        {
            // Initialize damage dictionary, using the types and groups from the damage
            // container prototype
            foreach (var type in damageContainerPrototype.SupportedTypes)
            {
                component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
            }

            foreach (var groupId in damageContainerPrototype.SupportedGroups)
            {
                var group = _prototypeManager.Index<DamageGroupPrototype>(groupId);
                foreach (var type in group.DamageTypes)
                {
                    component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
                }
            }
        }
        else
        {
            // No DamageContainerPrototype was given. So we will allow the container to support all damage types
            foreach (var type in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
            {
                component.Damage.DamageDict.TryAdd(type.ID, FixedPoint2.Zero);
            }
        }

        component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
        component.TotalDamage = component.Damage.GetTotal();
    }

    private void DamageableGetState(EntityUid uid, DamageableComponent component, ref ComponentGetState args)
    {
        if (_netMan.IsServer)
        {
            args.State = new DamageableComponentState(
                component.Damage.DamageDict,
                component.DamageContainerID,
                component.DamageModifierSetId,
                component.HealthBarThreshold
            );
        }
        else
        {
            // avoid mispredicting damage on newly spawned entities.
            args.State = new DamageableComponentState(
                component.Damage.DamageDict.ShallowClone(),
                component.DamageContainerID,
                component.DamageModifierSetId,
                component.HealthBarThreshold
            );
        }
    }

    private void OnIrradiated(EntityUid uid, DamageableComponent component, OnIrradiatedEvent args)
    {
        var damageValue = FixedPoint2.New(args.TotalRads);

        // Radiation should really just be a damage group instead of a list of types.
        DamageSpecifier damage = new();
        foreach (var typeId in component.RadiationDamageTypeIDs)
        {
            damage.DamageDict.Add(typeId, damageValue);
        }

        TryChangeDamage(uid, damage, interruptsDoAfters: false, origin: args.Origin);
    }

    private void OnRejuvenate(EntityUid uid, DamageableComponent component, RejuvenateEvent args)
    {
        TryComp<MobThresholdsComponent>(uid, out var thresholds);
        _mobThreshold.SetAllowRevives(uid, true, thresholds); // do this so that the state changes when we set the damage
        SetAllDamage(uid, component, 0);
        _mobThreshold.SetAllowRevives(uid, false, thresholds);
    }

    private void DamageableHandleState(EntityUid uid, DamageableComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not DamageableComponentState state)
        {
            return;
        }

        component.DamageContainerID = state.DamageContainerId;
        component.DamageModifierSetId = state.ModifierSetId;
        component.HealthBarThreshold = state.HealthBarThreshold;

        // Has the damage actually changed?
        DamageSpecifier newDamage = new() { DamageDict = new(state.DamageDict) };
        var delta = newDamage - component.Damage;
        delta.TrimZeros();

        if (!delta.Empty)
        {
            component.Damage = newDamage;
            DamageChanged(uid, component, delta);
        }
    }
}

/// <summary>
///     Raised before damage is done, so stuff can cancel it if necessary.
/// </summary>
[ByRefEvent]
public record struct BeforeDamageChangedEvent(
    DamageSpecifier Damage,
    EntityUid? Origin = null,
    TargetBodyPart? TargetPart = null,
    bool Cancelled = false);

/// <summary>
///     Shitmed Change: Raised on parts before damage is done so we can cancel the damage if they evade.
/// </summary>
[ByRefEvent]
public record struct TryChangePartDamageEvent(
    DamageSpecifier Damage,
    EntityUid? Origin = null,
    TargetBodyPart? TargetPart = null,
    bool IgnoreResistances = false,
    bool CanSever = true,
    bool CanEvade = false,
    float PartMultiplier = 1.00f,
    bool Evaded = false,
    bool Cancelled = false);

/// <summary>
///     Raised on an entity when damage is about to be dealt,
///     in case anything else needs to modify it other than the base
///     damageable component.
///
///     For example, armor.
/// </summary>
public sealed class DamageModifyEvent : EntityEventArgs, IInventoryRelayEvent
{
    // Whenever locational damage is a thing, this should just check only that bit of armour.
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

    public readonly DamageSpecifier OriginalDamage;
    public DamageSpecifier Damage;
    public EntityUid? Origin;
    public readonly TargetBodyPart? TargetPart;
    public readonly float ArmorPenetration = 0;
    public EntityUid? Tool;

    public DamageModifyEvent(
        DamageSpecifier damage,
        EntityUid? origin = null,
        float armorPenetration = 0,
        TargetBodyPart? targetPart = null,
        EntityUid? tool = null)
    {
        OriginalDamage = damage;
        Damage = damage;
        Origin = origin;
        TargetPart = targetPart;
        ArmorPenetration = armorPenetration;
        Tool = tool;
    }
}

public sealed class DamageChangedEvent : EntityEventArgs
{
    /// <summary>
    ///     This is the component whose damage was changed.
    /// </summary>
    /// <remarks>
    ///     Given that nearly every component that cares about a change in the damage, needs to know the
    ///     current damage values, directly passing this information prevents a lot of duplicate
    ///     Owner.TryGetComponent() calls.
    /// </remarks>
    public readonly DamageableComponent Damageable;

    /// <summary>
    ///     The amount by which the damage has changed. If the damage was set directly to some number, this will be
    ///     null.
    /// </summary>
    public readonly DamageSpecifier? DamageDelta;

    /// <summary>
    ///     Was any of the damage change dealing damage, or was it all healing?
    /// </summary>
    public readonly bool DamageIncreased;

    /// <summary>
    ///     Does this event interrupt DoAfters?
    ///     Note: As provided in the constructor, this *does not* account for DamageIncreased.
    ///     As written into the event, this *does* account for DamageIncreased.
    /// </summary>
    public readonly bool InterruptsDoAfters;

    /// <summary>
    ///     Contains the entity which caused the change in damage, if any was responsible.
    /// </summary>
    public readonly EntityUid? Origin;

    /// <summary>
    ///     Shitmed: Can this damage event sever parts?
    /// </summary>
    public readonly bool CanSever;

    public DamageChangedEvent(
        DamageableComponent damageable,
        DamageSpecifier? damageDelta,
        bool interruptsDoAfters,
        EntityUid? origin,
        bool canSever = true)
    {
        Damageable = damageable;
        DamageDelta = damageDelta;
        Origin = origin;
        CanSever = canSever;
        if (DamageDelta == null)
            return;

        foreach (var damageChange in DamageDelta.DamageDict.Values)
        {
            if (damageChange > 0)
            {
                DamageIncreased = true;
                break;
            }
        }

        InterruptsDoAfters = interruptsDoAfters && DamageIncreased;
    }
}

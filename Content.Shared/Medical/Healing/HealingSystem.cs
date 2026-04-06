using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared._FarHorizons.Medical.ConditionalHealing; // Far Horizons
using Content.Shared.Eye.Blinding.Components; // Far Horizons
using Content.Shared.Eye.Blinding.Systems; // Far Horizons
using Robust.Shared.Audio.Systems;
// Shitmed Change
using Content.Shared._Shitmed.Targeting; // Shitmed
using System.Linq;

namespace Content.Shared.Medical.Healing;

public sealed class HealingSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!; // Shitmed
    [Dependency] private readonly ConditionalHealingSystem _conditionalHealing = default!; // Far Horizons
    [Dependency] private readonly BlindableSystem _blindable = default!; // Far Horizons

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {

        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp(args.Used, out HealingComponent? healing))
        {
            // Far Horizons: Handle fake components from conditional healing
            if (args.Used is null || _conditionalHealing.SelectBestMatch(args.Used.Value, target.Owner) is not ConditionalHealingData healingData)
                return;
            healing = ConditionalHealingSystem.MakeComponent(healingData);
        }

        if (healing.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !healing.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            // Far Horizons: Handle fake components from conditional healing
            if (args.Used is null || _conditionalHealing.SelectBestMatch(args.Used.Value, target.Owner) is not ConditionalHealingData fallbackData)
                return;
            healing = ConditionalHealingSystem.MakeComponent(fallbackData);
        }

        TryComp<BloodstreamComponent>(target, out var bloodstream);

        // Heal some bloodloss damage.
        if (healing.BloodlossModifier != 0 && bloodstream != null)
        {
            var isBleeding = bloodstream.BleedAmount > 0;
            _bloodstreamSystem.TryModifyBleedAmount((target.Owner, bloodstream), healing.BloodlossModifier);
            if (isBleeding != bloodstream.BleedAmount > 0)
            {
                var popup = (args.User == target.Owner)
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                _popupSystem.PopupClient(popup, target, args.User);
            }
        }

        // Restores missing blood
        if (healing.ModifyBloodLevel != 0 && bloodstream != null)
            _bloodstreamSystem.TryModifyBloodLevel((target.Owner, bloodstream), healing.ModifyBloodLevel);

        // HardLight start
        // Determines if the entity is a Synth and scales damage recovery accordingly.
        var damageToApply = healing.Damage;
        if (TryComp<HLSynthComponent>(target.Owner, out _))
        {
            damageToApply = ScaleDamageSpecifier(healing.Damage, 0.5f);
        }

        var healed = _damageable.TryChangeDamage(target.Owner, damageToApply, true, origin: args.User, canSever: false); // Shitmed

        // HardLight end

        if (healed == null && healing.BloodlossModifier != 0)
            return;

        var total = healed?.GetTotal() ?? FixedPoint2.Zero;

        // Re-verify that we can heal the damage.
        var dontRepeat = false;
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.Use(args.Used.Value, 1, stackComp);

            if (_stacks.GetCount(args.Used.Value, stackComp) <= 0)
                dontRepeat = true;
        }
        else
        {
            PredictedQueueDel(args.Used.Value);
        }

        if (target.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{EntityManager.ToPrettyString(args.User):user} healed {EntityManager.ToPrettyString(target.Owner):target} for {total:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{EntityManager.ToPrettyString(args.User):user} healed themselves for {total:damage} damage");
        }

        _audio.PlayPredicted(healing.HealingEndSound, target.Owner, args.User);

        // Logic to determine the whether or not to repeat the healing action
        args.Repeat = HasDamage((args.Used.Value, healing), target) && !dontRepeat || IsPartDamaged(args.User, target.Owner); // Shitmed
        if (!args.Repeat && !dontRepeat)
            _popupSystem.PopupClient(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
        args.Handled = true;
    }

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target)
    {
        var damageableDict = target.Comp.Damage.DamageDict;
        var healingDict = healing.Comp.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict.TryGetValue(type.Key, out var damage) && damage.Value > 0)
            {
                return true;
            }
        }

        return false;
    }

    // HardLight start
    private DamageSpecifier ScaleDamageSpecifier(DamageSpecifier spec, float scale)
    {
        var scaled = new DamageSpecifier();
        foreach (var kvp in spec.DamageDict)
        {
            scaled.DamageDict[kvp.Key] = kvp.Value * scale;
        }
        return scaled;
    }

    // HardLight end

    // Shitmed start
    private bool IsPartDamaged(EntityUid user, EntityUid target)
    {
        if (!TryComp(user, out TargetingComponent? targeting))
            return false;

        var (targetType, targetSymmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
        foreach (var part in _bodySystem.GetBodyChildrenOfType(target, targetType, symmetry: targetSymmetry))
            if (TryComp<DamageableComponent>(part.Id, out var damageable)
                && damageable.TotalDamage > part.Component.MinIntegrity)
                return true;

        return false;
    }

    // Shitmed end

    private void OnHealingUse(Entity<HealingComponent> healing, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHeal(healing, args.User, args.User))
            args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> healing, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryHeal(healing, args.Target.Value, args.User))
            args.Handled = true;
    }

    public bool TryHeal(Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user) // Far Horizons: private<public
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (healing.Comp.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !healing.Comp.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            return false;
        }

        if (user != target.Owner && !_interactionSystem.InRangeUnobstructed(user, target.Owner, popup: true))
            return false;

        if (TryComp<StackComponent>(healing, out var stack) && stack.Count < 1)
            return false;

        var resolvedTarget = (target.Owner, target.Comp!);

        var anythingToDo =
            HasDamage(healing, resolvedTarget) ||
            IsPartDamaged(user, target.Owner) || // Shitmed
            healing.Comp.ModifyBloodLevel > 0 // Special case if healing item can restore lost blood...
                && TryComp<BloodstreamComponent>(target, out var bloodstream)
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && bloodSolution.Volume < bloodSolution.MaxVolume; // ...and there is lost blood to restore.

        if (!anythingToDo)
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-cant-use", ("item", healing.Owner)), healing, user);
            return false;
        }

        _audio.PlayPredicted(healing.Comp.HealingBeginSound, healing, user);

        var isNotSelf = user != target.Owner;

        if (isNotSelf)
        {
            var msg = Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", healing.Owner));
            _popupSystem.PopupEntity(msg, target, target, PopupType.Medium);
        }

        var delay = isNotSelf
            ? healing.Comp.Delay
            : healing.Comp.Delay * GetScaledHealingPenalty(healing);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, new HealingDoAfterEvent(), target, target: target, used: healing)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    /// <summary>
    /// Scales the self-heal penalty based on the amount of damage taken
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public float GetScaledHealingPenalty(Entity<HealingComponent> healing)
    {
        var output = healing.Comp.Delay;
        if (!TryComp<MobThresholdsComponent>(healing, out var mobThreshold) ||
            !TryComp<DamageableComponent>(healing, out var damageable))
            return output;
        if (!_mobThresholdSystem.TryGetThresholdForState(healing, MobState.Critical, out var amount, mobThreshold))
            return 1;

        var percentDamage = (float)(damageable.TotalDamage / amount);
        //basically make it scale from 1 to the multiplier.
        var modifier = percentDamage * (healing.Comp.SelfHealPenaltyMultiplier - 1) + 1;
        return Math.Max(modifier, 1);
    }
}

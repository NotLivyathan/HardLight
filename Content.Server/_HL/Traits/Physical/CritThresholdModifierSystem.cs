using Content.Shared._HL.Traits.Physical;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._HL.Traits.Physical;

/// <summary>
/// Applies flat Critical threshold changes and handles threshold re-initialization.
/// </summary>
public sealed class CritThresholdModifierSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CritThresholdModifierComponent, ComponentStartup>(OnCritModifierStartup);
        SubscribeLocalEvent<CritThresholdModifierComponent, ComponentShutdown>(OnCritModifierShutdown);
        SubscribeLocalEvent<GlassJawComponent, ComponentStartup>(OnGlassJawStartup);
        SubscribeLocalEvent<GlassJawComponent, ComponentShutdown>(OnGlassJawShutdown);
        SubscribeLocalEvent<TenacityComponent, ComponentStartup>(OnTenacityStartup);
        SubscribeLocalEvent<TenacityComponent, ComponentShutdown>(OnTenacityShutdown);
        SubscribeLocalEvent<OsteogenesisImperfectaComponent, ComponentStartup>(OnOsteogenesisStartup);
        SubscribeLocalEvent<OsteogenesisImperfectaComponent, ComponentShutdown>(OnOsteogenesisShutdown);
        SubscribeLocalEvent<MobThresholdsComponent, ComponentInit>(OnMobThresholdsInit);
    }

    private void OnCritModifierStartup(Entity<CritThresholdModifierComponent> ent, ref ComponentStartup args)
    {
        AdjustCritThreshold(ent.Owner, ent.Comp.CritThresholdDelta);
    }

    private void OnCritModifierShutdown(Entity<CritThresholdModifierComponent> ent, ref ComponentShutdown args)
    {
        AdjustCritThreshold(ent.Owner, -ent.Comp.CritThresholdDelta);
    }

    private void OnGlassJawStartup(Entity<GlassJawComponent> ent, ref ComponentStartup args)
    {
        AdjustCritThreshold(ent.Owner, -ent.Comp.CritDecrease);
    }

    private void OnGlassJawShutdown(Entity<GlassJawComponent> ent, ref ComponentShutdown args)
    {
        AdjustCritThreshold(ent.Owner, ent.Comp.CritDecrease);
    }

    private void OnTenacityStartup(Entity<TenacityComponent> ent, ref ComponentStartup args)
    {
        AdjustCritThreshold(ent.Owner, ent.Comp.CritIncrease);
    }

    private void OnTenacityShutdown(Entity<TenacityComponent> ent, ref ComponentShutdown args)
    {
        AdjustCritThreshold(ent.Owner, -ent.Comp.CritIncrease);
    }

    private void OnOsteogenesisStartup(Entity<OsteogenesisImperfectaComponent> ent, ref ComponentStartup args)
    {
        AdjustCritThreshold(ent.Owner, -ent.Comp.CritDecrease);
    }

    private void OnOsteogenesisShutdown(Entity<OsteogenesisImperfectaComponent> ent, ref ComponentShutdown args)
    {
        AdjustCritThreshold(ent.Owner, ent.Comp.CritDecrease);
    }

    private void OnMobThresholdsInit(EntityUid uid, MobThresholdsComponent comp, ComponentInit args)
    {
        if (TryComp<CritThresholdModifierComponent>(uid, out var modifier))
            AdjustCritThreshold(uid, modifier.CritThresholdDelta, comp);

        if (TryComp<GlassJawComponent>(uid, out var glassJaw))
            AdjustCritThreshold(uid, -glassJaw.CritDecrease, comp);

        if (TryComp<TenacityComponent>(uid, out var tenacity))
            AdjustCritThreshold(uid, tenacity.CritIncrease, comp);

        if (TryComp<OsteogenesisImperfectaComponent>(uid, out var osteogenesis))
            AdjustCritThreshold(uid, -osteogenesis.CritDecrease, comp);
    }

    private void AdjustCritThreshold(EntityUid uid, int deltaPoints, MobThresholdsComponent? thresholdsComp = null)
    {
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out var current, thresholdsComp))
            return;

        var newValue = FixedPoint2.Max(0, current.Value + (FixedPoint2)deltaPoints);
        _mobThresholds.SetMobStateThreshold(uid, newValue, MobState.Critical, thresholdsComp);
    }
}

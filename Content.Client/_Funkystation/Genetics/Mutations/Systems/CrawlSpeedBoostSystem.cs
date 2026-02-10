using Content.Shared._DV.Abilities;
using Content.Shared._Funkystation.Genetics.Mutations.Components;
using Content.Shared._Funkystation.Genetics.Mutations.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;

namespace Content.Client._Funkystation.Genetics.Systems;

public sealed class CrawlSpeedBoostSystem : SharedCrawlSpeedBoostSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrawlSpeedBoostComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(EntityUid uid, CrawlSpeedBoostComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<CrawlerComponent>(uid, out var crawler) ||
            !TryComp<StandingStateComponent>(uid, out var standing) ||
            standing.Standing)
            return;

        var original = crawler.SpeedModifier;
        if (original <= 0f)
            return;
        float boost = comp.TargetSpeedMult / original;

        args.ModifySpeed(boost, boost);
    }
}

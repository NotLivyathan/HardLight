using Content.Server.Traits.Assorted;
using Content.Shared.Stunnable;

namespace Content.Shared.Traits.Assorted.Systems;

public sealed class LayingDownModifierSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LayingDownModifierComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, LayingDownModifierComponent component, ComponentStartup args)
    {
        _stun.TryModifyCrawler(uid, component.LayingDownCooldownMultiplier, component.DownedSpeedMultiplierMultiplier);
    }
}

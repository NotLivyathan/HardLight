using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Drugs;

/// <summary>
///     System to handle drug related overlays.
/// </summary>
public sealed class DrugOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private RainbowOverlay _rainbowOverlay = default!;
    private AbyssalOverlay _abyssalOverlay = default!; // HardLight

    public static string AbyssalKey = "AbyssalWhispers"; // HardLight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);

        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);

        SubscribeLocalEvent<AbyssalWhispersComponent, ComponentInit>(OnAbyssalInit); // HardLight
        SubscribeLocalEvent<AbyssalWhispersComponent, ComponentShutdown>(OnAbyssalShutdown); // HardLight
        SubscribeLocalEvent<AbyssalWhispersComponent, LocalPlayerAttachedEvent>(OnAbyssalPlayerAttached); // HardLight
        SubscribeLocalEvent<AbyssalWhispersComponent, LocalPlayerDetachedEvent>(OnAbyssalPlayerDetached); // HardLight

        _rainbowOverlay = new(); // HardLight: _overlay<_rainbowOverlay
        _abyssalOverlay = new(); // HardLight
    }

    private void OnRemoved(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _rainbowOverlay.Intoxication = 0;
        _rainbowOverlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_rainbowOverlay); // HardLight: _overlay<_rainbowOverlay
    }

    private void OnApplied(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.Phase = _random.NextFloat(MathF.Tau); // random starting phase for movement effect
        _overlayMan.AddOverlay(_rainbowOverlay); // HardLight: _overlay<_rainbowOverlay
    }

    private void OnPlayerAttached(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_rainbowOverlay); // HardLight: _overlay<_rainbowOverlay
    }

    private void OnPlayerDetached(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        _overlay.Intoxication = 0;
        _overlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_rainbowOverlay); // HardLight: _overlay<_rainbowOverlay
    }

    // HardLight: Abyssal overlay events
    private void OnAbyssalPlayerAttached(EntityUid uid, AbyssalWhispersComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_abyssalOverlay);
    }

    private void OnAbyssalPlayerDetached(EntityUid uid, AbyssalWhispersComponent component, LocalPlayerDetachedEvent args)
    {
        _abyssalOverlay.Intoxication = 0;
        _abyssalOverlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_abyssalOverlay);
    }

    private void OnAbyssalInit(EntityUid uid, AbyssalWhispersComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_abyssalOverlay);
    }

    private void OnAbyssalShutdown(EntityUid uid, AbyssalWhispersComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _abyssalOverlay.Intoxication = 0;
            _abyssalOverlay.TimeTicker = 0;
            _overlayMan.RemoveOverlay(_abyssalOverlay);
        }
    }
}

using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.Drugs;

/// <summary>
///     System to handle drug related overlays.
/// </summary>
public sealed class DrugOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private RainbowOverlay _rainbowOverlay = default!;
    private AbyssalOverlay _abyssalOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectAppliedEvent>(OnRainbowApplied);
        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRemovedEvent>(OnRainbowRemoved);

        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnRainbowPlayerAttached);
        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnRainbowPlayerDetached);

        SubscribeLocalEvent<AbyssalWhispersStatusEffectComponent, StatusEffectAppliedEvent>(OnAbyssalApplied); // HardLight
        SubscribeLocalEvent<AbyssalWhispersStatusEffectComponent, StatusEffectRemovedEvent>(OnAbyssalRemoved); // HardLight

        SubscribeLocalEvent<AbyssalWhispersStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnAbyssalPlayerAttached); // HardLight
        SubscribeLocalEvent<AbyssalWhispersStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnAbyssalPlayerDetached); // HardLight

        _rainbowOverlay = new();
        _abyssalOverlay = new();
    }

    // HardLight start: Rainbow and abyssal overlay separation.
    // Rainbow overlay events
    private void OnRainbowRemoved(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _rainbowOverlay.Intoxication = 0;
        _rainbowOverlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_rainbowOverlay);
    }

    private void OnRainbowApplied(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _rainbowOverlay.Phase = _random.NextFloat(MathF.Tau); // random starting phase for movement effect
        _overlayMan.AddOverlay(_rainbowOverlay);
    }

    private void OnRainbowPlayerAttached(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_rainbowOverlay);
    }

    private void OnRainbowPlayerDetached(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        _rainbowOverlay.Intoxication = 0;
        _rainbowOverlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_rainbowOverlay);
    }

    // Abyssal overlay events
    private void OnAbyssalRemoved(Entity<AbyssalWhispersStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _abyssalOverlay.Intoxication = 0;
        _abyssalOverlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_abyssalOverlay);
    }
    private void OnAbyssalApplied(Entity<AbyssalWhispersStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity == args.Target)
            _overlayMan.AddOverlay(_abyssalOverlay);
    }

    private void OnAbyssalPlayerAttached(Entity<AbyssalWhispersStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_abyssalOverlay);
    }

    private void OnAbyssalPlayerDetached(Entity<AbyssalWhispersStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        _abyssalOverlay.Intoxication = 0;
        _abyssalOverlay.TimeTicker = 0;
        _overlayMan.RemoveOverlay(_abyssalOverlay);
    }
    // HardLight end
}

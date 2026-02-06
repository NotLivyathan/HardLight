using Content.Shared.Eye.Blinding.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Content.Shared.Flash.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Inventory.Events;

namespace Content.Client._Starlight.Overlay.Night;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    private NightVisionOverlay _overlay = default!;
    private EntityUid? _effect = null;
    private readonly EntProtoId _effectPrototype = "EffectNightVision";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, ComponentInit>(OnVisionInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentShutdown>(OnVisionShutdown);

        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<NightVisionComponent, FlashImmunityChangedEvent>(OnFlashImmunityChanged);

        _overlay = new();
    }

    private void OnFlashImmunityChanged(Entity<NightVisionComponent> ent, ref FlashImmunityChangedEvent args)
    {
        if (args.IsImmune)
        {
            ent.Comp.blockedByFlashImmunity = true;
            RemoveNightVision();
        }
        else
        {
            ent.Comp.blockedByFlashImmunity = false;
            AddNightVision(ent.Owner);
        }
    }

    private void OnPlayerAttached(Entity<NightVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        if (_effect == null && !ent.Comp.blockedByFlashImmunity)
            AddNightVision(ent.Owner);
    }

    private void OnPlayerDetached(Entity<NightVisionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveNightVision();
    }

    private void OnVisionInit(Entity<NightVisionComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity != ent.Owner) return;

        if (_effect == null)
            AddNightVision(ent.Owner);
    }

    private void OnVisionShutdown(Entity<NightVisionComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity != ent.Owner) return;
        RemoveNightVision();
    }

    private void AddNightVision(EntityUid uid)
    {
        _overlayMan.AddOverlay(_overlay);
        _effect = SpawnAttachedTo(_effectPrototype, Transform(uid).Coordinates);
        _xformSys.SetParent(_effect.Value, uid);
    }

    private void RemoveNightVision()
    {
        _overlayMan.RemoveOverlay(_overlay);
        Del(_effect);
        _effect = null;
    }
}

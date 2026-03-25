using Content.Shared.Movement.Components;

namespace Content.Shared._HL.Traits.Physical.Systems;

/// <summary>
/// Applies trait-based zoom multipliers to ContentEye settings.
/// </summary>
public sealed class SharedTraitZoomModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitZoomModifierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TraitZoomModifierComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ContentEyeComponent, ComponentInit>(OnContentEyeInit);
    }

    private void OnStartup(Entity<TraitZoomModifierComponent> ent, ref ComponentStartup args)
    {
        TryApply(ent.Owner, ent.Comp);
    }

    private void OnContentEyeInit(Entity<ContentEyeComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<TraitZoomModifierComponent>(ent, out var zoom))
            return;

        TryApply(ent.Owner, zoom, ent.Comp);
    }

    private void OnShutdown(Entity<TraitZoomModifierComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.Applied)
            return;

        if (!TryComp<ContentEyeComponent>(ent, out var eye))
            return;

        if (ent.Comp.Multiplier <= 0f)
            return;

        var factor = 1f / ent.Comp.Multiplier;
        eye.MaxZoom *= factor;
        eye.TargetZoom *= factor;
        ent.Comp.Applied = false;
        Dirty(ent.Owner, eye);
    }

    private void TryApply(EntityUid uid, TraitZoomModifierComponent zoom, ContentEyeComponent? eye = null)
    {
        if (zoom.Applied || zoom.Multiplier <= 0f)
            return;

        if (!Resolve(uid, ref eye, false))
            return;

        eye.MaxZoom *= zoom.Multiplier;
        eye.TargetZoom *= zoom.Multiplier;
        zoom.Applied = true;
        Dirty(uid, eye);
    }
}

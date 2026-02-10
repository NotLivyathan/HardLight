using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Popups;
using Content.Shared._HL.Body;
using Content.Server.Body.Systems;

namespace Content.Server._HL.Body;

public sealed class BionicPartReplacerSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BionicPartReplacerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<BionicPartReplacerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var target = args.Target;
        if (target is null || !TryComp<BodyComponent>(target.Value, out var body))
            return;

        var comp = ent.Comp;
        var ok = _bodySystem.ReplaceOrInsertBodyPart(target.Value,
            comp.TargetType,
            comp.Symmetry,
            comp.ReplacementProto,
            comp.ReplaceIfPresent,
            body);

        if (ok)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("replacer-success"), target.Value, args.User);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("replacer-fail"), target.Value, args.User);
        }
    }
}

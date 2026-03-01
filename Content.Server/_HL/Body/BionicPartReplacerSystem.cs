using Content.Shared.Interaction.Events;
using Content.Shared.Body;
using Content.Shared.Popups;
using Content.Shared._HL.Body;
using Robust.Shared.Containers;

namespace Content.Server._HL.Body;

public sealed class BionicPartReplacerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
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
        var ok = TryReplaceOrgan((target.Value, body), comp.TargetCategory, comp.ReplacementProto, comp.ReplaceIfPresent);

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

    private bool TryReplaceOrgan(Entity<BodyComponent> body,
        ProtoId<OrganCategoryPrototype> targetCategory,
        EntProtoId replacementProto,
        bool replaceIfPresent)
    {
        if (body.Comp.Organs == null)
            return false;

        EntityUid? existing = null;

        foreach (var organ in body.Comp.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            if (organComp.Category == targetCategory)
            {
                existing = organ;
                break;
            }
        }

        if (existing != null && !replaceIfPresent)
            return false;

        if (existing != null)
        {
            _container.Remove(existing.Value, body.Comp.Organs);
            QueueDel(existing.Value);
        }

        var replacement = Spawn(replacementProto, Transform(body.Owner).Coordinates);

        if (!_container.Insert(replacement, body.Comp.Organs))
        {
            QueueDel(replacement);
            return false;
        }

        return true;
    }
}

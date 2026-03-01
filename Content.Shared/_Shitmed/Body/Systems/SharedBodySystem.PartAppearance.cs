using System.Diagnostics;
using System.Linq;
using Content.Shared.Body;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Body.Systems;
public partial class SharedBodySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private void InitializePartAppearances()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentStartup>(OnPartAppearanceStartup);
        SubscribeLocalEvent<BodyPartAppearanceComponent, AfterAutoHandleStateEvent>(HandleState);
        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnPartAttachedToBody);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnPartDroppedFromBody);
    }

    private void OnPartAppearanceStartup(EntityUid uid, BodyPartAppearanceComponent component, ComponentStartup args)
    {
        if (!TryComp(uid, out OrganComponent? organ))
            return;

        var relevantLayer = component.Type;

        if (organ.Body is not { } body
            || !TryComp(body, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var customLayers = bodyAppearance.CustomBaseLayers;
        var spriteLayers = bodyAppearance.BaseLayers;
        component.Type = relevantLayer;

        if (customLayers.ContainsKey(component.Type))
        {
            component.ID = customLayers[component.Type].Id;
            component.Color = customLayers[component.Type].Color;
        }
        else if (spriteLayers.ContainsKey(component.Type))
        {
            component.ID = spriteLayers[component.Type].ID;
            component.Color = bodyAppearance.SkinColor;
        }
        else
        {
            component.ID = CreateIdFromPart(bodyAppearance, relevantLayer);
            component.Color = bodyAppearance.SkinColor;
        }

        // I HATE HARDCODED CHECKS I HATE HARDCODED CHECKS I HATE HARDCODED CHECKS
        if (relevantLayer == HumanoidVisualLayers.Head)
            component.EyeColor = bodyAppearance.EyeColor;

        var markingsByLayer = new Dictionary<HumanoidVisualLayers, List<Marking>>();

        foreach (var layer in HumanoidVisualLayersExtension.Sublayers(relevantLayer))
        {
            var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);
            if (bodyAppearance.MarkingSet.Markings.TryGetValue(category, out var markingList))
                markingsByLayer[layer] = markingList.Select(m => new Marking(m.MarkingId, m.MarkingColors.ToList(), m.IsGlowing)).ToList();
        }

        component.Markings = markingsByLayer;
    }

    private string? CreateIdFromPart(HumanoidAppearanceComponent bodyAppearance, HumanoidVisualLayers part)
    {
        var speciesProto = _prototypeManager.Index(bodyAppearance.Species);
        var baseSprites = _prototypeManager.Index<HumanoidSpeciesBaseSpritesPrototype>(speciesProto.SpriteSet);

        if (!baseSprites.Sprites.ContainsKey(part))
            return null;

        return HumanoidVisualLayersExtension.GetSexMorph(part, bodyAppearance.Sex, baseSprites.Sprites[part]);
    }

    public void ModifyMarkings(EntityUid uid,
        Entity<BodyPartAppearanceComponent?> partAppearance,
        HumanoidAppearanceComponent bodyAppearance,
        HumanoidVisualLayers targetLayer,
        string markingId,
        bool remove = false)
    {
        // Floofstation - DO NOT TOUCH MARKINGS CLIENT-SIDE, YOU ARE DUPLICATING THEM!!!
        if (_net.IsClient && !IsClientSide(uid))
            return;

        if (!Resolve(partAppearance, ref partAppearance.Comp))
            return;

        if (!remove)
        {

            if (!_markingManager.Markings.TryGetValue(markingId, out var prototype))
                return;

            var markingColors = MarkingColoring.GetMarkingLayerColors(
                    prototype,
                    bodyAppearance.SkinColor,
                    bodyAppearance.EyeColor,
                    bodyAppearance.MarkingSet
                );

            var marking = new Marking(markingId, markingColors, true);
            var dirty = false;

            _humanoid.SetLayerVisibility((uid, bodyAppearance), targetLayer, true, null, ref dirty);
            _humanoid.AddMarking(uid, markingId, markingColors, true, true, true, bodyAppearance);
            if (!partAppearance.Comp.Markings.ContainsKey(targetLayer))
                partAppearance.Comp.Markings[targetLayer] = new List<Marking>();

            partAppearance.Comp.Markings[targetLayer].Add(marking);

            if (dirty)
                Dirty(uid, bodyAppearance);
        }
        //else
            //RemovePartMarkings(uid, component, bodyAppearance);
    }

    private void HandleState(EntityUid uid, BodyPartAppearanceComponent component, ref AfterAutoHandleStateEvent args) =>
        ApplyPartMarkings(uid, component);

    private void OnPartAttachedToBody(EntityUid uid, BodyComponent component, ref OrganInsertedIntoEvent args)
    {
        if (!TryComp(args.Organ, out BodyPartAppearanceComponent? partAppearance)
            || !TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        if (partAppearance.ID != null)
            _humanoid.SetBaseLayerId(uid, partAppearance.Type, partAppearance.ID, sync: true, bodyAppearance);

        UpdateAppearance(uid, partAppearance);
    }

    private void OnPartDroppedFromBody(EntityUid uid, BodyComponent component, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(uid)
            || TerminatingOrDeleted(args.Organ)
            || !TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        // We check for this conditional here since some entities may not have a profile... If they dont
        // have one, and their part is gibbed, the markings will not be removed or applied properly.
        if (!HasComp<BodyPartAppearanceComponent>(args.Organ))
            EnsureComp<BodyPartAppearanceComponent>(args.Organ);

        if (TryComp<BodyPartAppearanceComponent>(args.Organ, out var partAppearance))
            RemoveAppearance(uid, partAppearance, args.Organ);
    }

    private void UpdateAppearance(EntityUid target,
        BodyPartAppearanceComponent component)
    {
        // Floofstation - DO NOT TOUCH MARKINGS CLIENT-SIDE, YOU ARE DUPLICATING THEM!!!
        if (_net.IsClient && !IsClientSide(target))
            return;

        if (!TryComp(target, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var dirty = false;

        if (component.EyeColor != null)
        {
            bodyAppearance.EyeColor = component.EyeColor.Value;
            _humanoid.SetLayerVisibility((target, bodyAppearance), HumanoidVisualLayers.Eyes, true, null, ref dirty);
        }

        if (component.Color != null)
            _humanoid.SetBaseLayerColor(target, component.Type, component.Color, true, bodyAppearance);

        _humanoid.SetLayerVisibility((target, bodyAppearance), component.Type, true, null, ref dirty);

        foreach (var (visualLayer, markingList) in component.Markings)
        {
            _humanoid.SetLayerVisibility((target, bodyAppearance), visualLayer, true, null, ref dirty);
            foreach (var marking in markingList)
            {
                _humanoid.AddMarking(target, marking.MarkingId, marking.MarkingColors, true, true, true, bodyAppearance);
            }
        }

        if (dirty)
            Dirty(target, bodyAppearance);
    }

    private void RemoveAppearance(EntityUid entity, BodyPartAppearanceComponent component, EntityUid partEntity)
    {
        if (!TryComp(entity, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var dirty = false;

        foreach (var (visualLayer, markingList) in component.Markings)
        {
            _humanoid.SetLayerVisibility((entity, bodyAppearance), visualLayer, false, null, ref dirty);
            if (dirty)
                Dirty(entity, bodyAppearance);
        }
        RemoveBodyMarkings(entity, component, bodyAppearance);
    }

    private void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component)
    {
        UpdateAppearance(target, component);
    }

    private void RemoveBodyMarkings(EntityUid target, BodyPartAppearanceComponent partAppearance, HumanoidAppearanceComponent bodyAppearance)
    {
        var dirty = false;

        foreach (var (_, markingList) in partAppearance.Markings)
        {
            foreach (var marking in markingList)
            {
                _humanoid.SetMarkingVisibility(target, bodyAppearance, marking.MarkingId, false);
                dirty = true;
            }
        }

        if (dirty)
            Dirty(target, bodyAppearance);
    }
}

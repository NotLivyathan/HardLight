using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.EffectConditions;

public sealed partial class NFIsHumanoid : EntityEffectCondition
{
    [DataField]
    public List<ProtoId<SpeciesPrototype>>? Whitelist = null;

    [DataField]
    public bool Inverse;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs)
            return false;

        if (!args.EntityManager.TryGetComponent<HumanoidAppearanceComponent>(args.TargetEntity, out var humanoidAppearance))
            return false;

        if (Whitelist != null && Whitelist.Contains(humanoidAppearance.Species) != Inverse)
            return false;

        return true;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        if (Whitelist == null || Whitelist.Count == 0)
            return Loc.GetString("reagent-effect-condition-guidebook-species-type-empty");

        var message = Inverse
            ? "reagent-effect-condition-guidebook-species-type-blacklist"
            : "reagent-effect-condition-guidebook-species-type-whitelist";

        var localizedSpecies = Whitelist
            .Select(p => Loc.GetString("reagent-effect-condition-guidebook-species-type-species", ("species", Loc.GetString(prototype.Index(p).Name))))
            .ToList();

        var list = ContentLocalizationManager.FormatListToOr(localizedSpecies);
        return Loc.GetString(message, ("species", list));
    }
}

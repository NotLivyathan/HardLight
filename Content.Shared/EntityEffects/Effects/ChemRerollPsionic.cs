using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class ChemRerollPsionic : EventEntityEffect<ChemRerollPsionic>
{
    [DataField("bonusMultiplier")]
    public float BonusMuliplier = 1f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-reroll-psionic", ("chance", Probability));
}

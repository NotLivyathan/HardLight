using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class MutationRemoval : EventEntityEffect<MutationRemoval>
{
    [DataField]
    public float Chance = 1.0f;

    [DataField]
    public int MinRemovals = 1;

    [DataField]
    public int MaxRemovals = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-mutation-removal", ("chance", Probability));
}

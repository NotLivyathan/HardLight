using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class RandomMutation : EventEntityEffect<RandomMutation>
{
    [DataField]
    public float Chance = 1.0f;

    [DataField]
    public int MinMutations = 1;

    [DataField]
    public int MaxMutations = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-mutation", ("chance", Probability));
}

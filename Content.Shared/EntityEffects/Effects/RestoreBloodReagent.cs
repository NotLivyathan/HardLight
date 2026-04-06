using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class RestoreBloodReagent : EntityEffect
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<ReagentPrototype>))]
    public string BloodReagent = "Blood";

    public override void Effect(EntityEffectBaseArgs args)
    {
        var bloodstream = IoCManager.Resolve<IEntityManager>().System<SharedBloodstreamSystem>();
        bloodstream.ChangeBloodReagent(args.TargetEntity, BloodReagent);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return null;
    }
}

using Content.Server.Animals.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;

namespace Content.Server.EntityEffects.Effects;

public sealed class InseminationEffectSystem : EntitySystem
{
    [Dependency] private readonly LewdEggLayingSystem _eggLaying = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecuteEntityEffectEvent<Inseminate>>(OnInseminate);
    }

    private void OnInseminate(ref ExecuteEntityEffectEvent<Inseminate> args)
    {
        _eggLaying.Inseminate(args.Args.TargetEntity, args.Effect.Probability);
    }
}

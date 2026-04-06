using Content.Server._Funkystation.Genetics.Components;
using Content.Server._Funkystation.Genetics.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Robust.Shared.Random;

namespace Content.Server.EntityEffects.Effects;

public sealed class GeneticsEffectSystem : EntitySystem
{
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecuteEntityEffectEvent<MutationRemoval>>(OnMutationRemoval);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<RandomMutation>>(OnRandomMutation);
    }

    private void OnMutationRemoval(ref ExecuteEntityEffectEvent<MutationRemoval> args)
    {
        var entity = args.Args.TargetEntity;

        if (!TryComp<GeneticsComponent>(entity, out var genetics))
            return;

        var scale = args.Args is EntityEffectReagentArgs reagentArgs ? reagentArgs.Scale.Float() : 1f;
        var attempts = _random.Next(args.Effect.MinRemovals, args.Effect.MaxRemovals + 1);

        for (var i = 0; i < attempts; i++)
        {
            if (_random.Prob(args.Effect.Chance * scale))
                _genetics.RemoveRandomMutation(entity, genetics, true);
        }
    }

    private void OnRandomMutation(ref ExecuteEntityEffectEvent<RandomMutation> args)
    {
        var entity = args.Args.TargetEntity;

        if (!TryComp<GeneticsComponent>(entity, out var genetics))
            return;

        var scale = args.Args is EntityEffectReagentArgs reagentArgs ? reagentArgs.Scale.Float() : 1f;
        var attempts = _random.Next(args.Effect.MinMutations, args.Effect.MaxMutations + 1);

        for (var i = 0; i < attempts; i++)
        {
            if (_random.Prob(args.Effect.Chance * scale))
                _genetics.TriggerRandomMutation(entity, genetics);
        }
    }
}

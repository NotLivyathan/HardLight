using Content.Server.Abilities.Psionics;
using Content.Server.Psionics;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;

namespace Content.Server.EntityEffects.Effects;

public sealed class PsionicsEffectSystem : EntitySystem
{
    [Dependency] private readonly PsionicsSystem _psionics = default!;
    [Dependency] private readonly PsionicAbilitiesSystem _psionicAbilities = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecuteEntityEffectEvent<ChemRerollPsionic>>(OnChemRerollPsionic);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<ChemRemovePsionic>>(OnChemRemovePsionic);
    }

    private void OnChemRerollPsionic(ref ExecuteEntityEffectEvent<ChemRerollPsionic> args)
    {
        _psionics.RerollPsionics(args.Args.TargetEntity, bonusMuliplier: args.Effect.BonusMuliplier);
    }

    private void OnChemRemovePsionic(ref ExecuteEntityEffectEvent<ChemRemovePsionic> args)
    {
        if (args.Args is EntityEffectReagentArgs reagentArgs && reagentArgs.Scale.Float() != 1f)
            return;

        _psionicAbilities.RemoveAllPsionicPowers(args.Args.TargetEntity);
    }
}

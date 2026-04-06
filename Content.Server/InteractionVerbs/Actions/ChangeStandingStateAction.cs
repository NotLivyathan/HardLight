using Content.Shared.InteractionVerbs;
using Content.Shared.Standing;

namespace Content.Server.InteractionVerbs.Actions;

[Serializable]
public sealed partial class ChangeStandingStateAction : InteractionAction
{
    [DataField]
    public bool MakeStanding = false;

    [DataField]
    public bool MakeLaying = false;

    public override bool CanPerform(InteractionArgs args, InteractionVerbPrototype proto, bool beforeDelay, VerbDependencies deps)
    {
        return true;
    }

    public override bool Perform(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        var standing = deps.EntMan.System<StandingStateSystem>();

        if (MakeStanding)
            return standing.Stand(args.Target, force: true);

        if (MakeLaying)
            return standing.Down(args.Target, force: true);

        return false;
    }
}

using Content.Shared.Body;
using Content.Shared.Examine;
using Content.Shared.Movement.Components; // TODO: use BrainComponent instead of InputMover when shitmed is merged
using Robust.Shared.Utility;

namespace Content.Shared._DV.Traits.Assorted;

/// <summary>
/// Adds a warning examine message to brains with <see cref="UnborgableComponent"/>.
/// </summary>
public sealed class UnborgableSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnborgableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<UnborgableComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Returns true if a mob's brain has <see cref="UnborgableComponent"/>.
    /// </summary>
    public bool IsUnborgable(EntityUid ent)
    {
        // technically this will apply for any organ not just brain, but assume nobody will be evil and do that
        if (!TryComp(ent, out BodyComponent? body))
            return false;

        return _body.TryGetOrgansWithComponent<UnborgableComponent>((ent, body), out _);
    }

    private void OnMapInit(Entity<UnborgableComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        if (!_body.TryGetOrgansWithComponent<InputMoverComponent>((ent.Owner, body), out var brains))
            return;

        foreach (var brain in brains)
            EnsureComp<UnborgableComponent>(brain);
    }

    private void OnExamined(Entity<UnborgableComponent> ent, ref ExaminedEvent args)
    {
        // need a health analyzer to see if someone can't be borged, can't just look at them and know
        if (!args.IsInDetailsRange || HasComp<BodyComponent>(ent))
            return;

        args.PushMarkup(Loc.GetString("brain-cannot-be-borged-message"));
    }
}

using Content.Shared._HL.Traits.Physical;
using Content.Shared.Throwing;

namespace Content.Server._HL.Traits.Physical;

/// <summary>
/// Applies trait-based throw distance multipliers.
/// </summary>
public sealed class ThrowStrengthModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrowStrengthModifierComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    private void OnBeforeThrow(EntityUid uid, ThrowStrengthModifierComponent component, ref BeforeThrowEvent args)
    {
        args.Direction *= component.Multiplier;
    }
}

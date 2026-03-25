namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Strongly decreases the damage threshold required to enter the Critical state.
/// Separate component so it can stack with GlassJaw and other modifiers.
/// </summary>
[RegisterComponent]
public sealed partial class OsteogenesisImperfectaComponent : Component
{
    [DataField]
    public int CritDecrease = 50;
}

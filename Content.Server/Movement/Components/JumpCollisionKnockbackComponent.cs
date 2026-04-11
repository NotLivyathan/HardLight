namespace Content.Server.Movement.Components;

[RegisterComponent]
public sealed partial class JumpCollisionKnockbackComponent : Component
{
    [DataField("throwForce")]
    public float ThrowForce = 10f;
}
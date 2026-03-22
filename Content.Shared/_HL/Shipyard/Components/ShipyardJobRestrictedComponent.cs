namespace Content.Shared._NF.Shipyard.Components;

/// <summary>
/// Marks entities that are restricted from most shipyard console actions.
/// Applied based on job restrictions at spawn.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardJobRestrictedComponent : Component
{
}

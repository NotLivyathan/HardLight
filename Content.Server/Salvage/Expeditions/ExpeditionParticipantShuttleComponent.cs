namespace Content.Server.Salvage.Expeditions;

/// <summary>
/// Marks shuttle grids that actually FTLed into an expedition.
/// This lets expedition cleanup ignore shuttle-themed ruin grids on salvage maps.
/// </summary>
[RegisterComponent]
public sealed partial class ExpeditionParticipantShuttleComponent : Component
{
}
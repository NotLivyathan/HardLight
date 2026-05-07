using Content.Shared.Roles; // HardLight
using Robust.Shared.Map;
using Robust.Shared.Prototypes; // HardLight
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Components;

/// <summary>
/// Spawns Colonial Command (emergency destination) for a station.
/// </summary>
[RegisterComponent]
public sealed partial class StationColcommComponent : Component
{
    /// <summary>
    /// Crude shuttle offset spawning.
    /// </summary>
    [DataField]
    public float ShuttleIndex;

    [DataField]
    public ResPath Map = new("/Maps/colcomm.yml");

    /// <summary>
    /// Colcomm entity that was loaded.
    /// </summary>
    [DataField]
    public EntityUid? Entity;

    [DataField]
    public EntityUid? MapEntity;

    /// <summary>
    /// HardLight: Job registry configuration used to seed
    /// on the persistent ColComm grid entity.
    /// Format: key = job prototype ID, value = [roundStartSlots, midRoundMaxSlots] (negative = unlimited).
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int[]> JobRegistryConfig = new();
}

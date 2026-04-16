using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._HL.ColComm;

/// <summary>
/// Attached to the persistent Colonial Command grid entity.
/// Serves as the server-wide, cross-round authoritative job registry.
/// </summary>
[RegisterComponent]
[Access(typeof(ColcommJobSystem))]
public sealed partial class ColcommJobRegistryComponent : Component
{
    /// <summary>
    /// Master job configuration: key = job prototype ID, value = [roundStartSlots, midRoundMaxSlots].
    /// Negative values mean unlimited (-1).
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int[]> ConfiguredJobs = new();

    /// <summary>
    /// Currently available slot counts. Null = unlimited.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int?> CurrentSlots = new();

    /// <summary>
    /// The configured mid-round maximum for each job.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> MidRoundMaxSlots = new();

    /// <summary>
    /// Tracks which players are occupying which jobs, to prevent double slot consumption on reconnect.
    /// </summary>
    [DataField]
    public Dictionary<NetUserId, HashSet<ProtoId<JobPrototype>>> PlayerJobs = new();
}

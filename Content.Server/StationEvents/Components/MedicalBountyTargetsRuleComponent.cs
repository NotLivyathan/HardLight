using Content.Server.StationEvents.Events;
using Content.Shared.Storage;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(MedicalBountyTargetsRule))]
public sealed partial class MedicalBountyTargetsRuleComponent : Component
{
    [DataField("entries")]
    public List<EntitySpawnEntry> Entries = new();

    [DataField("departmentId")]
    public string DepartmentId = "Medical";

    [DataField("variance")]
    public float Variance = 0.1f;
}

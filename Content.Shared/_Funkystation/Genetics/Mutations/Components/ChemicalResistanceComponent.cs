// HardLight start: Merged with upstream; had to migrate ChemicalResistanceComponent to Content.Shared
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class ChemicalResistanceComponent : Component
{
    [DataField]
    public List<ProtoId<ReagentPrototype>> Reagents { get; private set; } = new();

    [DataField]
    public FixedPoint2 PurgeAmount { get; private set; } = FixedPoint2.New(1);
}
// HardLight end

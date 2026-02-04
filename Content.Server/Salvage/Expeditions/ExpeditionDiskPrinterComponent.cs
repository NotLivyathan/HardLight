using Content.Shared.Salvage.Expeditions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Salvage.Expeditions;

[RegisterComponent]
public sealed partial class ExpeditionDiskPrinterComponent : Component
{
    [DataField("diskPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string DiskPrototype = "ExpeditionCoordinatesDisk";

    [DataField("difficulties")]
    public List<string> Difficulties =
    [
        "NFEasy",
        "NFModerate",
        "NFHazardous",
        "NFExtreme",
        "NFNightmare",
    ];

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
}

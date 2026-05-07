using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CM14.Xenos.Acid;

[Serializable, NetSerializable]
public sealed partial class XenoCorrosiveAcidDoAfterEvent : DoAfterEvent
{
    [DataField]
    public EntProtoId AcidId = "XenoAcid";

    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(30);

    public XenoCorrosiveAcidDoAfterEvent(XenoCorrosiveAcidEvent ev)
    {
        AcidId = ev.AcidId;
        Time = ev.Time;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

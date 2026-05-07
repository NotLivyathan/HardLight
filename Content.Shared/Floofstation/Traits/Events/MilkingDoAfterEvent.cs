using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.FloofStation.Traits.Events;

[Serializable, NetSerializable]
public sealed partial class MilkingDoAfterEvent : SimpleDoAfterEvent
{
}
// Hardlight Start
[Serializable, NetSerializable]
public sealed partial class DrinkMilkDoAfterEvent : SimpleDoAfterEvent
{
}
//Hardlight End

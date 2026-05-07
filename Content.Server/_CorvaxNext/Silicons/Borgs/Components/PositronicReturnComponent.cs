using Robust.Shared.GameStates;

namespace Content.Server._CorvaxNext.Silicons.Borgs.Components;

[RegisterComponent]
public sealed partial class PositronicReturnComponent : Component
{
    public EntityUid? ReturnTarget;
    public EntityUid? ReturnActionEntity;
}

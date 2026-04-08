using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.SurveillanceCamera;

[Serializable, NetSerializable]
public enum SurveillanceCameraVisualsKey : byte
{
    Key,
    Layer
}

[Serializable, NetSerializable]
public enum SurveillanceCameraVisuals : byte
{
    Active,
    InUse,
    Disabled,
    // Reserved for future use
    Xray,
    Emp
}

/// <summary>
/// Raised on a camera entity to find whether it is externally viewed by some entity.
/// This does not use the actual viewers or monitors camera has and is simply used to see whether the camera is "technically"
/// being looked through by somebody, such as the Station AI.
/// </summary>
[ByRefEvent]
public record struct SurveillanceCameraGetIsViewedExternallyEvent(bool Viewed = false);

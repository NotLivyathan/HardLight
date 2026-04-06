using Robust.Shared.Serialization;

namespace Content.Shared.Toggleable;

// Compatibility enum for older prototype visualizer keys.
[Serializable, NetSerializable]
public enum ToggleableVisuals : byte
{
    Enabled,
    Layer
}

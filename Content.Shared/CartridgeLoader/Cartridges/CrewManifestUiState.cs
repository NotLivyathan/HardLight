using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class CrewManifestUiState : BoundUserInterfaceState
{
    public CrewManifestEntries? Entries;
    public string? Message;

    public CrewManifestUiState(CrewManifestEntries? entries, string? message = null)
    {
        Entries = entries;
        Message = message;
    }
}

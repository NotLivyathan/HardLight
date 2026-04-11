using System;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent]
public sealed partial class VinylPlayerComponent : Component
{
    /// <summary>
    /// Should the vinyl player relay to radios around the station, should only be true for the radiostation vinyl player
    /// </summary>
    [DataField]
    public bool RelayToRadios;

    /// <summary>
    /// The sound entity being played
    /// </summary>
    [NonSerialized]
    public EntityUid? SoundEntity;
}

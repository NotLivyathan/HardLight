using Content.Shared.Interaction.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio;

namespace Content.Shared._HL.Traits.Physical.Systems;

/// <summary>
/// Rewrites the inherited humanoid interaction popup so pettable players use the fox/kitsune pet interaction consistently on both sides.
/// </summary>
public sealed class SharedPettableInteractionOverrideSystem : EntitySystem
{
    private static readonly SoundSpecifier PetSound = new SoundPathSpecifier("/Audio/Animals/fox_squeak.ogg");
    [Dependency] private readonly InteractionPopupSystem _interactionPopup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PettableInteractionOverrideComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, PettableInteractionOverrideComponent component, ComponentStartup args)
    {
        var popup = EnsureComp<InteractionPopupComponent>(uid);
        _interactionPopup.ConfigureInteractionPopup(
            (uid, popup),
            0.75f,
            "petting-success-soft-floofy-kitsune",
            "petting-failure-generic",
            PetSound,
            null,
            "EffectHearts",
            null,
            null,
            true);
    }
}

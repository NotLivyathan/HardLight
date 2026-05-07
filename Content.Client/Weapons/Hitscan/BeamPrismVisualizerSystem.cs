using Content.Shared.Weapons.Hitscan;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Weapons.Hitscan;

public sealed class BeamPrismVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier BeamPrismOverlaySprite =
        new SpriteSpecifier.Rsi(new ResPath("_HL/Objects/ShuttleWeapons/beamprism.rsi"), "artillerygem");

    private const string BeamPrismOverlayLayerKey = "BeamPrismFiringOverlay";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeamPrismComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, BeamPrismComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var entSprite = (uid, sprite);
        var layerIndex = _sprite.LayerMapReserve(entSprite, BeamPrismOverlayLayerKey);

        if (_appearance.TryGetData<string>(uid, BeamPrismVisuals.FiringState, out var state, args.Component))
        {
            _sprite.LayerSetSprite(entSprite, layerIndex, BeamPrismOverlaySprite);
            _sprite.LayerSetRsiState(entSprite, layerIndex, state);
            sprite.LayerSetShader(layerIndex, "unshaded");
            _sprite.LayerSetVisible(entSprite, layerIndex, true);
            return;
        }

        if (_sprite.LayerExists(entSprite, layerIndex))
            _sprite.LayerSetVisible(entSprite, layerIndex, false);
    }
}

using System.Linq;
using Content.Client.Silicons.Borgs;
using Content.Shared._CD.Silicons;
using Content.Shared._CD.Silicons.Borgs;
using Content.Shared.CCVar; // HardLight
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration; // HardLight
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._CD.Silicons.Borgs;

/// <summary>
/// Primarily handles the appearance aspects of the borg subtype.
/// </summary>
public sealed class BorgSwitchableSubtypeSystem : SharedBorgSwitchableSubtypeSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly BorgSystem _borg = default!;
    [Dependency] private readonly BorgSwitchableTypeSystem _borgTypeSystem = default!; // HardLight
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!; // HardLight

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.ShowCyborgSubtypeSprites, OnShowCyborgSubtypeSpritesChanged); // HardLight
        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, AfterAutoHandleStateEvent>(OnAutoHandleEvent);
    }

    private void OnShowCyborgSubtypeSpritesChanged(bool _) // HardLight
    {
        var query = EntityQueryEnumerator<BorgSwitchableSubtypeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            SelectBorgSubtype((uid, comp));
        }
    }

    private void OnAutoHandleEvent(Entity<BorgSwitchableSubtypeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SelectBorgSubtype(ent);
    }

    private void OnComponentStartup(Entity<BorgSwitchableSubtypeComponent> ent, ref ComponentStartup args)
    {
        SelectBorgSubtype(ent);
    }

    protected override void UpdateEntityAppearance(Entity<BorgSwitchableSubtypeComponent> entity, BorgSubtypePrototype borgSubtypePrototype)
    {
        // HardLight: Check if player has disabled custom borg sprites
        if (!_cfg.GetCVar(CCVars.ShowCyborgSubtypeSprites))
        {
            if (TryComp<BorgSwitchableTypeComponent>(entity, out var typeComp))
                _borgTypeSystem.RefreshEntityAppearance((entity.Owner, typeComp), ignoreSubtype: true);

            return;
        }

        // LOT of copy pasted code from BorgSwitchableTypeSystem, but is probably necessary unless the upstream code
        // is refactored

        // get our required components
        var (owner, _) = entity;
        if (!TryComp<SpriteComponent>(entity, out var chassisSprite))
            return;

        // remove all existing layers
        for (var i = chassisSprite.AllLayers.Count() - 1; i >= 0; i--) // HardLight: int<var
        {
            _sprite.RemoveLayer((entity, chassisSprite), i);
        }

        for (var i = 0; i < borgSubtypePrototype.LayerData.Length; i++) // HardLight: int<var
        {
            var layerData = borgSubtypePrototype.LayerData[i];

            layerData.RsiPath = borgSubtypePrototype.SpritePath?.ToString();
            if (borgSubtypePrototype.Offset != null)
                layerData.Offset = borgSubtypePrototype.Offset;
            _sprite.AddLayer((owner, chassisSprite), layerData, i);
        }

        if (TryComp<BorgChassisComponent>(entity, out var chassis))
        {
            _borg.SetMindStates(
                (entity.Owner, chassis),
                borgSubtypePrototype.SpriteHasMindState,
                borgSubtypePrototype.SpriteNoMindState);

            if (TryComp(entity, out AppearanceComponent? appearance))
            {
                // Queue update so state changes apply.
                _appearance.QueueUpdate(entity, appearance);
            }
        }

        if (borgSubtypePrototype.SpriteBodyMovementState is { } movementState)
        {
            var spriteMovement = EnsureComp<SpriteMovementComponent>(entity);
            spriteMovement.NoMovementLayers.Clear();
            spriteMovement.NoMovementLayers["movement"] = new PrototypeLayerData
            {
                State = borgSubtypePrototype.SpriteBodyState,
            };
            spriteMovement.MovementLayers.Clear();
            spriteMovement.MovementLayers["movement"] = new PrototypeLayerData
            {
                State = movementState,
            };
        }
        else
        {
            RemComp<SpriteMovementComponent>(entity);
        }

        base.UpdateEntityAppearance(entity, borgSubtypePrototype);
    }
}

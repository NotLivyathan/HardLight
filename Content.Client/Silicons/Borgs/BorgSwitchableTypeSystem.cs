using System.Linq; // HardLight
using Content.Shared._CD.Silicons.Borgs;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// Client side logic for borg type switching. Sets up primarily client-side visual information.
/// </summary>
/// <seealso cref="SharedBorgSwitchableTypeSystem"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
public sealed class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private readonly BorgSystem _borgSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableTypeComponent, AfterAutoHandleStateEvent>(AfterStateHandler);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<BorgSwitchableTypeComponent> ent, ref ComponentStartup args)
    {
        UpdateEntityAppearance(ent);
    }

    private void AfterStateHandler(Entity<BorgSwitchableTypeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateEntityAppearance(ent);
    }

    public void RefreshEntityAppearance(Entity<BorgSwitchableTypeComponent> entity, bool ignoreSubtype = false) // HardLight
    {
        if (!Prototypes.TryIndex(entity.Comp.SelectedBorgType, out var prototype))
            return;

        ApplyEntityAppearance(entity, prototype, ignoreSubtype);
    }

    protected override void UpdateEntityAppearance(
        Entity<BorgSwitchableTypeComponent> entity,
        BorgTypePrototype prototype)
    {
        ApplyEntityAppearance(entity, prototype, false); // HardLight
    }

    private void ApplyEntityAppearance( // HardLight
        Entity<BorgSwitchableTypeComponent> entity,
        BorgTypePrototype prototype,
        bool ignoreSubtype)
    {
        // CD - added checks to stop sprite state errors
        if (!ignoreSubtype && // HardLight
            (!TryComp<BorgSwitchableSubtypeComponent>(entity, out var subtype) || // HardLight
             subtype.BorgSubtype != null))
            return;

        if (TryComp(entity, out SpriteComponent? sprite))
        {
            if (_resourceCache.TryGetResource<RSIResource>(
                    SpriteSpecifierSerializer.TextureRoot / prototype.SpritePath,
                    out var res))
            {
                sprite.BaseRSI = res.RSI;
            }

            if (ignoreSubtype) // HardLight
                ResetBaseLayers((entity, sprite), prototype);

            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.Body, prototype.SpriteBodyState);
            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.LightStatus, prototype.SpriteToggleLightState);
        }

        if (TryComp(entity, out BorgChassisComponent? chassis))
        {
            _borgSystem.SetMindStates(
                (entity.Owner, chassis),
                prototype.SpriteHasMindState,
                prototype.SpriteNoMindState);

            if (TryComp(entity, out AppearanceComponent? appearance))
            {
                // Queue update so state changes apply.
                _appearance.QueueUpdate(entity, appearance);
            }
        }

        if (prototype.SpriteBodyMovementState is { } movementState)
        {
            var spriteMovement = EnsureComp<SpriteMovementComponent>(entity);
            spriteMovement.NoMovementLayers.Clear();
            spriteMovement.NoMovementLayers["movement"] = new PrototypeLayerData
            {
                State = prototype.SpriteBodyState,
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

        base.UpdateEntityAppearance(entity, prototype);
    }

    // HardLight: Rebuild the default chassis layer stack so subtype sprites can revert cleanly at runtime.
    private void ResetBaseLayers(Entity<SpriteComponent?> sprite, BorgTypePrototype prototype)
    {
        var spriteComp = sprite.Comp;
        if (spriteComp == null)
            return;

        for (var i = spriteComp.AllLayers.Count() - 1; i >= 0; i--)
        {
            _sprite.RemoveLayer(sprite, i);
        }

        var body = _sprite.AddRsiLayer(sprite, prototype.SpriteBodyState);
        _sprite.LayerMapSet(sprite, BorgVisualLayers.Body, body);
        _sprite.LayerMapSet(sprite, "movement", body);

        var light = _sprite.AddRsiLayer(sprite, prototype.SpriteNoMindState);
        _sprite.LayerMapSet(sprite, BorgVisualLayers.Light, light);
        spriteComp.LayerSetShader(light, "unshaded");
        _sprite.LayerSetVisible(sprite, light, false);

        var lightStatus = _sprite.AddRsiLayer(sprite, prototype.SpriteToggleLightState);
        _sprite.LayerMapSet(sprite, BorgVisualLayers.LightStatus, lightStatus);
        _sprite.LayerMapSet(sprite, "light", lightStatus);
        spriteComp.LayerSetShader(lightStatus, "unshaded");
        _sprite.LayerSetVisible(sprite, lightStatus, false);
    }
}

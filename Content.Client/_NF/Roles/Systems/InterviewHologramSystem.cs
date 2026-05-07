using System.Linq;
using System.Numerics;
using Content.Server._NF.Roles.Systems;
using Content.Shared._NF.Roles.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._NF.Roles.Systems;

public sealed class InterviewHologramSystem : SharedInterviewHologramSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, HologramVisualState> _visualStates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InterviewHologramComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<InterviewHologramComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<InterviewHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    private void OnComponentStartup(Entity<InterviewHologramComponent> entity, ref ComponentStartup ev)
    {
        UpdateHologramSprite(entity);
    }

    private void OnComponentShutdown(Entity<InterviewHologramComponent> entity, ref ComponentShutdown ev)
    {
        _visualStates.Remove(entity);
    }

    private void OnShaderRender(Entity<InterviewHologramComponent> entity, ref BeforePostShaderRenderEvent ev)
    {
        UpdateHologramSprite(entity, ev.Sprite);
    }

    private void UpdateHologramSprite(EntityUid hologram, SpriteComponent? sprite = null)
    {
        // Get required components
        if (!Resolve(hologram, ref sprite, false) ||
            !TryComp<InterviewHologramComponent>(hologram, out var hologramComp))
            return;

        var visualState = EnsureVisualState(hologram, sprite, hologramComp);
        UpdateHologramShader(sprite, hologramComp, visualState);
    }

    private HologramVisualState EnsureVisualState(EntityUid uid, SpriteComponent sprite, InterviewHologramComponent hologramComp)
    {
        if (!_visualStates.TryGetValue(uid, out var visualState))
        {
            visualState = new HologramVisualState
            {
                Shader = _prototype.Index<ShaderPrototype>(hologramComp.ShaderName).InstanceUnique(),
                LayerCount = -1,
            };

            _visualStates[uid] = visualState;
        }

        var layerCount = sprite.AllLayers.Count();
        if (visualState.LayerCount != layerCount || NeedsLayerShaderRefresh(sprite))
        {
            ApplyHologramVisuals(sprite, hologramComp);
            visualState.LayerCount = layerCount;
        }

        visualState.TexHeight = GetLargestLayerHeight(sprite);
        return visualState;
    }

    private static bool NeedsLayerShaderRefresh(SpriteComponent sprite)
    {
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            if (!sprite.TryGetLayer(i, out var layer)
                || layer.ShaderPrototype == "DisplacedStencilDraw"
                || layer.ShaderPrototype == "unshaded")
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void ApplyHologramVisuals(SpriteComponent sprite, InterviewHologramComponent hologramComp)
    {
        sprite.Color = Color.White;
        sprite.Offset = hologramComp.Offset;
        sprite.DrawDepth = (int)DrawDepth.Mobs;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            if (sprite.TryGetLayer(i, out var layer) && layer.ShaderPrototype != "DisplacedStencilDraw")
                sprite.LayerSetShader(i, "unshaded");
        }
    }

    private static float GetLargestLayerHeight(SpriteComponent sprite)
    {
        var texHeight = 0f;

        foreach (var layer in sprite.AllLayers)
        {
            if (layer.PixelSize.Y > texHeight)
                texHeight = layer.PixelSize.Y;
        }

        return texHeight;
    }

    private void UpdateHologramShader(SpriteComponent sprite, InterviewHologramComponent hologramComp, HologramVisualState visualState)
    {
        var instance = visualState.Shader;
        instance.SetParameter("color1", new Vector3(hologramComp.Color1.R, hologramComp.Color1.G, hologramComp.Color1.B));
        instance.SetParameter("color2", new Vector3(hologramComp.Color2.R, hologramComp.Color2.G, hologramComp.Color2.B));
        instance.SetParameter("alpha", hologramComp.Alpha);
        instance.SetParameter("intensity", hologramComp.Intensity);
        instance.SetParameter("texHeight", visualState.TexHeight);
        instance.SetParameter("t", (float)_timing.CurTime.TotalSeconds * hologramComp.ScrollRate);

        sprite.PostShader = instance;
        sprite.RaiseShaderEvent = true;
    }

    private sealed class HologramVisualState
    {
        public required ShaderInstance Shader;
        public int LayerCount;
        public float TexHeight;
    }

    // NOOP, spawn logic handled on server.
    protected override void HandleApprovalChanged(Entity<InterviewHologramComponent> ent)
    {
    }
}

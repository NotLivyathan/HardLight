using Content.Server.Spreader;
using Content.Shared.Atmos;
using Content.Shared.CM14.Xenos;
using Content.Shared.CM14.Xenos.Construction;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using XenoWeedableComponent = Content.Shared.CM14.Xenos.Construction.Nest.XenoWeedableComponent;
using XenoWeedsComponent = Content.Shared.CM14.Xenos.Construction.XenoWeedsComponent;

namespace Content.Server.CM14.Xenos.Construction;

[UsedImplicitly]
public sealed class XenoConstructionServerSystem : SharedXenoConstructionSystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly List<EntityUid> _anchored = new();

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[XenoWeeds] (server) XenoConstructionSystem.Initialize()");

        SubscribeLocalEvent<XenoWeedsComponent, SpreadNeighborsEvent>(OnWeedsSpreadNeighbors);
        SubscribeLocalEvent<XenoWeedsComponent, AnchorStateChangedEvent>(OnWeedsAnchorChanged);
        SubscribeLocalEvent<XenoWeedableComponent, AnchorStateChangedEvent>(OnWeedableAnchorStateChanged);
    }

    private void OnWeedsAnchorChanged(Entity<XenoWeedsComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            QueueDel(ent);
    }

    // Note: If HiveCoreComponent exists in this codebase, wire its MapInit here.
    //private void OnHiveCoreMapInit(Entity<HiveCoreComponent> ent, ref MapInitEvent args)
    //{
    //    var coordinates = _transform.GetMoverCoordinates(ent).SnapToGrid(EntityManager, _map);
    //    Spawn(ent.Comp.Spawns, coordinates);
    //}
    private void OnWeedsSpreadNeighbors(Entity<XenoWeedsComponent> ent, ref SpreadNeighborsEvent args)
    {
        var source = ent.Comp.IsSource ? ent.Owner : ent.Comp.Source;

        // TODO CM14
        // There is an edge case right now where existing weeds can block new weeds
        // from expanding further. If this is the case then the weeds should reassign
        // their source to this one and reactivate if it is closer to them than their
        // original source and only if it is still within range
        if (args.NeighborFreeTiles.Count <= 0 ||
            !Exists(source) ||
            !TryComp(source, out TransformComponent? transform) ||
            ent.Comp.Spawns.Id is not { } prototype)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        var any = false;
        foreach (var neighbor in args.NeighborFreeTiles)
        {
            var gridUid = neighbor.Tile.GridUid;
            var coords = _mapSystem.GridTileToLocal(gridUid, neighbor.Grid, neighbor.Tile.GridIndices);

            var sourceLocal = _mapSystem.CoordinatesToTile(gridUid, neighbor.Grid, transform.Coordinates);
            var diff = neighbor.Tile.GridIndices - sourceLocal;
            if (Math.Abs(diff.X) >= ent.Comp.Range || Math.Abs(diff.Y) >= ent.Comp.Range)
                continue;

            var neighborWeeds = Spawn(prototype, coords);
            var neighborWeedsComp = EnsureComp<XenoWeedsComponent>(neighborWeeds);

            neighborWeedsComp.IsSource = false;
            neighborWeedsComp.Source = source;

            EnsureComp<ActiveEdgeSpreaderComponent>(neighborWeeds);

            any = true;

            // Respect spread budget per tick
            args.Updates--;
            if (args.Updates <= 0)
                return;

            for (var i = 0; i < 4; i++)
            {
                var dir = (AtmosDirection)(1 << i);
                var pos = neighbor.Tile.GridIndices.Offset(dir);
                if (!_mapSystem.TryGetTileRef(gridUid, neighbor.Grid, pos, out var adjacent))
                    continue;

                _anchored.Clear();
                _mapSystem.GetAnchoredEntities((gridUid, neighbor.Grid), adjacent.GridIndices, _anchored);
                foreach (var anchored in _anchored)
                {
                    if (!TryComp(anchored, out XenoWeedableComponent? weedable) ||
                        weedable.Entity != null ||
                        !TryComp(anchored, out TransformComponent? weedableTransform) ||
                        !weedableTransform.Anchored)
                    {
                        continue;
                    }

                    weedable.Entity = SpawnAtPosition(weedable.Spawn, anchored.ToCoordinates());
                }
            }
        }

        if (!any)
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
    }

    private void OnWeedableAnchorStateChanged(Entity<XenoWeedableComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            QueueDel(ent.Comp.Entity);
    }
}

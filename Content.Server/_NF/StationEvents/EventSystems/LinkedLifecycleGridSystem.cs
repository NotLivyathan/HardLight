using System.Numerics;
using System.Linq;
using Content.Server.StationEvents.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mech.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Content.Shared._Goobstation.Vehicles;

namespace Content.Server.StationEvents.Events;

public sealed class LinkedLifecycleGridSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public readonly record struct ReparentTarget(EntityUid EntityUid, EntityUid MapUid, Vector2 MapPosition);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LinkedLifecycleGridParentComponent, GridSplitEvent>(OnParentSplit);
        SubscribeLocalEvent<LinkedLifecycleGridChildComponent, GridSplitEvent>(OnChildSplit);

        SubscribeLocalEvent<LinkedLifecycleGridParentComponent, ComponentRemove>(OnMasterRemoved);
    }

    private void OnParentSplit(EntityUid uid, LinkedLifecycleGridParentComponent component, ref GridSplitEvent args)
    {
        LinkSplitGrids(uid, ref args);
    }

    private void OnChildSplit(EntityUid uid, LinkedLifecycleGridChildComponent component, ref GridSplitEvent args)
    {
        LinkSplitGrids(component.LinkedUid, ref args);
    }

    private void LinkSplitGrids(EntityUid target, ref GridSplitEvent args)
    {
        if (!TryComp(target, out LinkedLifecycleGridParentComponent? master))
            return;

        foreach (var grid in args.NewGrids)
        {
            if (grid == target)
                continue;

            var comp = EnsureComp<LinkedLifecycleGridChildComponent>(grid);
            comp.LinkedUid = target;
            master.LinkedEntities.Add(grid);
        }
    }

    private void OnMasterRemoved(EntityUid uid, LinkedLifecycleGridParentComponent component, ref ComponentRemove args)
    {
        if (!TryComp<MetaDataComponent>(uid, out var meta))
            return;

        // Somebody destroyed our component, but the entity lives on, do not destroy the grids.
        if (meta.EntityLifeStage < EntityLifeStage.Terminating)
            return;

        // Destroy child entities
        foreach (var entity in component.LinkedEntities.ToArray())
            UnparentPlayersFromGrid(entity, true);
    }

    // Try to get parent of entity where appropriate.
    private (EntityUid, TransformComponent) GetParentToReparent(EntityUid uid, TransformComponent xform)
    {
        if (TryComp<VehicleComponent>(xform.ParentUid, out var vehicle) && vehicle.Driver == uid)
        {
            if (!TryComp<TransformComponent>(xform.ParentUid, out var vehicleXform))
                return (uid, xform);

            if (vehicleXform.MapUid != null)
            {
                return (xform.ParentUid, vehicleXform);
            }
        }
        if (TryComp<MechPilotComponent>(uid, out var mechPilot))
        {
            if (!TryComp<TransformComponent>(mechPilot.Mech, out var mechXform))
                return (uid, xform);

            if (mechXform.MapUid != null)
            {
                return (mechPilot.Mech, mechXform);
            }
        }
        return (uid, xform);
    }

    /// <summary>
    /// Returns a list of entities to reparent on a grid.
    /// Useful if you need to do your own bookkeeping.
    /// </summary>
    public List<ReparentTarget> GetEntitiesToReparent(EntityUid grid)
    {
        List<ReparentTarget> reparentEntities = new();
        HashSet<EntityUid> handledMindContainers = new();
        HashSet<EntityUid> queuedEntities = new();

        // Get player characters
        var mobQuery = AllEntityQuery<HumanoidAppearanceComponent, BankAccountComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var mobUid, out _, out _, out var xform))
        {
            handledMindContainers.Add(mobUid);

            if (xform.GridUid == null || xform.MapUid == null || xform.GridUid != grid)
                continue;

            var (targetUid, targetXform) = GetParentToReparent(mobUid, xform);

            TryAddReparentTarget(targetUid, targetXform, ref reparentEntities, queuedEntities);

            HandlePulledEntity(targetUid, ref reparentEntities, queuedEntities);
        }

        // Get silicon
        var borgQuery = AllEntityQuery<BorgChassisComponent, ActorComponent, TransformComponent>();
        while (borgQuery.MoveNext(out var borgUid, out _, out _, out var xform))
        {
            handledMindContainers.Add(borgUid);

            if (xform.GridUid == null || xform.MapUid == null || xform.GridUid != grid)
                continue;

            var (targetUid, targetXform) = GetParentToReparent(borgUid, xform);

            TryAddReparentTarget(targetUid, targetXform, ref reparentEntities, queuedEntities);

            HandlePulledEntity(targetUid, ref reparentEntities, queuedEntities);
        }

        // Get occupied MindContainers (non-humanoids, pets, etc.)
        var mindQuery = AllEntityQuery<MindContainerComponent, TransformComponent>();
        while (mindQuery.MoveNext(out var mobUid, out var mindContainer, out var xform))
        {
            if (xform.GridUid == null || xform.MapUid == null || xform.GridUid != grid)
                continue;

            // Not player-controlled, little to lose
            if (_mind.GetMind(mobUid, mindContainer) == null)
                continue;

            // All humans and borgs should have mind containers - if we've handled them already, no need.
            if (handledMindContainers.Contains(mobUid))
                continue;

            var (targetUid, targetXform) = GetParentToReparent(mobUid, xform);

            TryAddReparentTarget(targetUid, targetXform, ref reparentEntities, queuedEntities);

            HandlePulledEntity(targetUid, ref reparentEntities, queuedEntities);
        }

        return reparentEntities;
    }

    /// <summary>
    /// Tries to get what the passed entity is pulling, if anything, and adds it to the passed list.
    /// </summary>
    private void HandlePulledEntity(Entity<PullerComponent?> entity, ref List<ReparentTarget> listToReparent, HashSet<EntityUid> queuedEntities)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        if (entity.Comp.Pulling is not EntityUid pulled)
            return;

        if (!TryComp<TransformComponent>(pulled, out var pulledXform))
            return;

        if (pulledXform.MapUid is not EntityUid pulledMapUid)
            return;

        TryAddReparentTarget(pulled, pulledXform, ref listToReparent, queuedEntities);
    }

    private void TryAddReparentTarget(
        EntityUid uid,
        TransformComponent xform,
        ref List<ReparentTarget> listToReparent,
        HashSet<EntityUid> queuedEntities)
    {
        if (!queuedEntities.Add(uid))
            return;

        if (xform.MapUid is not EntityUid mapUid)
            return;

        listToReparent.Add(new ReparentTarget(uid, mapUid, _transform.GetWorldPosition(xform)));
    }

    // Deletes a grid, reparenting every humanoid and player character that's on it.
    public void UnparentPlayersFromGrid(EntityUid grid, bool deleteGrid, bool ignoreLifeStage = false)
    {
        if (!TryComp<MetaDataComponent>(grid, out var gridMeta))
            return;

        if (!ignoreLifeStage && gridMeta.EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        var reparentEntities = GetEntitiesToReparent(grid);

        foreach (var target in reparentEntities)
        {
            if (!TryComp<MetaDataComponent>(target.EntityUid, out var meta) || meta.EntityLifeStage >= EntityLifeStage.Terminating)
                continue;

            if (!TryComp<TransformComponent>(target.EntityUid, out var xform))
                continue;

            // If the item has already been moved to nullspace, skip it.
            if (xform.MapID == MapId.Nullspace)
                continue;

            // Move the target and all of its children (for bikes, mechs, etc.)
            _transform.DetachEntity(target.EntityUid, xform);
        }

        // Deletion has to happen before grid traversal re-parents players.
        if (deleteGrid)
            Del(grid);

        foreach (var target in reparentEntities)
        {
            if (!TryComp<MetaDataComponent>(target.EntityUid, out var meta) || meta.EntityLifeStage >= EntityLifeStage.Terminating)
                continue;

            if (!TryComp<TransformComponent>(target.EntityUid, out var xform))
                continue;

            // If the item has already been moved out of nullspace, skip it.
            if (xform.MapID != MapId.Nullspace)
                continue;

            _transform.SetCoordinates(target.EntityUid, xform, new EntityCoordinates(target.MapUid, target.MapPosition));
        }
    }
}

using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

public sealed class PortalArtifactSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PortalArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }

    private void OnActivate(Entity<PortalArtifactComponent> artifact, ref ArtifactActivatedEvent args)
    {
        var map = Transform(artifact).MapID;
        var validMinds = new ValueList<EntityUid>();
        var mindQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent, MetaDataComponent>();
        while (mindQuery.MoveNext(out var uid, out var mc, out var xform, out var meta))
        {
            // Cheap-first short-circuit: most MindContainers on a populated server are not on
            // the same map as the artifact, and IsEntityOrParentInContainer walks the parent
            // chain. Filter by map, then HasMind, then container check. Same set of valid minds.
            if (xform.MapID != map)
                continue;
            if (!mc.HasMind)
                continue;
            if (_container.IsEntityOrParentInContainer(uid, meta: meta, xform: xform))
                continue;

            validMinds.Add(uid);
        }
        //this would only be 0 if there were a station full of AIs and no one else, in that case just stop this function
        if (validMinds.Count == 0)
            return;

        var firstPortal = Spawn(artifact.Comp.PortalProto, _transform.GetMapCoordinates(artifact));

        var target = _random.Pick(validMinds);

        var secondPortal = Spawn(artifact.Comp.PortalProto, _transform.GetMapCoordinates(target));

        //Manual position swapping, because the portal that opens doesn't trigger a collision, and doesn't teleport targets the first time.
        _transform.SwapPositions(target, artifact.Owner);

        _link.TryLink(firstPortal, secondPortal, true);
    }
}

using System.Linq;
using Content.Server.Salvage;
using Content.Server.Xenoarchaeology.XenoArtifacts.Triggers.Components;
using Content.Shared.Clothing;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Triggers.Systems;

/// <summary>
/// This handles artifacts that are activated by magnets, both salvage and magboots.
/// </summary>
public sealed class ArtifactMagnetTriggerSystem : EntitySystem
{
    [Dependency] private readonly ArtifactSystem _artifact = default!;

    private readonly List<Entity<ArtifactMagnetTriggerComponent, TransformComponent>> _artifacts = new();
    private readonly HashSet<EntityUid> _toActivate = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SalvageMagnetActivatedEvent>(OnMagnetActivated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Single pass to build the artifact list. Replaces the previous EntityQuery<>().Any()
        // precheck which iterated all magnet artifacts and was followed by another full
        // re-enumeration inside every active-magboot iteration.
        _artifacts.Clear();
        var artifactQuery = EntityQueryEnumerator<ArtifactMagnetTriggerComponent, TransformComponent>();
        while (artifactQuery.MoveNext(out var uid, out var trigger, out var xform))
        {
            _artifacts.Add((uid, trigger, xform));
        }

        if (_artifacts.Count == 0)
            return;

        _toActivate.Clear();

        //assume that there's more magboots than artifacts
        var query = EntityQueryEnumerator<MagbootsComponent, TransformComponent, ItemToggleComponent>();
        while (query.MoveNext(out _, out var magboot, out var magXform, out var toggle))
        {
            if (!toggle.Activated)
                continue;

            foreach (var (artifactUid, trigger, xform) in _artifacts)
            {
                // Already queued from a different magboot this tick; skip the distance check.
                if (_toActivate.Contains(artifactUid))
                    continue;

                if (!magXform.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var distance))
                    continue;

                if (distance > trigger.MagbootRange)
                    continue;

                _toActivate.Add(artifactUid);
            }
        }

        foreach (var a in _toActivate)
        {
            _artifact.TryActivateArtifact(a);
        }
    }

    private void OnMagnetActivated(ref SalvageMagnetActivatedEvent ev)
    {
        _toActivate.Clear();

        var magXform = Transform(ev.Magnet);

        var query = EntityQueryEnumerator<ArtifactMagnetTriggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var artifact, out var xform))
        {
            if (!magXform.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var distance))
                continue;

            if (distance > artifact.Range)
                continue;

            _toActivate.Add(uid);
        }

        foreach (var a in _toActivate)
        {
            _artifact.TryActivateArtifact(a, logMissing: false);
        }
    }
}

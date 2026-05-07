using System.Linq;
using Content.Shared.Instruments;
using Content.Server.Instruments;
using Content.Server.Xenoarchaeology.XenoArtifacts.Triggers.Components;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Triggers.Systems;

/// <summary>
/// This handles activating an artifact when music is playing nearby
/// </summary>
public sealed class ArtifactMusicTriggerSystem : EntitySystem
{
    [Dependency] private readonly ArtifactSystem _artifact = default!;

    private readonly List<Entity<ArtifactMusicTriggerComponent, TransformComponent>> _artifacts = new();
    private readonly HashSet<EntityUid> _toActivate = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _artifacts.Clear();
        var artifactQuery = EntityQueryEnumerator<ArtifactMusicTriggerComponent, TransformComponent>();
        while (artifactQuery.MoveNext(out var uid, out var trigger, out var xform))
        {
            _artifacts.Add((uid, trigger, xform));
        }

        if (_artifacts.Count == 0)
            return;

        _toActivate.Clear();
        var query = EntityQueryEnumerator<ActiveInstrumentComponent, TransformComponent>();

        //assume that there's more instruments than artifacts
        while (query.MoveNext(out _, out var instXform))
        {
            foreach (var (uid, trigger, xform) in _artifacts)
            {
                // Already queued from a different instrument this tick; skip the distance check.
                // Multiple instruments near the same artifact would otherwise call
                // TryActivateArtifact repeatedly, which the cooldown ignores anyway.
                if (_toActivate.Contains(uid))
                    continue;

                if (!instXform.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var distance))
                    continue;

                if (distance > trigger.Range)
                    continue;

                _toActivate.Add(uid);
            }
        }

        foreach (var a in _toActivate)
        {
            _artifact.TryActivateArtifact(a);
        }
    }
}

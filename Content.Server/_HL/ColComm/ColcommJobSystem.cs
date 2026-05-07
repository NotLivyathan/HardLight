using Content.Server.GameTicking.Events;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._HL.ColComm;

// Manages the Colonial Command job registry: the persistent, server-wide authoritative job slot tracker.
// All job slot open/close operations route through this system instead of per-station.
public sealed class ColcommJobSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (!TryGetColcommRegistry(out var colcomm))
            return;

        ResetToDefaults(colcomm);

        // Let other systems deduct active role counts for
        // crew that persisted from the previous round.
        RaiseLocalEvent(new ColcommRegistryRoundStartEvent { Colcomm = colcomm });
    }

    // First-time initialization from a freshly created ColComm entity.
    public void InitRegistry(Entity<ColcommJobRegistryComponent> colcomm)
    {
        ResetToDefaults(colcomm);
    }

    // Sets the job configuration on ColComm creation and initializes the registry.
    public void SetupColcommRegistry(Entity<ColcommJobRegistryComponent> colcomm, Dictionary<ProtoId<JobPrototype>, int[]> configuredJobs)
    {
        if (colcomm.Comp.ConfiguredJobs.Count > 0)
            return; // Already configured from a prior round. Do not overwrite.

        colcomm.Comp.ConfiguredJobs = configuredJobs;
        ResetToDefaults(colcomm);
    }

    private void ResetToDefaults(Entity<ColcommJobRegistryComponent> colcomm)
    {
        var comp = colcomm.Comp;
        comp.CurrentSlots.Clear();
        comp.MidRoundMaxSlots.Clear();
        comp.PlayerJobs.Clear();

        foreach (var (job, slots) in comp.ConfiguredJobs)
        {
            var midRoundMax = slots.Length > 1 ? slots[1] : 0;
            comp.CurrentSlots[job] = midRoundMax < 0 ? (int?)null : midRoundMax;
            comp.MidRoundMaxSlots[job] = Math.Max(midRoundMax, 0);
        }
    }

    // Deducts the given job counts from ColComm's current available slots.
    // Should account for crew transitioning from the previous round.
    public void DeductActiveRoles(Entity<ColcommJobRegistryComponent> colcomm, Dictionary<ProtoId<JobPrototype>, int> activeCounts)
    {
        foreach (var (job, count) in activeCounts)
        {
            if (!colcomm.Comp.CurrentSlots.TryGetValue(job, out var current) || current == null)
                continue;

            colcomm.Comp.CurrentSlots[job] = Math.Max(current.Value - count, 0);
        }
    }

    // Returns the ColComm grid entity that holds the registry, if one exists.
    public bool TryGetColcommRegistry(out Entity<ColcommJobRegistryComponent> entity)
    {
        var query = AllEntityQuery<ColcommJobRegistryComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            entity = (uid, comp);
            return true;
        }

        entity = default;
        return false;
    }

    public bool IsConfiguredJob(Entity<ColcommJobRegistryComponent> colcomm, ProtoId<JobPrototype> jobId)
        => colcomm.Comp.ConfiguredJobs.ContainsKey(jobId);

    public bool TryGetJobSlot(Entity<ColcommJobRegistryComponent> colcomm, ProtoId<JobPrototype> jobId, out int? slots)
        => colcomm.Comp.CurrentSlots.TryGetValue(jobId, out slots);

    public bool TryAdjustJobSlot(Entity<ColcommJobRegistryComponent> colcomm, ProtoId<JobPrototype> jobId, int amount, bool clamp = false)
    {
        if (!colcomm.Comp.CurrentSlots.TryGetValue(jobId, out var current))
            return false;

        if (current == null)
            return true; // Unlimited; no adjustment needed

        var newVal = current.Value + amount;
        if (newVal < 0 && !clamp)
            return false;

        colcomm.Comp.CurrentSlots[jobId] = Math.Max(newVal, 0);
        return true;
    }

    // Sets the available slot count.
    public bool TrySetJobSlot(Entity<ColcommJobRegistryComponent> colcomm, ProtoId<JobPrototype> jobId, int amount, bool createSlot = false)
    {
        if (!colcomm.Comp.CurrentSlots.ContainsKey(jobId))
        {
            if (!createSlot)
                return false;
        }

        colcomm.Comp.CurrentSlots[jobId] = amount;
        return true;
    }

    // Sets the mid-round max for the given job.
    public bool TrySetJobMidRoundMax(Entity<ColcommJobRegistryComponent> colcomm, ProtoId<JobPrototype> jobId, int amount, bool createSlot = false)
    {
        if (!colcomm.Comp.MidRoundMaxSlots.ContainsKey(jobId))
        {
            if (!createSlot)
                return false;
        }

        colcomm.Comp.MidRoundMaxSlots[jobId] = amount;
        return true;
    }

    public bool IsPlayerJobTracked(Entity<ColcommJobRegistryComponent> colcomm, NetUserId userId, ProtoId<JobPrototype> jobId)
        => colcomm.Comp.PlayerJobs.TryGetValue(userId, out var jobs) && jobs.Contains(jobId);

    public bool TryTrackPlayerJob(Entity<ColcommJobRegistryComponent> colcomm, NetUserId userId, ProtoId<JobPrototype> jobId)
    {
        colcomm.Comp.PlayerJobs.TryAdd(userId, new HashSet<ProtoId<JobPrototype>>());
        colcomm.Comp.PlayerJobs[userId].Add(jobId);
        return true;
    }

    public bool TryUntrackPlayerJob(Entity<ColcommJobRegistryComponent> colcomm, NetUserId userId, ProtoId<JobPrototype> jobId)
    {
        if (!colcomm.Comp.PlayerJobs.TryGetValue(userId, out var jobs))
            return false;

        jobs.Remove(jobId);
        if (jobs.Count == 0)
            colcomm.Comp.PlayerJobs.Remove(userId);
        return true;
    }
}

// Raised after the ColComm registry has been reset to defaults at the start of a new round.
public sealed class ColcommRegistryRoundStartEvent : EntityEventArgs
{
    public Entity<ColcommJobRegistryComponent> Colcomm;
}

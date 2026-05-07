using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

/// <summary>
///     Handles NPC which become aggressive after being attacked.
/// </summary>
public sealed class NPCRetaliationSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Reusable scratch buffer for expired-attack-memory cleanup.
    // Avoids allocating a fresh ValueList per NPC per tick.
    private readonly List<EntityUid> _expiredScratch = new();

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<NPCRetaliationComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<NPCRetaliationComponent, DisarmedEvent>(OnDisarmed);
    }

    private void OnDamageChanged(Entity<NPCRetaliationComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (args.Origin is not {} origin)
            return;

        TryRetaliate(ent, origin);
    }

    private void OnDisarmed(Entity<NPCRetaliationComponent> ent, ref DisarmedEvent args)
    {
        TryRetaliate(ent, args.Source);
    }

    public bool TryRetaliate(Entity<NPCRetaliationComponent> ent, EntityUid target)
    {
        // don't retaliate against inanimate objects.
        if (!HasComp<MobStateComponent>(target))
            return false;

        // don't retaliate against the same faction
        if (_npcFaction.IsEntityFriendly(ent.Owner, target))
            return false;

        _npcFaction.AggroEntity(ent.Owner, target);
        if (ent.Comp.AttackMemoryLength is {} memoryLength)
            ent.Comp.AttackMemories[target] = _timing.CurTime + memoryLength;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<NPCRetaliationComponent, FactionExceptionComponent>();
        while (query.MoveNext(out var uid, out var retaliationComponent, out var factionException))
        {
            var memories = retaliationComponent.AttackMemories;
            if (memories.Count == 0)
                continue;

            // Collect expired (or terminated) entries into the reusable scratch list, then prune.
            // Iterating the dictionary directly while mutating is unsafe, so a one-off snapshot is
            // required — but we reuse the same list across all NPCs and ticks.
            _expiredScratch.Clear();
            foreach (var (entity, expiry) in memories)
            {
                if (TerminatingOrDeleted(entity) || curTime >= expiry)
                    _expiredScratch.Add(entity);
            }

            for (var i = 0; i < _expiredScratch.Count; i++)
            {
                var entity = _expiredScratch[i];
                _npcFaction.DeAggroEntity((uid, factionException), entity);
                memories.Remove(entity);
            }
        }

        _expiredScratch.Clear();
    }
}

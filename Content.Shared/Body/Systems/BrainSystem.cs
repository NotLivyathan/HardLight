using Content.Shared.Body.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Pointing;
// Shitmed start
using Content.Shared._Shitmed.Body.Organ;
// Shitmed end

namespace Content.Shared.Body.Systems
{
    public sealed class BrainSystem : EntitySystem
    {
        [Dependency] private readonly SharedMindSystem _mindSystem = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BrainComponent, OrganGotInsertedEvent>((uid, brain, args) => HandleMind(args.Target, uid, brain));
        // Shitmed start
            SubscribeLocalEvent<BrainComponent, OrganGotRemovedEvent>(HandleRemoval); // HardLight: Merged with upstream; OrganRemovedFromBodyEvent<OrganGotRemovedEvent
            SubscribeLocalEvent<BrainComponent, PointAttemptEvent>(OnPointAttempt);
        }

        private void HandleRemoval(EntityUid uid, BrainComponent brain, ref OrganGotRemovedEvent args)
        {
            if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.Target))
                return;

            brain.Active = false;
            if (!CheckOtherBrains(args.Target))
            {
                // Prevents revival, should kill the user within a given timespan too.
                EnsureComp<DebrainedComponent>(args.Target);
                HandleMind(uid, args.Target);
            }
        }

        private void HandleMind(EntityUid newEntity, EntityUid oldEntity, BrainComponent? brain = null)
        {
            if (TerminatingOrDeleted(newEntity) || TerminatingOrDeleted(oldEntity))
                return;

            EnsureComp<MindContainerComponent>(newEntity);
            EnsureComp<MindContainerComponent>(oldEntity);

            var ghostOnMove = EnsureComp<GhostOnMoveComponent>(newEntity);
            if (HasComp<BodyComponent>(newEntity))
                ghostOnMove.MustBeDead = true;

            if (!_mindSystem.TryGetMind(oldEntity, out var mindId, out var mind))
                return;

            _mindSystem.TransferTo(mindId, newEntity, mind: mind);
            if (brain != null)
                brain.Active = true;
        }

        private bool CheckOtherBrains(EntityUid entity)
        {
            var hasOtherBrains = false;
            if (TryComp<BodyComponent>(entity, out var body))
            {
                if (TryComp<BrainComponent>(entity, out var bodyBrain))
                    hasOtherBrains = true;
                else
                {
                    foreach (var organ in body.Organs?.ContainedEntities ?? [])
                    {
                        if (TryComp<BrainComponent>(organ, out var brain) && brain.Active)
                        {
                            hasOtherBrains = true;
                            break;
                        }
                    }
                }
            }

            return hasOtherBrains;
        }

        // Shitmed end
        private void OnPointAttempt(Entity<BrainComponent> ent, ref PointAttemptEvent args)
        {
            args.Cancel();
        }
    }
}

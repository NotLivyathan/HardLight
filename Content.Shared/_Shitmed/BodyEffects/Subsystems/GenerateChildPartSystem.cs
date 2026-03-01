using Content.Shared.Body;
using Content.Shared._Shitmed.Body.Organ;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using System.Numerics;

namespace Content.Shared._Shitmed.BodyEffects.Subsystems;

public sealed class GenerateChildPartSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenerateChildPartComponent, OrganComponentsModifyEvent>(OnPartComponentsModify);
    }

    private void OnPartComponentsModify(EntityUid uid, GenerateChildPartComponent component, ref OrganComponentsModifyEvent args)
    {
        if (args.Add)
            CreatePart(uid, component);
        //else
            //DeletePart(uid, component);
    }

    private void CreatePart(EntityUid uid, GenerateChildPartComponent component)
    {
        if (!TryComp(uid, out OrganComponent? organComp)
            || organComp.Body is null
            || component.Active)
            return;

        // I pinky swear to also move this to the server side properly next update :)
        if (_net.IsServer)
        {
            if (!TryComp(organComp.Body.Value, out BodyComponent? bodyComp) || bodyComp.Organs == null)
                return;

            var childPart = Spawn(component.Id, new EntityCoordinates(organComp.Body.Value, Vector2.Zero));

            if (!_containers.Insert(childPart, bodyComp.Organs))
            {
                QueueDel(childPart);
                return;
            }

            component.ChildPart = childPart;
            component.Active = true;
        }
    }

    // Still unusued, gotta figure out what I want to do with this function outside of fuckery with mantis blades.
    private void DeletePart(EntityUid uid, GenerateChildPartComponent component)
    {
        if (!TryComp(uid, out OrganComponent? organComp))
            return;

        if (organComp.Body is { } bodyId && TryComp(bodyId, out BodyComponent? bodyComp) && bodyComp.Organs != null)
            _containers.Remove(uid, bodyComp.Organs);

        QueueDel(uid);
    }
}


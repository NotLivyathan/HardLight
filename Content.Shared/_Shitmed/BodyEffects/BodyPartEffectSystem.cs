using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Body;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared._Shitmed.BodyEffects;
public partial class BodyPartEffectSystem : EntitySystem
{
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, OrganComponentsModifyEvent>(OnPartComponentsModify);
    }

    // While I would love to kill this function, problem is that if we happen to have two parts that add the same
    // effect, removing one will remove both of them, since we cant tell what the source of a Component is.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BodyPartEffectComponent, OrganComponent>();
        var now = _gameTiming.CurTime;
        while (query.MoveNext(out var uid, out var comp, out var part))
        {
            if (now < comp.NextUpdate || !comp.Active.Any() || part.Body is not { } body)
                continue;

            comp.NextUpdate = now + comp.Delay;
            AddComponents(body, uid, comp.Active);
        }
    }

    private void OnPartComponentsModify(Entity<OrganComponent> partEnt,
        ref OrganComponentsModifyEvent ev)
    {
        if (!TryComp<BodyPartEffectComponent>(partEnt, out var effectComp))
            return;

        if (ev.Add)
            AddComponents(ev.Body, partEnt, effectComp.Active, effectComp);
        else
            RemoveComponents(ev.Body, partEnt, effectComp.Active, effectComp);

        Dirty(partEnt, effectComp);
    }

    private void AddComponents(EntityUid body,
        EntityUid part,
        ComponentRegistry reg,
        BodyPartEffectComponent? effectComp = null)
    {
        if (!Resolve(part, ref effectComp, logMissing: false))
            return;

        foreach (var (key, comp) in reg)
        {
            var compType = comp.Component.GetType();
            if (HasComp(body, compType))
                continue;

            var newComp = (Component) _serManager.CreateCopy(comp.Component, notNullableOverride: true);
            EntityManager.AddComponent(body, newComp, true);

            effectComp.Active[key] = comp;
        }
    }

    private void RemoveComponents(EntityUid body,
        EntityUid part,
        ComponentRegistry reg,
        BodyPartEffectComponent? effectComp = null)
    {
        if (!Resolve(part, ref effectComp, logMissing: false))
            return;

        foreach (var (key, comp) in reg)
        {
            RemComp(body, comp.Component.GetType());
            effectComp.Active.Remove(key);
        }
    }
}

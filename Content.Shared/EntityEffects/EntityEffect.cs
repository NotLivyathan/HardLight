using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;
// HardLight start
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
// HardLight end

namespace Content.Shared.EntityEffects;

/// <summary>
/// A basic instantaneous effect which can be applied to an entity via events.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class EntityEffect
{
    public abstract void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user);

    [DataField]
    public EntityCondition[]? Conditions;

    /// <summary>
    /// If our scale is less than this value, the effect fails.
    /// </summary>
    [DataField]
    public virtual float MinScale { get; private set; }

    /// <summary>
    /// If true, then it allows the scale multiplier to go above 1.
    /// </summary>
    [DataField]
    public virtual bool Scaling { get; private set; } = true;

    // TODO: This should be an entity condition but guidebook relies on it heavily for formatting...
    /// <summary>
    /// Probability of the effect occuring.
    /// </summary>
    [DataField]
    public float Probability = 1.0f;

    public virtual string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    /// <summary>
    /// If this effect is logged, how important is the log?
    /// </summary>
    [ViewVariables]
    public virtual LogImpact? Impact => null;

    [ViewVariables]
    public virtual LogType LogType => LogType.EntityEffect;
}

/// <summary>
/// Used to store an <see cref="EntityEffect"/> so it can be raised without losing the type of the condition.
/// </summary>
/// <typeparam name="T">The Condition wer are raising.</typeparam>
public abstract partial class EntityEffectBase<T> : EntityEffect where T : EntityEffectBase<T>
{
    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        if (this is not T type)
            return;

        raiser.RaiseEffectEvent(target, type, scale, user);
    }
}

// HardLight: Apparently necessary, so I'm putting this here for now.
/// <summary>
///     EntityEffectBaseArgs only contains the target of an effect.
///     If a trigger wants to include more info (e.g. the quantity of the chemical triggering the effect), it can be extended (see EntityEffectReagentArgs).
/// </summary>
public record class EntityEffectBaseArgs
{
    public EntityUid TargetEntity;

    public IEntityManager EntityManager = default!;

    public EntityEffectBaseArgs(EntityUid targetEntity, IEntityManager entityManager)
    {
        TargetEntity = targetEntity;
        EntityManager = entityManager;
    }
}

public record class EntityEffectReagentArgs : EntityEffectBaseArgs
{
    public EntityUid? OrganEntity;

    public Solution? Source;

    public FixedPoint2 Quantity;

    public ReagentPrototype? Reagent;

    public ReactionMethod? Method;

    public FixedPoint2 Scale;

    public EntityEffectReagentArgs(EntityUid targetEntity, IEntityManager entityManager, EntityUid? organEntity, Solution? source, FixedPoint2 quantity, ReagentPrototype? reagent, ReactionMethod? method, FixedPoint2 scale) : base(targetEntity, entityManager)
    {
        OrganEntity = organEntity;
        Source = source;
        Quantity = quantity;
        Reagent = reagent;
        Method = method;
        Scale = scale;
    }
}

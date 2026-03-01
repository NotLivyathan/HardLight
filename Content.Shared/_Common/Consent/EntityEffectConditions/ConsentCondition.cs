// SPDX-FileCopyrightText: Copyright (c) 2025 Space Wizards Federation
// SPDX-License-Identifier: MIT

using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Common.Consent.EffectConditions;

/// <summary>
/// Checks if the target entity has consented to a specific toggle.
/// </summary>
public sealed partial class ConsentConditionSystem : EntityConditionSystem<ConsentComponent, ConsentCondition>
{
    [Dependency] private readonly SharedConsentSystem _consentSystem = default!;

    protected override void Condition(Entity<ConsentComponent> entity, ref EntityConditionEvent<ConsentCondition> args)
    {
        args.Result = _consentSystem.HasConsent(entity, args.Condition.Consent);
    }
}

public sealed partial class ConsentCondition : EntityConditionBase<ConsentCondition>
{
    [DataField]
    public ProtoId<ConsentTogglePrototype> Consent = default!;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-consent-condition", ("consent", Consent));
    }
}

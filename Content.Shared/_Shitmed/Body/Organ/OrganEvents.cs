namespace Content.Shared._Shitmed.Body.Organ;

public readonly record struct OrganComponentsModifyEvent(EntityUid Body, bool Add);

[ByRefEvent]
public readonly record struct OrganEnableChangedEvent(bool Enabled);

[ByRefEvent]
public readonly record struct OrganEnabledEvent(EntityUid Organ);

[ByRefEvent]
public readonly record struct OrganDisabledEvent(EntityUid Organ);

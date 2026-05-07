using Content.Shared.Whitelist;

namespace Content.Shared.Projectiles;

/// <summary>
/// HardLight: Restricts which entities a projectile can collide with.
/// Targets that fail this filter are ignored as if they were not hit.
/// </summary>
[RegisterComponent]
public sealed partial class ProjectileTargetWhitelistComponent : Component
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}

// HardLight: Written for upstream compatibility.
// I'm leaving it in _FarHorizons for convenience in the event we ever need to remove FH code.

using System.Linq;
using Content.Shared.Medical.Healing;
using Content.Shared.Tag;

namespace Content.Shared._FarHorizons.Medical.ConditionalHealing;

/// <summary>
/// Shared helper for selecting and materializing conditional healing definitions.
/// Runtime interaction handling remains in gameplay healing systems.
/// </summary>
public sealed class ConditionalHealingSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public ConditionalHealingData? SelectBestMatch(Entity<ConditionalHealingComponent?> item, EntityUid target) =>
        !Resolve(item, ref item.Comp, false)
            ? null
            : item.Comp.HealingDefinitions
                .Where(p => _tag.HasAnyTag(target, p.AllowedTags))
                .Select(p => (ConditionalHealingData?) p.Healing)
                .FirstOrDefault((ConditionalHealingData?) null);

    public static HealingComponent MakeComponent(ConditionalHealingData data) =>
        new()
        {
            Damage = data.Damage,
            BloodlossModifier = data.BloodlossModifier,
            ModifyBloodLevel = data.ModifyBloodLevel,
            DamageContainers = data.DamageContainers,
            Delay = data.Delay,
            SelfHealPenaltyMultiplier = data.SelfHealPenaltyMultiplier,
            HealingBeginSound = data.HealingBeginSound,
            HealingEndSound = data.HealingEndSound,
        };
}

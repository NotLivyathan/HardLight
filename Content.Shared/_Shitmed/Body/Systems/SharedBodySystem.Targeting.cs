using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Targeting.Events;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Inventory;

// Namespace has set accessors, leaving it on the default.
namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    private readonly string[] _severingDamageTypes = { "Slash", "Piercing", "Blunt" };
    private static readonly ProtoId<DamageModifierSetPrototype> PartDamageSetId = "PartDamage";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryHead = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryTorso = "Torso";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryArmLeft = "ArmLeft";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryArmRight = "ArmRight";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryHandLeft = "HandLeft";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryHandRight = "HandRight";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryLegLeft = "LegLeft";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryLegRight = "LegRight";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryFootLeft = "FootLeft";
    private static readonly ProtoId<OrganCategoryPrototype> CategoryFootRight = "FootRight";

    private EntityQuery<TargetingComponent> _queryTargeting;
    private void InitializeIntegrityQueue()
    {
        _queryTargeting = GetEntityQuery<TargetingComponent>();
        SubscribeLocalEvent<BodyComponent, TryChangePartDamageEvent>(OnTryChangePartDamage);
        SubscribeLocalEvent<BodyComponent, DamageModifyEvent>(OnBodyDamageModify);
    }

    private void OnTryChangePartDamage(Entity<BodyComponent> ent, ref TryChangePartDamageEvent args)
    {
        // If our target has a TargetingComponent, that means they will take limb damage
        // And if their attacker also has one, then we use that part.
        if (_queryTargeting.TryComp(ent, out var targetEnt))
        {
            var damage = args.Damage;
            TargetBodyPart? targetPart = null;

            if (args.TargetPart != null)
            {
                targetPart = args.TargetPart;
            }
            else if (args.Origin.HasValue && _queryTargeting.TryComp(args.Origin.Value, out var targeter))
            {
                targetPart = targeter.Target;
                // If the target is Torso then have a 33% chance to hit another part
                if (targetPart.Value == TargetBodyPart.Torso)
                {
                    var additionalPart = GetRandomPartSpread(_random, 10);
                    targetPart = targetPart.Value | additionalPart;
                }
            }
            else
            {
                // If there's an origin in this case, that means it comes from an entity without TargetingComponent,
                // such as an animal, so we attack a random part.
                if (args.Origin.HasValue)
                {
                    targetPart = GetRandomBodyPart(ent, targetEnt);
                }
                // Otherwise we damage all parts equally (barotrauma, explosions, etc).
                else if (damage != null)
                {
                    // Division by 10 cuz damaging all parts by the same damage by default is too much.
                    damage /= 10;
                    targetPart = TargetBodyPart.All;
                }
            }

            if (targetPart == null)
                return;

            if (!TryChangePartDamage(ent, args.Damage, args.IgnoreResistances, args.CanSever, args.CanEvade, args.PartMultiplier, targetPart.Value)
                && args.CanEvade)
            {
                if (_net.IsServer)
                    _popup.PopupEntity(Loc.GetString("surgery-part-damage-evaded", ("user", Identity.Entity(ent, EntityManager))), ent);

                args.Evaded = true;
            }
        }
    }

    private void OnBodyDamageModify(Entity<BodyComponent> bodyEnt, ref DamageModifyEvent args)
    {
        if (args.TargetPart != null)
        {
            args.Damage *= GetPartDamageModifier(args.TargetPart.Value);
        }
    }

    private bool TryChangePartDamage(EntityUid entity,
        DamageSpecifier damage,
        bool ignoreResistances,
        bool canSever,
        bool canEvade,
        float partMultiplier,
        TargetBodyPart targetParts)
    {
        if (!TryComp(entity, out BodyComponent? body) || body.Organs == null)
            return false;

        var landed = false;
        var targets = SharedTargetingSystem.GetValidParts();

        foreach (var target in targets)
        {
            if (!targetParts.HasFlag(target))
                continue;

            foreach (var organ in GetOrgansForTargetPart(body, target))
            {
                if (canEvade && TryEvadeDamage(entity, GetEvadeChance(target)))
                    continue;

                var damageResult = _damageable.TryChangeDamage(organ, damage * partMultiplier, ignoreResistances, canSever: canSever);
                if (damageResult != null && damageResult.GetTotal() != 0)
                    landed = true;
            }
        }

        return landed;
    }

    private IEnumerable<EntityUid> GetOrgansForTargetPart(BodyComponent body, TargetBodyPart target)
    {
        foreach (var organ in body.Organs?.ContainedEntities ?? [])
        {
            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            if (!MatchesTargetPart(organComp, target))
                continue;

            yield return organ;
        }
    }

    private static bool MatchesTargetPart(OrganComponent organ, TargetBodyPart target)
    {
        if (organ.Category == null)
            return false;

        return target switch
        {
            TargetBodyPart.Head => organ.Category == CategoryHead,
            TargetBodyPart.Torso => organ.Category == CategoryTorso,
            TargetBodyPart.Groin => organ.Category == CategoryTorso,
            TargetBodyPart.LeftArm => organ.Category == CategoryArmLeft,
            TargetBodyPart.RightArm => organ.Category == CategoryArmRight,
            TargetBodyPart.LeftHand => organ.Category == CategoryHandLeft,
            TargetBodyPart.RightHand => organ.Category == CategoryHandRight,
            TargetBodyPart.LeftLeg => organ.Category == CategoryLegLeft,
            TargetBodyPart.RightLeg => organ.Category == CategoryLegRight,
            TargetBodyPart.LeftFoot => organ.Category == CategoryFootLeft,
            TargetBodyPart.RightFoot => organ.Category == CategoryFootRight,
            _ => false
        };
    }

    /// <summary>
    /// Gets the random body part rolling a number between 1 and 9, and returns
    /// Torso if the result is 9 or more. The higher torsoWeight is, the higher chance to return it.
    /// By default, the chance to return Torso is 50%.
    /// </summary>
    private static TargetBodyPart GetRandomPartSpread(IRobustRandom random, ushort torsoWeight = 9)
    {
        const int targetPartsAmount = 9;
        // 5 = amount of target parts except Torso
        return random.Next(1, targetPartsAmount + torsoWeight) switch
        {
            1 => TargetBodyPart.Head,
            2 => TargetBodyPart.RightArm,
            3 => TargetBodyPart.RightHand,
            4 => TargetBodyPart.LeftArm,
            5 => TargetBodyPart.LeftHand,
            6 => TargetBodyPart.RightLeg,
            7 => TargetBodyPart.RightFoot,
            8 => TargetBodyPart.LeftLeg,
            9 => TargetBodyPart.LeftFoot,
            _ => TargetBodyPart.Torso,
        };
    }

    public TargetBodyPart? GetRandomBodyPart(EntityUid uid, TargetingComponent? target = null)
    {
        if (!Resolve(uid, ref target))
            return null;

        var totalWeight = target.TargetOdds.Values.Sum();
        var randomValue = _random.NextFloat() * totalWeight;

        foreach (var (part, weight) in target.TargetOdds)
        {
            if (randomValue <= weight)
                return part;
            randomValue -= weight;
        }

        return TargetBodyPart.Torso; // Default to torso if something goes wrong
    }

    /// <summary>
    /// This should be called after body part damage was changed.
    /// </summary>
    /// <summary>
    /// Gets the integrity of all body parts in the entity.
    /// </summary>
    public Dictionary<TargetBodyPart, TargetIntegrity> GetBodyPartStatus(EntityUid entityUid)
    {
        var result = new Dictionary<TargetBodyPart, TargetIntegrity>();

        if (!TryComp<BodyComponent>(entityUid, out var body))
            return result;

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = TargetIntegrity.Severed;
        }

        foreach (var organ in body.Organs?.ContainedEntities ?? [])
        {
            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            var targetBodyPart = GetTargetBodyPart(organComp.Category);

            if (targetBodyPart != null)
                result[targetBodyPart.Value] = GetIntegrityForOrgan(organ);
        }

        // Hardcoded shitcode for Groin :)
        result[TargetBodyPart.Groin] = result[TargetBodyPart.Torso];

        return result;
    }

    public TargetBodyPart? GetTargetBodyPart(ProtoId<OrganCategoryPrototype>? category)
    {
        if (category == null)
            return null;

        if (category == CategoryHead)
            return TargetBodyPart.Head;
        if (category == CategoryTorso)
            return TargetBodyPart.Torso;
        if (category == CategoryArmLeft)
            return TargetBodyPart.LeftArm;
        if (category == CategoryArmRight)
            return TargetBodyPart.RightArm;
        if (category == CategoryHandLeft)
            return TargetBodyPart.LeftHand;
        if (category == CategoryHandRight)
            return TargetBodyPart.RightHand;
        if (category == CategoryLegLeft)
            return TargetBodyPart.LeftLeg;
        if (category == CategoryLegRight)
            return TargetBodyPart.RightLeg;
        if (category == CategoryFootLeft)
            return TargetBodyPart.LeftFoot;
        if (category == CategoryFootRight)
            return TargetBodyPart.RightFoot;

        return null;
    }

    private TargetIntegrity GetIntegrityForOrgan(EntityUid organ)
    {
        if (!TryComp<DamageableComponent>(organ, out var damageable))
            return TargetIntegrity.Healthy;

        var total = damageable.TotalDamage.Float();

        if (total <= 0f)
            return TargetIntegrity.Healthy;
        if (total <= 10f)
            return TargetIntegrity.LightlyWounded;
        if (total <= 20f)
            return TargetIntegrity.SomewhatWounded;
        if (total <= 40f)
            return TargetIntegrity.ModeratelyWounded;
        if (total <= 60f)
            return TargetIntegrity.HeavilyWounded;
        if (total <= 80f)
            return TargetIntegrity.CriticallyWounded;

        return TargetIntegrity.Dead;
    }

    /// <summary>
    /// Fetches the damage multiplier for part integrity based on part types.
    /// </summary>
    /// TODO: Serialize this per body part.
    public static float GetPartDamageModifier(TargetBodyPart partType)
    {
        return partType switch
        {
            TargetBodyPart.Head => 0.2f, // 20% damage, necks are hard to cut
            TargetBodyPart.Torso => 1.0f, // 100% damage
            TargetBodyPart.LeftArm => 0.7f, // 70% damage
            TargetBodyPart.RightArm => 0.7f,
            TargetBodyPart.LeftHand => 0.7f,
            TargetBodyPart.RightHand => 0.7f,
            TargetBodyPart.LeftLeg => 0.7f,
            TargetBodyPart.RightLeg => 0.7f,
            TargetBodyPart.LeftFoot => 0.7f,
            TargetBodyPart.RightFoot => 0.7f,
            _ => 0.5f
        };
    }

    /// <summary>
    /// Fetches the TargetIntegrity equivalent of the current integrity value for the body part.
    /// </summary>
    /// <summary>
    /// Fetches the chance to evade integrity damage for a body part.
    /// Used when the entity is not dead, laying down, or incapacitated.
    /// </summary>
    public static float GetEvadeChance(TargetBodyPart partType)
    {
        return partType switch
        {
            TargetBodyPart.Head => 0.70f,  // 70% chance to evade
            TargetBodyPart.LeftArm => 0.20f,
            TargetBodyPart.RightArm => 0.20f,
            TargetBodyPart.LeftHand => 0.20f,
            TargetBodyPart.RightHand => 0.20f,
            TargetBodyPart.LeftLeg => 0.20f,
            TargetBodyPart.RightLeg => 0.20f,
            TargetBodyPart.LeftFoot => 0.20f,
            TargetBodyPart.RightFoot => 0.20f,
            TargetBodyPart.Torso => 0f, // 0% chance to evade
            _ => 0f
        };
    }

    public bool CanEvadeDamage(EntityUid uid)
    {
        if (!TryComp<MobStateComponent>(uid, out var mobState)
            || !TryComp<StandingStateComponent>(uid, out var standingState)
            || _mobState.IsCritical(uid, mobState)
            || _mobState.IsDead(uid, mobState))
            return false;

        return true;
    }

    public bool TryEvadeDamage(EntityUid uid, float evadeChance)
    {
        if (!CanEvadeDamage(uid))
            return false;

        return _random.NextFloat() < evadeChance;
    }

}

using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Forensics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
// HardLight start: I'm a little confused by some of these...
using Content.Shared.Chemistry.Components;
using Solution = Content.Shared.Chemistry.Components.Solution;
using System;
using System.Text;
// HardLight end

namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
/// Handles feeding attempts both on yourself and on the target.
/// </summary>
[Obsolete("Migration to Content.Shared.Nutrition.EntitySystems.IngestionSystem is required")]
public sealed class FoodSystem : EntitySystem
{
    [Dependency] private readonly FlavorProfileSystem _flavorProfile = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public const float MaxFeedDistance = 1.0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FoodComponent, UseInHandEvent>(OnUseFoodInHand, after: new[] { typeof(OpenableSystem), typeof(InventorySystem) });
        SubscribeLocalEvent<FoodComponent, AfterInteractEvent>(OnFeedFood);

        SubscribeLocalEvent<FoodComponent, GetVerbsEvent<AlternativeVerb>>(AddEatVerb);

        SubscribeLocalEvent<FoodComponent, BeforeIngestedEvent>(OnBeforeFoodEaten);
        SubscribeLocalEvent<FoodComponent, IngestedEvent>(OnFoodEaten);
        SubscribeLocalEvent<FoodComponent, FullyEatenEvent>(OnFoodFullyEaten);

        SubscribeLocalEvent<FoodComponent, GetUtensilsEvent>(OnGetUtensils);

        SubscribeLocalEvent<FoodComponent, IsDigestibleEvent>(OnIsFoodDigestible);

        SubscribeLocalEvent<FoodComponent, EdibleEvent>(OnFood);

        SubscribeLocalEvent<FoodComponent, GetEdibleTypeEvent>(OnGetEdibleType);

        SubscribeLocalEvent<FoodComponent, BeforeFullySlicedEvent>(OnBeforeFullySliced);

        SubscribeLocalEvent<FoodComponent, ComponentStartup>(OnFoodStartup); // HardLight
    }

    /// <summary>
    /// Eat or drink an item
    /// </summary>
    private void OnUseFoodInHand(Entity<FoodComponent> entity, ref UseInHandEvent ev)
    {
        if (ev.Handled)
            return;

        ev.Handled = _ingestion.TryIngest(ev.User, ev.User, entity);
    }

    /// <summary>
    /// Feed someone else
    /// </summary>
    private void OnFeedFood(Entity<FoodComponent> entity, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        args.Handled = _ingestion.TryIngest(args.User, args.Target.Value, entity);
    }

    // HardLight start: I'm shoving this in here because I'm not sure where it should go yet. I'll fix it later.
    private void OnFoodStartup(EntityUid uid, FoodComponent component, ComponentStartup args)
    {
        if (!TryComp<StackComponent>(uid, out _))
            return;

        if (!_solutionContainer.TryGetSolution(uid, component.Solution, out _, out var solution))
            return;

        var stackSig = EnsureComp<StackSignatureComponent>(uid);
        var builder = new StringBuilder();
        var prototypeId = Prototype(uid)?.ID;
        if (prototypeId != null)
            builder.Append(prototypeId).Append('|');

        AppendSolutionSignature(builder, solution);

        var signature = builder.ToString();
        if (stackSig.Signature == signature)
            return;

        stackSig.Signature = signature;
        Dirty(uid, stackSig);
    }

    private static void AppendSolutionSignature(StringBuilder builder, Solution solution)
    {
        foreach (var reagent in solution.Contents.OrderBy(r => r.Reagent.Prototype, StringComparer.Ordinal))
        {
            builder.Append(reagent.Reagent.Prototype)
                .Append('=')
                .Append(reagent.Quantity.Value)
                .Append(';');
        }
    }
    // HardLight end

    private void AddEatVerb(Entity<FoodComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (entity.Owner == user || !args.CanInteract || !args.CanAccess)
            return;

        if (!_ingestion.TryGetIngestionVerb(user, entity, IngestionSystem.Food, out var verb))
            return;

        args.Verbs.Add(verb);
    }

    private void OnBeforeFoodEaten(Entity<FoodComponent> food, ref BeforeIngestedEvent args)
    {
        if (args.Cancelled || args.Solution is not { } solution)
            return;

        // Set it to transfer amount if it exists, otherwise eat the whole volume if possible.
        args.Transfer = food.Comp.TransferAmount ?? solution.Volume;
    }

    private void OnFoodEaten(Entity<FoodComponent> entity, ref IngestedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // HardLight start: I'm shoving this in here because I'm not sure where it should go yet. I'll fix it later.
        StackComponent? stackComp = null;
        if (TryComp<StackComponent>(entity, out var foundStack))
            stackComp = foundStack;

        var stackCount = stackComp?.Count ?? 1;
        var isStacked = stackCount > 1;
        var sourceSolution = isStacked ? new Solution(solution) : solution;
        var transferAmount = entity.Comp.TransferAmount != null
            ? FixedPoint2.Min((FixedPoint2) entity.Comp.TransferAmount, sourceSolution.Volume)
            : sourceSolution.Volume;

        var split = isStacked
            ? sourceSolution.SplitSolution(transferAmount)
            : _solutionContainer.SplitSolution(soln.Value, transferAmount);
        // HardLight end

        _audio.PlayPredicted(entity.Comp.UseSound, args.Target, args.User, AudioParams.Default.WithVolume(-1f).WithVariation(0.20f));

        var flavors = _flavorProfile.GetLocalizedFlavorsMessage(entity.Owner, args.Target, args.Split);

        if (args.ForceFed)
        {
            var targetName = Identity.Entity(args.Target, EntityManager);
            var userName = Identity.Entity(args.User, EntityManager);
            _popup.PopupEntity(Loc.GetString("edible-force-feed-success", ("user", userName), ("verb", _ingestion.GetProtoVerb(IngestionSystem.Food)), ("flavors", flavors)), entity, entity);

            _popup.PopupClient(Loc.GetString("edible-force-feed-success-user", ("target", targetName), ("verb", _ingestion.GetProtoVerb(IngestionSystem.Food))), args.User, args.User);

            // log successful forced feeding
            _adminLogger.Add(LogType.ForceFeed, LogImpact.Medium, $"{ToPrettyString(entity):user} forced {ToPrettyString(args.User):target} to eat {ToPrettyString(entity):food}");
        }
        else
        {
            _popup.PopupClient(Loc.GetString(entity.Comp.EatMessage, ("food", entity.Owner), ("flavors", flavors)), args.User, args.User);

            // log successful voluntary eating
            _adminLogger.Add(LogType.Ingestion, LogImpact.Low, $"{ToPrettyString(args.User):target} ate {ToPrettyString(entity):food}");
        }

        // BREAK OUR UTENSILS
        if (_ingestion.TryGetUtensils(args.User, entity, out var utensils))
        {
            foreach (var utensil in utensils)
            {
                _ingestion.TryBreak(utensil, args.User);
            }

        // HardLight start: I'm shoving this in here because I'm not sure where it should go yet. I'll fix it later.
        args.Repeat = !forceFeed;

        if (stackComp != null && stackComp.Count > 1)
        {
            // Consume a single item from the stack without altering remaining reagents.
            _stack.SetCount(entity.Owner, stackComp.Count - 1);
            return;
        }
        // HardLight end

        if (_ingestion.GetUsesRemaining(entity, entity.Comp.Solution, args.Split.Volume) > 0)
        {
            // Leave some of the consumer's DNA on the consumed item...
            var ev = new TransferDnaEvent
            {
                Donor = args.Target,
                Recipient = entity,
                CanDnaBeCleaned = false,
            };
            RaiseLocalEvent(args.Target, ref ev);

            args.Repeat = !args.ForceFed;
            return;
        }

        // Food is always destroyed...
        args.Destroy = true;
    }

    private void OnFoodFullyEaten(Entity<FoodComponent> food, ref FullyEatenEvent args)
    {
        if (food.Comp.Trash.Count == 0)
            return;

        var position = _transform.GetMapCoordinates(food);
        var trashes = food.Comp.Trash;
        var tryPickup = _hands.IsHolding(args.User, food, out _);

        foreach (var trash in trashes)
        {
            var spawnedTrash = EntityManager.PredictedSpawn(trash, position);

            // If the user is holding the item
            if (tryPickup)
            {
                // Put the trash in the user's hand
                _hands.TryPickupAnyHand(args.User, spawnedTrash);
            }
        }
    }

    private void OnFood(Entity<FoodComponent> food, ref EdibleEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Cancelled || args.Solution != null)
            return;

        if (food.Comp.UtensilRequired && !_ingestion.HasRequiredUtensils(args.User, food.Comp.Utensil))
        {
            args.Cancelled = true;
            return;
        }

        // Check this last
        _solutionContainer.TryGetSolution(food.Owner, food.Comp.Solution, out args.Solution);
        args.Time += TimeSpan.FromSeconds(food.Comp.Delay);
    }

    private void OnGetUtensils(Entity<FoodComponent> entity, ref GetUtensilsEvent args)
    {
        if (entity.Comp.Utensil == UtensilType.None)
            return;

        if (entity.Comp.UtensilRequired)
            args.AddRequiredTypes(entity.Comp.Utensil);
        else
            args.Types |= entity.Comp.Utensil;
    }

    // TODO: When DrinkComponent and FoodComponent are properly obseleted, make the IsDigestionBools in IngestionSystem private again.
    private void OnIsFoodDigestible(Entity<FoodComponent> ent, ref IsDigestibleEvent args)
    {
        if (ent.Comp.RequireDead && _mobState.IsAlive(ent))
            return;

        args.AddDigestible(ent.Comp.RequiresSpecialDigestion);
    }

    private void OnGetEdibleType(Entity<FoodComponent> ent, ref GetEdibleTypeEvent args)
    {
        if (args.Type != null)
            return;

        args.SetPrototype(IngestionSystem.Food);
    }

    private void OnBeforeFullySliced(Entity<FoodComponent> food, ref BeforeFullySlicedEvent args)
    {
        if (food.Comp.Trash.Count == 0)
            return;

        var position = _transform.GetMapCoordinates(food);
        var trashes = food.Comp.Trash;
        var tryPickup = _hands.IsHolding(args.User, food, out _);

        foreach (var trash in trashes)
        {
            var spawnedTrash = EntityManager.PredictedSpawn(trash, position);

            // If the user is holding the item
            if (tryPickup)
            {
                // Put the trash in the user's hand
                _hands.TryPickupAnyHand(args.User, spawnedTrash);
            }
        }
    }
}

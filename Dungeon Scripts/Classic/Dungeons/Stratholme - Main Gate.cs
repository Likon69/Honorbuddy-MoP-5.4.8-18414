

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class StratholmeMainGate : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 40; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(3393.185, -3359.307, 142.7716); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(3393.08, -3356.25, 142.25); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var p in units)
			{
				var unit = p.Object.ToUnit();
				if (unit.Entry == EyeOfNaxxramasId)
					p.Score += 1000;
			}
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var myLoc = Me.Location;
			foreach (var unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				if (unit.Entry == EyeOfNaxxramasId && Me.Combat && unit.Location.DistanceSqr(myLoc) < 30 * 30)
				{
					outgoingunits.Add(unit);
				}

				else if ((unit.Entry == PlagueRat || unit.Entry == PlaguedInsectId) && Me.Combat && unit.Location.DistanceSqr(myLoc) < 40 * 40)
				{
					if ((unit.CurrentTargetGuid == Me.Guid || unit.IsWithinMeleeRange) && myLoc.DistanceSqr(_inbetweenGateLoc) < 12 * 12 && _ratGateIsDown || !_ratGateIsDown)
						outgoingunits.Add(unit);
				}
				else if (unit.Entry == HearthsingerForrestenId && !Me.Combat && Me.IsTank() && DungeonBuddySettings.Instance.KillOptionalBosses)
				{
					outgoingunits.Add(unit);
				}
			}
		}

		#endregion

		#region Root

		private const uint HearthsingerForrestenId = 10558;
		private const uint EyeOfNaxxramasId = 10411;
		private const uint PlagueRat = 10441;
		private const uint PlaguedInsectId = 10461;
		private WoWPoint _inbetweenGateLoc = new WoWPoint(3610.87, -3334.26, 124.1506);

		private bool _ratGateIsDown;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(45200, "Crusade Commander Eligor Dawnbringer", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(45201, "Master Craftsman Wilhelm", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[LocationHandler(3610.87, -3334.26, 124.1506, 40, "Kill loads of rats.")]
		public Composite RatLocationHandler()
		{
			return new PrioritySelector();
		}

		[ObjectHandler(175351, "Doodad_SmallPortcullis03", ObjectRange = 35)]
		public Composite Doodad_SmallPortcullis03Handler()
		{
			var moveInBetweengGatesTimer = new WaitTimer(TimeSpan.FromMinutes(3));
			var stayPutTimer = new WaitTimer(TimeSpan.FromSeconds(5));
			var waitLoc = new WoWPoint(3595.56, -3335.506, 126.1457);

			WoWGameObject gate = null;
			return new PrioritySelector(
				ctx =>
				{
					gate = ctx as WoWGameObject;
					_ratGateIsDown = gate.State == WoWGameObjectState.Ready;
					return gate;
				},
				// move in-between the 2 gates where the rats spawn.
				new Decorator(
					ctx =>
					Targeting.Instance.IsEmpty() && moveInBetweengGatesTimer.IsFinished && gate.State == WoWGameObjectState.Active &&
					_inbetweenGateLoc.Distance(Me.Location) > 4,
				// tank should wait for party members to start running in.
					new PrioritySelector(
						new Decorator(
							ctx => Me.IsTank() && Me.GroupInfo.IsInParty && !Me.PartyMembers.Any(p => p.Location.Distance(_inbetweenGateLoc) < 10),
							new PrioritySelector(
								new Decorator(ctx => Me.Location.Distance(waitLoc) > 4, new Action(ctx => Navigator.MoveTo(waitLoc))), new ActionAlwaysSucceed())),
						new Action(ctx => Navigator.MoveTo(_inbetweenGateLoc)))),
				// reset the timers once bot is in position.
				new Decorator(
					ctx => _inbetweenGateLoc.Distance(Me.Location) <= 4 && moveInBetweengGatesTimer.IsFinished,
					new Sequence(new Action(ctx => moveInBetweengGatesTimer.Reset()), new Action(ctx => stayPutTimer.Reset()))),
				// wait for the rats to spawn.. Targeting is handled in target filter
				new Decorator(ctx => !stayPutTimer.IsFinished && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[ObjectHandler(176325, "Blacksmithing Plans", ObjectRange = 15)]
		public Composite BlacksmithingPlansHandler()
		{
			WoWGameObject plans = null;
			PlayerQuest quest = null;
			const int cuttingTheCompetitionQuestId = 27185;

			return new PrioritySelector(
				ctx => plans = ctx as WoWGameObject,
				new Decorator(
					ctx => (quest = Me.QuestLog.GetQuestById(cuttingTheCompetitionQuestId)) != null && !quest.IsCompleted && !Me.Combat && !plans.InUse,
					new PrioritySelector(
						new Decorator(ctx => plans.WithinInteractRange, new Action(ctx => plans.Interact())),
						new Decorator(ctx => !plans.WithinInteractRange, new Action(ctx => Navigator.MoveTo(plans.Location))))));
		}

		[ObjectHandler(177287, "Unfinished Painting", ObjectRange = 25)]
		public Composite UnfinishedPaintingHandler()
		{
			WoWGameObject painting = null;
			PlayerQuest quest = null;
			const int ofLoveAndFamilyQuestId = 27305;

			return new PrioritySelector(
				ctx => painting = ctx as WoWGameObject,
				new Decorator(
					ctx =>
					(quest = Me.QuestLog.GetQuestById(ofLoveAndFamilyQuestId)) != null && !quest.IsCompleted && !Me.Combat &&
					!ScriptHelpers.WillPullAggroAtLocation(painting.Location) && !painting.InUse,
					new PrioritySelector(
						new Decorator(
							ctx => painting.WithinInteractRange,
							new Sequence(new Action(ctx => painting.Interact()), new WaitContinue(4, ctx => false, new ActionAlwaysSucceed()))),
						new Decorator(ctx => !painting.WithinInteractRange, new Action(ctx => Navigator.MoveTo(painting.Location))))));
		}

		#endregion

		#region The Unforgiven

		[EncounterHandler(10516, "The Unforgiven")]
		public Composite TheUnforgivenFight()
		{
			const int unrelentingAnguishId = 122832;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, unrelentingAnguishId));
		}

		#endregion

		#region Skul

		[EncounterHandler(10393, "Skul")]
		public Composite SkulFight()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion

		#region Timmy the Cruel

		[EncounterHandler(10808, "Timmy the Cruel")]
		public Composite TimmyTheCruelFight()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelEnemy("Enrage", ScriptHelpers.EnemyDispelType.Enrage, ctx => boss));
		}

		#endregion

		#region Commander Malor

		[EncounterHandler(11032, "Commander Malor")]
		public Composite CommanderMalorFight()
		{
			WoWUnit boss = null;
			const int shadowBoltVolleyId = 15245;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, shadowBoltVolleyId));
		}

		#endregion

		#region Willey Hopebreaker

		private readonly WaitTimer _canonTimer = new WaitTimer(TimeSpan.FromSeconds(10));

		[EncounterHandler(10997, "Willey Hopebreaker")]
		public Composite WilleyHopebreakerFight()
		{
			WoWUnit boss = null;
			WoWGameObject cannonBallStack = null;
			WoWGameObject cannon = null;
			const uint cannonBallStackId = 176215;
			const uint cannonId = 176216;
			const uint cannonBallId = 12973;
			bool shouldFireCannon = false;

			var tankLoc = new WoWPoint(3569.718, -2939.918, 125.0002);
			var cannonBallStackLoc = new WoWPoint(3574.597, -2935.417, 125.0017);
			return new PrioritySelector(
				ctx =>
				{
					shouldFireCannon =
						Me.GroupInfo.RaidMembers.Where(o => o.HasRole(WoWPartyMember.GroupRole.Damage) && o.ToPlayer() != null && o.ToPlayer().IsAlive).Select(o => o.ToPlayer()).OrderBy(o => o.MaxHealth).FirstOrDefault() == Me;
					if (shouldFireCannon)
					{
						cannonBallStack =
							ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Entry == cannonBallStackId).OrderBy(
								o => o.Location.DistanceSqr(cannonBallStackLoc)).FirstOrDefault();
						cannon = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == cannonId);
					}
					return boss = ctx as WoWUnit;
				},
				new Decorator(ctx => boss.CurrentTargetGuid == Me.Guid && !_canonTimer.IsFinished, ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 4)),
				new Decorator(
					ctx => shouldFireCannon && _canonTimer.IsFinished,
					new PrioritySelector(
						new Decorator(
							ctx => Me.BagItems.All(i => i.Entry != cannonBallId),
							new PrioritySelector(
								new Decorator(ctx => cannonBallStack.WithinInteractRange, new Action(ctx => cannonBallStack.Interact())),
								new Decorator(ctx => !cannonBallStack.WithinInteractRange, new Action(ctx => Navigator.MoveTo(cannonBallStack.Location))))),
						new Decorator(
							ctx => Me.BagItems.Any(i => i.Entry == cannonBallId),
							new PrioritySelector(
								new Decorator(ctx => cannon.WithinInteractRange, new Sequence(
								   new DecoratorContinue(ctx => Me.IsMoving,
									   new Action(ctx => WoWMovement.MoveStop())),
									new WaitContinue(2, ctx => !Me.IsMoving, new ActionAlwaysSucceed()),
									new Action(ctx => cannon.Interact()),
									new WaitContinue(2, ctx => false, new ActionAlwaysSucceed()),
									new Action(ctx => _canonTimer.Reset()))),
								new Decorator(ctx => !cannon.WithinInteractRange, new Action(ctx => Navigator.MoveTo(cannon.Location))))))));
		}

		#endregion

		#region Instructor Galford

		[EncounterHandler(10811, "Instructor Galford")]
		public Composite InstructorGalfordFight()
		{
			WoWUnit boss = null;
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, 10, o => o == boss && boss.CurrentTargetGuid != Me.Guid && boss.IsAlive);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelGroup("Burning Winds", ScriptHelpers.PartyDispelType.Magic));
		}

		#endregion

		#region Balnazzar

		[EncounterHandler(10813, "Balnazzar")]
		public Composite BalnazzarFight()
		{
			const uint balnazzarId = 10813;

			WoWUnit boss = null;
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, 8, o => o.Entry == balnazzarId && o.ToUnit().CurrentTargetGuid != Me.Guid && o.ToUnit().IsAlive);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Sleep", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateDispelGroup("Psychic Scream", ScriptHelpers.PartyDispelType.Magic));
		}

		#endregion
	}
}
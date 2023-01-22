
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class UngaIngoo : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 499; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == JibberwingRiderId && (unit.TransportGuid > 0 || ScriptHelpers.IsBossAlive("Captain Ook")))
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if ((unit.Entry == SwallowedBrewstealerId || unit.Entry == UngaBrewstealerId) && unit.HasAura("Stealing Brew") && Me.IsTank())
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null) { }
			}
		}

		#endregion

		private const int BrewmasterBoId = 62467;
		private const uint StolenUngaBrewKegId = 63028;
		private const uint SwallowedBrewstealerId = 66402;
		private const uint JibberwingRiderId = 67029;
		const uint UngaBrewstealerId = 62508;

		readonly List<Blackspot> _mugBlackspots = new List<Blackspot>()
		{
			new Blackspot(new WoWPoint(-3006.032, 943.6677, -20),50,30), // ship
			new Blackspot(new WoWPoint(-2908.807, 928.8163, 10.04452),10,0) // huy
		};


		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return
				new PrioritySelector(
					new Decorator(
						ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 2 && ScriptHelpers.IsBossAlive("Unga Bird-Haver"),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Unga Bird-Haver"))),
					new Decorator(
						ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 3 && ScriptHelpers.IsBossAlive("Ba-Bam"),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Ba-Bam"))));
		}

		[EncounterHandler(62467, "Brewmaster Bo", Mode = CallBehaviorMode.Proximity)]
		public Composite BrewmasterBoEncounter()
		{
			const int bosRejuvinatingMistId = 126667;
			WoWUnit unit = null;

			var escortStartLoc = new WoWPoint(-3086.776, 750.25, 1.252403);
			var escortEndLoc = new WoWPoint(-2992.632, 852.3806, 16.41096);
			WoWAreaTrigger rejuvMist = null;
			WoWUnit myCachedTarget;

			return new PrioritySelector(
				ctx =>
				{
					rejuvMist =
						ObjectManager.GetObjectsOfType<WoWAreaTrigger>().Where(a => a.SpellId == bosRejuvinatingMistId).OrderBy(u => u.DistanceSqr).FirstOrDefault();
					return unit = ctx as WoWUnit;
				},
				new Decorator(ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 1, ScriptHelpers.CreateTankTalkToThenEscortNpc(BrewmasterBoId, escortStartLoc, escortEndLoc)),
				//                new Decorator( /
				//                  ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 1, ScriptHelpers.CreateTankTalkToThenEscortNpc(BrewmasterBoId, escortStartLoc, escortEndLoc)),
				// if we need to heal up then move to a nearby rejuvinating mist.
				new Decorator(
					ctx => Me.HealthPercent < 60 && rejuvMist != null && rejuvMist.Distance > 3,
						   new PrioritySelector(
						new Decorator(
							ctx => Me.Location.Distance(rejuvMist.Location) > 3,
							new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(rejuvMist.Location)))),
						new Decorator(
							ctx => Me.Location.Distance(rejuvMist.Location) <= 3,
				// we want to stay inside the healing circle until healed.. but if we have a target within range 
				// then continue down the behavior tree to allow Combat routine to run.
							new PrioritySelector(
								new Decorator(
									ctx =>
									(myCachedTarget = Me.CurrentTarget) == null ||
									(Me.IsMelee() && !myCachedTarget.IsWithinMeleeRange || Me.IsRange() && !Me.IsHealer() && myCachedTarget.Distance > 40),
									new ActionAlwaysSucceed()))))));
		}

		[ScenarioStage(2)]
		public Composite CreateStageTwoBehavior()
		{
			var kegs = new uint[] { StolenUngaBrewKegId, 212248, 212251, 212252, 212253, 212254, 212255, 212256, 212273, 212278, };
			var defendLoc = new WoWPoint(-2924.799, 769.6967, 2.793367);
			var brewTurninLoc = new WoWPoint(-2937.843, 767.4191, 4.046346);
			WoWObject keg = null;
			//const uint inactiveBeachBombId = 212293; // WoWGameObject. throw these bombs into the path of the invaders. don't use them all at once.
			//const uint beachBombId = 212290; // WoWGameObject. active bomb
			//const uint brewDefenderId = 62705; // WowUnit. Cannon
			//const uint burningBlazeId = 131781;
			var kegSearchPath = new CircularQueue<WoWPoint> { new WoWPoint(-2827.162, 930.8008, 1.861316), new WoWPoint(-2834.043, 501.5092, 7.446692) };

			// ToDo Add defend logic.
			return new PrioritySelector(
				ctx =>
				{
					keg =
						ObjectManager.ObjectList.Where(
							u => kegs.Contains(u.Entry) && !IsInBlackspot(u.Location) &&
								Navigator.CanNavigateFully(StyxWoW.Me.Location, u.Location) &&
								(u is WoWUnit && u.ToUnit().TransportGuid == 0 || u is WoWGameObject && u.ToGameObject().CanUse())).OrderBy(
								u => u.DistanceSqr).FirstOrDefault();
					return ctx;
				},
				// return kegs to Brewmaster Bo
				new Decorator(
					ctx =>
					!Me.Combat && Me.HasAura("Unga Jungle Brew Collected") &&
					(Me.HasAura("Encumbered") ||
					 ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(1).TotalQuantity - ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(1).CurrentQuantity <=
						// todo: use this when DB 1.70 + goes live.
						// ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(1).TotalQuantity - ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(1).CurrentQuantity <= 
					 Me.Auras["Unga Jungle Brew Collected"].StackCount),
					new Action(ctx => Navigator.MoveTo(brewTurninLoc))),
				// collect any kegs
				new Decorator(
					ctx => keg != null && !Me.Combat && !Me.HasAura("Encumbered"),
					new PrioritySelector(
						new Decorator(ctx => keg.Distance >= 40 && Me.IsTank() && Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(keg.Location))),
						new Decorator(ctx => keg.Distance < 40, ScriptHelpers.CreateInteractWithObject(ctx => keg, 3)))),
				// go searching for kegs.
				new Decorator(
					ctx => keg == null && Targeting.Instance.IsEmpty() && !Me.HasAura("Encumbered") && StyxWoW.Me.IsTank(),
					new PrioritySelector(
						new Decorator(ctx => kegSearchPath.Peek().Distance(Me.Location) < 5, new Action(ctx => kegSearchPath.Dequeue())),
						new Action(ctx => Navigator.MoveTo(kegSearchPath.Peek())))));
		}

		bool IsInBlackspot(WoWPoint loc)
		{
			foreach (var mugBlackspot in _mugBlackspots)
			{
				if (mugBlackspot.Location.Distance2D(loc) < mugBlackspot.Radius &&
					(mugBlackspot.Height == 0 || loc.Z >= mugBlackspot.Location.Z && loc.Z < mugBlackspot.Location.Z + mugBlackspot.Height))
					return true;
			}
			return false;
		}

		[EncounterHandler(62465, "Captain Ook")]
		public Composite CaptainOokEncounter()
		{
			WoWUnit boss = null;
			const uint scurvyCuringOrangeId = 212261;
			WoWGameObject orange = null;
			const int gonnaOokYaId = 121865;
			const int gettingSmashedId = 121883;
			//const int StealOrangeId = 121940;

			return new PrioritySelector(
				ctx =>
				{
					orange = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(u => u.Entry == scurvyCuringOrangeId && u.CanUse());
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => orange != null,
					new PrioritySelector(
						new Decorator(ctx => orange.WithinInteractRange,
							new Sequence(
								new Action(ctx => orange.Interact()),
								new Action(ctx => WoWMovement.MoveStop()),
								new WaitContinue(3, ctx => Lua.GetReturnVal<bool>("return ExtraActionButton1:IsVisible()", 0), new ActionAlwaysSucceed()),
								new Action(ctx => Lua.DoString("ExtraActionButton1:Click()")),
								new Action(ctx => SpellManager.ClickRemoteLocation(WoWMathHelper.CalculatePointFrom(boss.Location, Me.Location, -39)))
								)),
						new Decorator(ctx => !orange.WithinInteractRange, new Action(ctx => Navigator.MoveTo(orange.Location))))),
				/*
					new Decorator(
						ctx => Me.HasAura("Fixate on Orange Carrier"),
						new PrioritySelector(
							new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							new Decorator(
								ctx => !Me.IsCasting,
								new Action(
									ctx =>
									{
										Logger.Write("Throwing Orange away from Captain Ook");
										// throw orange away from Ook.
										Lua.DoString("ExtraActionButton1:Click()");
										SpellManager.ClickRemoteLocation(WoWMathHelper.CalculatePointFrom(boss.Location, Me.Location, -39));
									})),
							   new Decorator(
								ctx => Me.IsCasting,
								new ActionAlwaysSucceed())
										)), */
				// avoid standing in front of Captain Ook when he's casting 'Gonna Ook Ya!'
				ScriptHelpers.GetBehindUnit(ctx => boss.CastingSpellId == gonnaOokYaId && boss.IsFacing(Me) && boss.Distance < 10, ctx => boss),
				ScriptHelpers.CreateInterruptCast(ctx => boss, gettingSmashedId));
		}
	}
}
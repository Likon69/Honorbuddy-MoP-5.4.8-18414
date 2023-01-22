using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Routines;
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

	#region Normal Difficulty

	public class BrewingStorm : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 517; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						// ignore these while they're scaling cliffs
						if ((unit.Entry == ViletongueDecimatorId || unit.Entry == ViletongueSkirmisherId || unit.Entry == ViletongueWarriorId) && unit.TransportGuid > 0)
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
					WoWUnit target;
					if (unit.Combat && (target = unit.CurrentTarget) != null && target.Entry == BrewmasterBlancheId)
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

		private const uint BrewkegId = 58916;
		private const uint BrewmasterBlancheId = 58740;
		private const uint ViletongueSkirmisherId = 58738;
		private const uint ViletongueDecimatorId = 59632;
		private const uint ViletongueWarriorId = 58737;

		protected static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		[EncounterHandler(58740, "Brewmaster Blanche", Mode = CallBehaviorMode.Proximity)]
		public Composite BrewmasterBlancheEncounter()
		{
			return
				new PrioritySelector(
					new Decorator<WoWUnit>(unit => unit.CanGossip && Me.RaidMembers.All(r => r.IsAlive), ScriptHelpers.CreateTalkToNpc((int)BrewmasterBlancheId)));
		}


		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector(HealBehavior());
		}

		private Composite HealBehavior()
		{
			const uint healingCircleId = 114663;
			WoWUnit target = null;
			WoWObject healObject = null;

			return
				new Decorator(
					ctx =>
					Me.HealthPercent < 60 &&
					(healObject = ObjectManager.ObjectList.Where(o => o.Entry == healingCircleId && o.Distance <= 40).OrderBy(o => o.DistanceSqr).FirstOrDefault()) != null,
					new PrioritySelector(
						new Decorator(
							ctx => Me.Location.Distance(healObject.Location) > 3,
							new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(healObject.Location)))),
						new Decorator(
							ctx => Me.Location.Distance(healObject.Location) <= 3,
				// we want to stay inside the healing circle until healed.. but if we have a target within range 
				// then continue down the behavior tree to allow Combat routine to run.
							new PrioritySelector(
								new Decorator(
									ctx =>
									(target = Me.CurrentTarget) == null ||
									(Me.IsMelee() && !target.IsWithinMeleeRange || Me.IsRange() && !Me.IsHealer() && target.Distance > 35),
									new ActionAlwaysSucceed())))));
		}


		[ScenarioStage(1, "Make Boomer' Brew")]
		public Composite StageOneEncounter()
		{
			const uint davesIndustrialLightandMagicBunnyId = 38821;

			AddAvoidObject(ctx => true, 8, u => u.Entry == davesIndustrialLightandMagicBunnyId && u.ToUnit().HasAura("Lightning Channel"));

			WoWUnit blanche = null;
			var encounterLoc = new WoWPoint(2249.904, -1132.496, 485.9289);
			WoWUnit kegOnFire = null;

			return new PrioritySelector(
				ctx =>
				{
					blanche = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BrewmasterBlancheId);
					kegOnFire =
						ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(
										 u =>
										 u.Entry == BrewkegId && u.HasAura("On Fire") && !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(u.Location)) &&
										 !Me.PartyMembers.Any(p => !p.IsMe && p.IsSafelyFacing(u, 45) && p.Location.DistanceSqr(u.Location) < Me.Location.DistanceSqr(u.Location)))
									 .OrderBy(u => u.DistanceSqr)
									 .FirstOrDefault();

					return ctx;
				},
				new Decorator(
					ctx =>
					kegOnFire != null &&
					(Me.HealthPercent >= 80 && Targeting.Instance.IsEmpty() ||
					 (LfgDungeon.DifficultyId > 1 && !Me.IsHealer() || ScriptHelpers.GroupMembers.All(g => g.HealthPercent > 60))),
					new PrioritySelector(
						new Decorator(ctx => !kegOnFire.WithinInteractRange, new Action(ctx => Navigator.MoveTo(kegOnFire.Location))),
						new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
						new Action(ctx => kegOnFire.Interact()))),
				new Decorator(ctx => blanche != null && !Me.Combat && blanche.CanGossip, ScriptHelpers.CreateTalkToNpc(ctx => blanche)),
				// stay close to encounter.
				new Decorator(ctx => Targeting.Instance.IsEmpty() && Me.Location.Distance(encounterLoc) > 30, new Action(ctx => Navigator.MoveTo(encounterLoc))),
				// don't move to next boss before event is over.
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty(),
					new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())));
		}

		[ScenarioStage(2, "Road to Thunderpaw")]
		public Composite StageTwoEncounter()
		{
			var escortMoveLoc = new WoWPoint(2179.667, -1016.134, 450.7713);
			return new PrioritySelector(
				ctx => ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BrewmasterBlancheId),
				new Decorator<WoWUnit>(
					unit => Me.IsTank() && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator<WoWUnit>(
							unit => unit.Distance > 20, new Helpers.Action<WoWUnit>(unit => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(unit.Location)))),
				// since I'm forcing tank/leader to follow this NPC I need to also make sure he stays alive since there might be no healer in party
						new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior),
						new ActionAlwaysSucceed())),
				// move to escort to location if blanche is nowhere to be found.
				new Decorator(ctx => Me.IsTank() && ctx == null, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(escortMoveLoc))));
		}

		public Composite StageThreeEncounter()
		{
			var escortMoveLoc = new WoWPoint(2179.667, -1016.134, 450.7713);
			return
				new PrioritySelector(
					new Decorator(
						ctx => Me.IsTank() && Targeting.Instance.IsEmpty(),
						new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(escortMoveLoc)))));
		}

		[EncounterHandler(58739, "Borokhula the Destroyer")]
		public Composite BorokhulaTheDestroyerEncounter()
		{
			const int swampSmashId = 115013;
			const int earthShatteringMissileId = 122143;
			WoWUnit myTargetCache;
			WoWUnit boss = null;

			AddAvoidLocation(ctx => true, 5, m => ((WoWMissile)m).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellId == earthShatteringMissileId));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => boss.CastingSpellId == swampSmashId && boss.Distance < 20, ctx => boss, new ScriptHelpers.AngleSpan(0, 160)),
				new Decorator(
					ctx =>
					(myTargetCache = Me.CurrentTarget) != null && myTargetCache != boss && myTargetCache.IsHostile &&
					!Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0),
					new Sequence(new Action(ctx => Logger.Write("Using Boomers Brew Power Button")), new Action(ctx => Lua.DoString("ExtraActionButton1:Click()")))));
		}
	}

	#endregion

	#region Heroic Difficulty

	public class BrewingStormHeroic : BrewingStorm
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 639; }
		}

		#endregion


		[EncounterHandler(0)]
		public new Composite RootEncounter()
		{
			const uint torchTossId = 142562;
			const uint venomCloudId = 142561;
			AddAvoidObject(ctx => true, 3, torchTossId, venomCloudId);

			return new PrioritySelector(base.RootEncounter());
		}

		[EncounterHandler(59632, "Champion Sithiss")]
		public Composite ChampionSithissEncounter()
		{
			var tankLoc = new WoWPoint(2254.339, -1192.018, 425.4802);
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// tank boss away from edge
				new Decorator(ctx => boss.CurrentTargetGuid == Me.Guid && Me.Location.Distance(tankLoc) > 4, new Action(ctx => Navigator.MoveTo(tankLoc))));
		}


	}

	#endregion

}
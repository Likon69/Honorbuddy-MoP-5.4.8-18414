using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class DireMaulCapitalGardens : Dungeon
	{
		#region Overrides of Dungeon

		private const uint EldrethDarter = 14398;
		private readonly WoWPoint _immoltharExitLoc = new WoWPoint(-40.97228, 760.8812, -30.83465);
		private readonly WoWPoint _immoltharLoc = new WoWPoint(-38.08067, 812.4398, -29.53583);

		public override uint DungeonId
		{
			get { return 36; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-3817.948, 1250.378, 160.2729); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-51.30532, 160.3396, -3.458581); }
		}
		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				// kill these before engaging 1st boss because they will aid him.
				if (unit.Entry == IronbarkProtectorId && !Me.Combat && Me.IsTank() && ScriptHelpers.IsBossAlive("Tendris Warpwood"))
				{
					outgoingunits.Add(unit);
				}
				else if ((unit.Entry == ArcaneAberrationId || unit.Entry == ManaRemnantId) && Me.IsTank() && !Me.Combat && unit.DistanceSqr < 40*40 )
				{
					var pathDist = unit.Location.PathDistance(Me.Location, 40f);
					if (pathDist.HasValue && pathDist.Value < 40f)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var p in units)
			{
				WoWUnit unit = p.Object.ToUnit();
				switch (unit.Entry)
				{
					case IronbarkProtectorId:
						p.Score += 100;
						break;
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				u =>
				{
					var unit = u.ToUnit();
					if (unit.Entry == EldrethDarter && !unit.Combat)
						return true;
					return false;
				});
		}

		public override void OnEnter()
		{
			_generator1IsDisabled = _generator2IsDisabled = _generator3IsDisabled = _generator4IsDisabled = _generator5IsDisabled = false;
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			var immolatharDistSqr = StyxWoW.Me.Location.DistanceSqr(_immoltharLoc);
			// entering the center area
			if (location.DistanceSqr(_immoltharLoc) <= 40 * 40)
			{
				if (immolatharDistSqr < 60 * 60)
				{
					Navigator.PlayerMover.MoveTowards(location);
					return MoveResult.Moved;
				}
				else return Navigator.MoveTo(_immoltharExitLoc);
			}
			// exiting the center area.
			else if (immolatharDistSqr < 60 * 60 && location.DistanceSqr(_immoltharLoc) >= 50 * 50)
			{
				if (immolatharDistSqr < 50 * 50)
				{
					if (StyxWoW.Me.Location.DistanceSqr(_immoltharExitLoc) <= 8 * 8)
						WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					Navigator.PlayerMover.MoveTowards(_immoltharExitLoc);
					return MoveResult.Moved;
				}
				else if (StyxWoW.Me.MovementInfo.IsAscending)
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
			}

			return base.MoveTo(location);
		}

		#endregion

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(44991, "Estulan", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(44999, "Shen'dralar Watcher", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(14358, "Shen'dralar Ancient", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx =>
					{
						if (Me.Combat || unit.QuestGiverStatus != QuestGiverStatus.Available)
							return false;

						if ( ScriptHelpers.WillPullAggroAtLocation(unit.Location))
							return false;

						var pathDist =  Me.Location.PathDistance(unit.Location, 40f);
						return pathDist.HasValue && pathDist.Value < 40f;
					}, 
					ScriptHelpers.CreatePickupQuest(ctx => unit)),

				new Decorator(
					ctx =>
					{
						if (Me.Combat || unit.QuestGiverStatus != QuestGiverStatus.TurnIn)
							return false;

						if (ScriptHelpers.WillPullAggroAtLocation(unit.Location))
							return false;

						var pathDist = Me.Location.PathDistance(unit.Location, 40f);
						return pathDist.HasValue && pathDist.Value < 40f;
					}, 
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		#region Force Field

		private const uint ForceFieldId = 179503;
		private const int ArcaneAberrationId = 11480;
		private const int ManaRemnantId = 11483;

		private readonly Dictionary<string, WoWPoint> _generatorLocations = new Dictionary<string, WoWPoint>
		{
			{"Crystal Generator 1", new WoWPoint(3.172923, 274.9728, -8.346642)},
			{"Crystal Generator 2", new WoWPoint(-81.00807, 442.6851, 28.60135)},
			{"Crystal Generator 3", new WoWPoint(113.8273, 435.8742, 28.60132)},
			{"Crystal Generator 4", new WoWPoint(66.86565, 739.7091, -24.58033)},
			{"Crystal Generator 5", new WoWPoint(-141.7635, 729.8258, -24.57926)},
		};

		private bool _generator1IsDisabled, _generator2IsDisabled, _generator3IsDisabled, _generator4IsDisabled, _generator5IsDisabled;

		// walk around the force field to take out 2 remaining crystal generators
		[ObjectHandler(179503, "Force Field", 2000)]
		public Composite ForceField()
		{
			return
				new PrioritySelector(
					new Decorator(
						ctx =>
						!StyxWoW.Me.Combat && StyxWoW.Me.IsTank() && ((WoWGameObject)ctx).State != WoWGameObjectState.Active &&
						Targeting.Instance.FirstUnit == null,
						new PrioritySelector(
							ctx =>
							{ // update the crystal genorator status.
								foreach (var obj in ObjectManager.GetObjectsOfType<WoWGameObject>())
								{
									switch (obj.Entry)
									{
										case 177259: // Crystal Generator 1
											_generator1IsDisabled = obj.State == WoWGameObjectState.Active;
											break;
										case 177257: // Crystal Generator 2
											_generator2IsDisabled = obj.State == WoWGameObjectState.Active;
											break;
										case 177258: // Crystal Generator 3
											_generator3IsDisabled = obj.State == WoWGameObjectState.Active;
											break;
										case 179504: // Crystal Generator 4
											_generator4IsDisabled = obj.State == WoWGameObjectState.Active;
											break;
										case 179505: // Crystal Generator 5
											_generator5IsDisabled = obj.State == WoWGameObjectState.Active;
											break;
									}
								}
								return ctx;
							},
							new Decorator(
								ctx => BotPoi.Current.Type == PoiType.None && Me.IsTank(),
								new PrioritySelector(
									new Decorator(
										ctx => !_generator1IsDisabled,
										new Sequence(
											new ActionSetActivity("Moving towards Crystal Generator 1"),
											new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_generatorLocations["Crystal Generator 1"])))),
									new Decorator(
										ctx => !_generator3IsDisabled && !ScriptHelpers.IsBossAlive("Tendris Warpwood"),
										new Sequence(
											new ActionSetActivity("Moving towards Crystal Generator 3"),
											new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_generatorLocations["Crystal Generator 3"])))),
									new Decorator(
										ctx => !_generator2IsDisabled && !ScriptHelpers.IsBossAlive("Tendris Warpwood"),
										new Sequence(
											new ActionSetActivity("Moving towards Crystal Generator 2"),
											new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_generatorLocations["Crystal Generator 2"])))),
									new Decorator(
										ctx => !_generator4IsDisabled && !ScriptHelpers.IsBossAlive("Magister Kalendris"),
										new Sequence(
											new ActionSetActivity("Moving towards Crystal Generator 4"),
											new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_generatorLocations["Crystal Generator 4"])))),
									new Decorator(
										ctx => !_generator5IsDisabled && !ScriptHelpers.IsBossAlive("Magister Kalendris"),
										new PrioritySelector(
										  new Sequence(
											new ActionSetActivity("Moving towards Crystal Generator 5"),
											new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_generatorLocations["Crystal Generator 5"]))))))))));
		}

		#endregion

		#region Tendris Warpwood

		private const uint TendrisWarpwoodId = 11489;
		private const uint IronbarkProtectorId = 11459;


		[EncounterHandler(11489, "Tendris Warpwood")]
		public Composite TendrisWarpwoodEncounter()
		{
			const uint tendrisWarpwoodId = 11489;
			WoWUnit boss = null;
			// range should stay way from boss to avoid getting trampled.
			AddAvoidObject(ctx => Me.IsRange() && !Me.HasAura("Entangle"), 10, o => o.Entry == tendrisWarpwoodId && o.ToUnit().Combat && o.ToUnit().CurrentTargetGuid != Me.Guid);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelGroup("Grasping Vines", ScriptHelpers.PartyDispelType.Magic));
		}

		#endregion

		#region Illyanna Ravenoak

		[EncounterHandler(11488, "Illyanna Ravenoak")]
		public Composite IllyannaRavenoakEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Concussive Shot", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateDispelGroup("Immolation Trap", ScriptHelpers.PartyDispelType.Magic));
		}

		#endregion

		#region Magister Kalendris

		[EncounterHandler(11487, "Magister Kalendris")]
		public Composite MagisterKalendrisEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Shadow Word: Pain", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateDispelGroup("Dominate Mind", ScriptHelpers.PartyDispelType.Magic));
		}

		#endregion

		#region Magister Immol'thar

		[EncounterHandler(11496, "Immol'thar")]
		public Composite ImmoltharEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelGroup("Infected Bite", ScriptHelpers.PartyDispelType.Disease));
		}

		#endregion

		#region Magister Prince Tortheldrin

		private const uint PrinceTortheldrinId = 11486;

		[EncounterHandler(11486, "Prince Tortheldrin")]
		public Composite PrinceTortheldrinEncounter()
		{
			AddAvoidObject(ctx => !Me.IsTank(), 8, o => o.Entry == PrinceTortheldrinId && o.ToUnit().HasAura("Whirlwind"));
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion
	}
}
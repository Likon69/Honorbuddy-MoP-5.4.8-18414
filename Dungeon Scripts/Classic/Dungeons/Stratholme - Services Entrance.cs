using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
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
using Bots.DungeonBuddy.Enums;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class StratholmeServicesEntrance : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WoWPoint _entrance = new WoWPoint(3236.26, -4058.314, 108.464);

		public override uint DungeonId
		{
			get { return 274; }
		}

		public override WoWPoint Entrance
		{
			get { return _entrance; }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(3588.10, -3638.56, 138.47); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (Targeting.TargetPriority p in units)
			{
				WoWUnit unit = p.Object.ToUnit();
				if (unit.Entry == EyeOfNaxxramasId) // Eye of Naxxramas
					p.Score += 120;

				if (unit.Entry == MagistrateBarthilasId)
					p.Score += 10000;
			}
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var myLoc = Me.Location;
			foreach (WoWUnit unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				if (unit.Entry == EyeOfNaxxramasId && Me.Combat && unit.Location.DistanceSqr(myLoc) < 30 * 30)
				{
					outgoingunits.Add(unit);
				}
				else if (unit.Entry == AcrylicId && Me.IsTank() && !Me.Combat && myLoc.DistanceSqr(unit.Location) < 40 * 40)
				{
					outgoingunits.Add(unit);
				}
				else if (unit.Entry == BileSpewerId || unit.Entry == VenomBelcherId)
				{
					if (!ScriptHelpers.IsBossAlive("Magistrate Barthilas") && _slaughterGateOpen && Me.IsTank() && !Me.Combat)
						outgoingunits.Add(unit);
				}
				else if ((unit.Entry == PlagueRat || unit.Entry == PlaguedInsectId) && Me.Combat && unit.Location.DistanceSqr(myLoc) < 40 * 40)
				{
					if ((unit.CurrentTargetGuid == Me.Guid || unit.IsWithinMeleeRange) && myLoc.DistanceSqr(_inbetweenGateLoc) < 12 * 12 && _bugGateIsDown || !_bugGateIsDown)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				u =>
				{
					var unit = u as WoWUnit;
					if (unit != null)
					{
						if ((unit.Entry == BileSpewerId || unit.Entry == VenomBelcherId) && !unit.Combat && !_slaughterGateOpen)
							return true;

						if (unit.Entry == LordAuriusRivendareId && !unit.Combat && (!_innerDoorIsOpen || !_outerDoorIsOpen))
							return true;
					}
					return false;
				});
		}

		public override void OnEnter()
		{
			ZigguratPath = new CircularQueue<WoWPoint>
						   {
							   new WoWPoint(3834.884, -3490.78, 141.21),
							   new WoWPoint(3844.55, -3758.432, 145.0914),
							   new WoWPoint(4065.366, -3674.258, 132.6548)
						   };
		}

		#endregion

		private const uint EyeOfNaxxramasId = 10411;
		private const uint PlagueRat = 10441;
		private const uint PlaguedInsectId = 10461;
		private const uint AcrylicId = 10399;
		private const uint VenomBelcherId = 10417;
		private const uint BileSpewerId = 10416;
		private const uint EyeofNaxxramasId = 42973;
		private const uint LordAuriusRivendareId = 45412;
		private const uint MagistrateBarthilasId = 10435;

		private static readonly CircularQueue<WoWPoint> CourtYardPath = new CircularQueue<WoWPoint>
																		{
																			new WoWPoint(4027.646, -3430.52, 118.1971),
																			new WoWPoint(3986.372, -3397.229, 118.4226),
																			new WoWPoint(4027.646, -3430.52, 118.1971),
																			new WoWPoint(4085.29, -3395.159, 115.653)
																		};

		private static CircularQueue<WoWPoint> ZigguratPath;

		private readonly WaitTimer _waitForZombiesTimer = new WaitTimer(TimeSpan.FromSeconds(15));

		private bool _bugGateIsDown;
		private WoWPoint _inbetweenGateLoc = new WoWPoint(3919.933, -3547.046, 134.2238);
		private bool _innerDoorIsOpen;
		private bool _outerDoorIsOpen;
		private bool _slaughterGateOpen;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector(
				// clear the Zigguarts 
				new Decorator(
					ctx => !_slaughterGateOpen && ZigguratPath.Any() && Me.IsTank() && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator(ctx => Me.Location.Distance2D(ZigguratPath.Peek()) <= 4, new Action(ctx => ZigguratPath.Dequeue())),
						new Decorator(ctx => Me.Location.Distance2D(ZigguratPath.Peek()) > 4, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(ZigguratPath.Peek()))))),
				// clear courtyard.
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Magistrate Barthilas"),
					new PrioritySelector(
						new Decorator(
							ctx => Me.IsTank() && _slaughterGateOpen && (!_outerDoorIsOpen || !_innerDoorIsOpen) && Targeting.Instance.IsEmpty(),
							new PrioritySelector(
								ctx => CourtYardPath.Peek(),
								new Decorator(ctx => StyxWoW.Me.Location.Distance2DSqr((WoWPoint)ctx) < 5 * 5, new Action(ctx => CourtYardPath.Dequeue())),
								new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS((WoWPoint)ctx)))),
						new Decorator(
				// wait for zombies to storm the area.
							ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && (_outerDoorIsOpen || _innerDoorIsOpen) && !_waitForZombiesTimer.IsFinished,
							new ActionAlwaysSucceed()))));
		}

		[EncounterHandler(45329, "Crusade Commander Eligor Dawnbringer", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(45206, "Crusade Commander Korfax", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(45330, "Archmage Angela Dosantos", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
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


		[ObjectHandler(175354, "Doodad_SmallPortcullis09", ObjectRange = 35)]
		[ObjectHandler(175355, "Doodad_SmallPortcullis08", ObjectRange = 35)]
		public Composite Doodad_SmallPortcullis03Handler()
		{
			var moveInBetweengGatesTimer = new WaitTimer(TimeSpan.FromMinutes(3));
			var stayPutTimer = new WaitTimer(TimeSpan.FromSeconds(5));
			var waitLoc = new WoWPoint(3905.132, -3543.719, 136.554);

			WoWGameObject gate = null;
			return new PrioritySelector(
				ctx =>
				{
					gate = ctx as WoWGameObject;
					_bugGateIsDown = gate.State == WoWGameObjectState.Ready;
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
								new Decorator(ctx => Me.Location.Distance(waitLoc) > 4, new Action(ctx => Navigator.MoveTo(waitLoc))),
								new ActionAlwaysSucceed())),
						new Action(ctx => Navigator.MoveTo(_inbetweenGateLoc)))),
				// reset the timers once bot is in position.
				new Decorator(
					ctx => _inbetweenGateLoc.Distance(Me.Location) <= 4 && moveInBetweengGatesTimer.IsFinished,
					new Sequence(new Action(ctx => moveInBetweengGatesTimer.Reset()), new Action(ctx => stayPutTimer.Reset()))),
				// wait for the rats to spawn.. Targeting is handled in target filter
				new Decorator(ctx => !stayPutTimer.IsFinished && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(10436, "Baroness Anastari")]
		public Composite BaronessAnastariFight()
		{
			WoWUnit boss = null;
			const uint baronessAnastariId = 10436;
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, 10, o => o.Entry == baronessAnastariId && o.ToUnit().IsAlive && o.ToUnit().CurrentTargetGuid != Me.Guid);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(10438, "Maleki the Pallid")]
		public Composite MalekiThePallidFight()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Ice Tomb", ScriptHelpers.PartyDispelType.Magic));
		}

		[EncounterHandler(10435, "Magistrate Barthilas")]
		public Composite MagistrateBarthilasFight()
		{
			WoWUnit boss = null;
			const uint magistrateBarthId = 10435;
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, 10, o => o.Entry == magistrateBarthId && o.ToUnit().IsAlive && o.ToUnit().CurrentTargetGuid != Me.Guid);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(45412, "Lord Aurius Rivendare", Mode = CallBehaviorMode.Proximity, BossRange = 55)]
		public Composite LordAuriusRivendareFight()
		{
			WoWUnit boss = null;

			var insideInnerDoorLoc = new WoWPoint(4032.931, -3360.988, 115.0531);
			var innerLeftDoorSideLoc = new WoWPoint(4029.11, -3363.723, 115.0578);
			var innerRightDoorSideLoc = new WoWPoint(4035.901, -3363.785, 115.0578);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat && _innerDoorIsOpen && _outerDoorIsOpen && _waitForZombiesTimer.IsFinished && !Me.Combat,
					new PrioritySelector(
				// get inside the door before it closes.
						new Decorator(
							ctx => !Me.Location.IsPointLeftOfLine(innerLeftDoorSideLoc, innerRightDoorSideLoc) && !boss.Combat,
							new Action(ctx => Navigator.MoveTo(insideInnerDoorLoc))),
						new Decorator(
							ctx => Me.IsTank() && !Me.PartyMembers.All(p => p.Location.IsPointLeftOfLine(innerLeftDoorSideLoc, innerRightDoorSideLoc)),
							new PrioritySelector(new ActionSetActivity("Waiting for party members to get insided room."), new ActionAlwaysSucceed())))),
				// combat behavior
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(
							ctx => !Me.IsTank() && boss.Distance < 8 && !boss.IsMoving && !Me.IsCasting,
							ctx => boss,
							new ScriptHelpers.AngleSpan(0, 180)))));
		}

		[ObjectHandler(175373, "Slaught Sqaure Entrance Gate", 1000)]
		public Composite SlaughtSqaureEntranceDoor()
		{
			return new PrioritySelector(
				new Helpers.Action<WoWGameObject>(
					door =>
					{
						_slaughterGateOpen = door.State == WoWGameObjectState.Active;
						return RunStatus.Failure;
					}));
		}

		[ObjectHandler(175405, "Slaught Sqaure Outter Gate", 1000)]
		public Composite SlaughtSqaureOutterDoor()
		{
			return new PrioritySelector(
				new Helpers.Action<WoWGameObject>(
					door =>
					{
						_outerDoorIsOpen = door.State == WoWGameObjectState.Active;
						return RunStatus.Failure;
					}));
		}

		[ObjectHandler(175796, "Slaught Sqaure Inner Gate", 1000)]
		public Composite SlaughtSqaureInnerDoor()
		{
			return new PrioritySelector(
				new Helpers.Action<WoWGameObject>(
					door =>
					{
						_innerDoorIsOpen = door.State == WoWGameObjectState.Active;
						return RunStatus.Failure;
					}));
		}
	}
}
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Enums;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class VioletHold : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 220; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(5676.366, 481.6799, 652.2564); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(1797.077f, 804.2758f, 44.36467f); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					if (ret.Entry == EtherealSphereId)
						return true;
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
					if (StyxWoW.Me.IsTank() && unit.IsHostile && !unit.IsCritter)
						outgoingunits.Add(unit);

					if (unit.Entry == VoidSentryId && unit.CanSelect && unit.Attackable)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == VoidSentryId)
						priority.Score += 500;

					if (StyxWoW.Me.IsTank() && !unit.TaggedByMe)
						priority.Score += 100;
				}
			}
		}

		public override bool CanExitNow
		{
			get { return _prisonSeal== null || !_prisonSeal.IsValid || _prisonSeal.State == WoWGameObjectState.Active; }
		}

		readonly WoWPoint _prisonDoorLeftSide = new WoWPoint(1823.263, 808.7631, 44.36466);
		readonly WoWPoint _prisonDoorRightSide = new WoWPoint(1823.375, 799.1082, 44.36467);
		WoWPoint _insidePrisonDoorMoveTo = new WoWPoint(1826.208, 803.7178, 44.36467);

		public override MoveResult MoveTo(WoWPoint location)
		{
			// prevent moving outside door when dungeon is not complete.
			if (location.IsPointLeftOfLine(_prisonDoorRightSide, _prisonDoorLeftSide) && !IsComplete)
			{
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		#endregion

		private const uint VoidSentryId = 29364;
		private const uint EtherealSphereId = 29271;
		private const uint ContainmentQuestId = 29830;
		private const uint PrisonSealId = 30896;

		private readonly uint[] _cellIds =
		{
			191566, // Lavanthor
			191606, // Moragg
			191722, // Ichoron
			191556, // Xevozz
			191564, // Erekem
			191565 // Zuramat
		};

		private int NumberOfCellsOpen
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().Count(g => _cellIds.Contains(g.Entry) && g.State == WoWGameObjectState.Active); }
		}


		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(30658, "Lieutenant Sinclari", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
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


		WoWGameObject _prisonSeal;

		[ObjectHandler(191723, "Prison Seal", ObjectRange = 200)]
		public Composite PrisonSealHandler()
		{
			var roomCenterLoc = new WoWPoint(1883.929, 802.645, 38.41035);
			var sinclariStartingLoc = new WoWPoint(1830.95, 799.463, 44.33469);

			WoWUnit portal = null, sinclari = null;

			return new PrioritySelector(
				ctx =>
				{
					sinclari = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 30658);
					portal = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 30679 || u.Entry == 32174).OrderBy(u => u.DistanceSqr).FirstOrDefault();
					return _prisonSeal = ctx as WoWGameObject;

				},

				new Decorator(ctx => Me.Location.IsPointLeftOfLine(_prisonDoorRightSide, _prisonDoorLeftSide) && ScriptHelpers.IsBossAlive("Cyanigosa"),
					new PrioritySelector(
						// If I was a sucka by not being inside room when event started then talk to sinclari to get ported inside
						new Decorator(ctx => sinclari != null && sinclari.Location.IsPointLeftOfLine(_prisonDoorRightSide, _prisonDoorLeftSide) && sinclari != null,
							ScriptHelpers.CreateTalkToNpc(ctx => sinclari)),

						// step inside the room to not get locked out.
						new Action(ctx => Navigator.MoveTo(_insidePrisonDoorMoveTo)))),

				new Decorator(
					ctx => sinclari != null && sinclari.Location.DistanceSqr(sinclariStartingLoc) <= 3 * 3 && StyxWoW.Me.IsTank() ,
							new PrioritySelector(
								// Start the event if tank and all group members are inside room
								new Decorator(ctx => ScriptHelpers.GroupMembers.All(g => g.MapId == Me.MapId && !g.Location.IsPointLeftOfLine(_prisonDoorRightSide, _prisonDoorLeftSide)),
									new PrioritySelector(
										new Decorator(ctx => sinclari.DistanceSqr > 4 * 4, new Action(ctx => Navigator.MoveTo(sinclari.Location))),
										new Sequence(
											new DecoratorContinue(ctx => Me.IsMoving, 
												new Sequence(
													new Action(ctx => WoWMovement.MoveStop()),
													new WaitContinue(2, ctx => !Me.IsMoving, new Sleep(200)))),
											new DecoratorContinue(ctx => !GossipFrame.Instance.IsVisible, 
												new Sequence(
													new Action(ctx => sinclari.Interact()),
													new WaitContinue(2, ctx => !GossipFrame.Instance.IsVisible, new Sleep(200)))),
											new Action(ctx => GossipFrame.Instance.SelectGossipOption(0)),
											new Sleep(2000),
											new Action(ctx => GossipFrame.Instance.SelectGossipOption(0)),
											new Sleep(2000)))),
								// wait for group members to get in room
								new Decorator(ctx => !Me.IsBeingAttacked, new ActionAlwaysSucceed()))),

				new Decorator(
					ctx =>
					ScriptHelpers.IsBossAlive("Cyanigosa") &&
					(_prisonSeal.State == WoWGameObjectState.Ready || sinclari == null || sinclari.Location.IsPointLeftOfLine(_prisonDoorRightSide, _prisonDoorLeftSide)),
					new PrioritySelector(
						new Decorator(
							ctx => ScriptHelpers.GetUnfriendlyNpsAtLocation(roomCenterLoc, 200, u => true).Any(),
							ScriptHelpers.CreateClearArea(() => roomCenterLoc, 200, u => true)),
						new Decorator(
							ctx => Targeting.Instance.FirstUnit == null && !ScriptHelpers.GetUnfriendlyNpsAtLocation(roomCenterLoc, 200, u => true).Any(),
							new PrioritySelector(
								new Decorator(
									ctx => portal != null && StyxWoW.Me.Location.DistanceSqr(portal.Location) > 10 * 10,
									new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(portal.Location))),
								new Decorator(
									ctx => portal != null && StyxWoW.Me.IsTank() && StyxWoW.Me.Location.DistanceSqr(portal.Location) <= 10 * 10, new ActionAlwaysSucceed()),
								new Decorator(
									ctx => portal == null && StyxWoW.Me.Location.DistanceSqr(roomCenterLoc) > 15 * 15, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(roomCenterLoc))),
								new Decorator(
									ctx => portal == null && StyxWoW.Me.IsTank() && StyxWoW.Me.Location.DistanceSqr(roomCenterLoc) <= 15 * 15, new ActionAlwaysSucceed()))))),
				new Decorator(
					ctx => _prisonSeal.State == WoWGameObjectState.Active && ScriptHelpers.IsBossAlive("Cyanigosa") && NumberOfCellsOpen == 2, new Action(ctx => ScriptHelpers.MarkBossAsDead("Cyanigosa", "Dungeon is over."))));
		}

		[EncounterHandler(29315, "Erekem")]
		public Composite ErekemEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(29316, "Moragg")]
		public Composite MoraggEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(29313, "Ichoron")]
		public Composite IchoronEncounter()
		{
			WoWUnit boss = null;
			WoWGameObject activationCrystal = null;

			return new PrioritySelector(
				ctx =>
				{
					activationCrystal =
						ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Entry == 193611 && o.CanUse()).OrderBy(o => o.DistanceSqr).FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				new Decorator(ctx => boss.HasAura("Drained") && activationCrystal != null, ScriptHelpers.CreateInteractWithObject(ctx => activationCrystal, 0, true)));
		}

		[EncounterHandler(29266, "Xevozz")]
		public Composite XevozzEncounter()
		{
			WoWUnit boss = null;
			var tankLoc = new WoWPoint(1937.878, 793.6298, 52.41381);
			AddAvoidObject(ctx => !Me.IsCasting, obj => Me.IsRange() && Me.IsMoving ? 16 : 12, EtherealSphereId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 6));
		}

		[EncounterHandler(29312, "Lavanthor")]
		public Composite LavanthorEncounter()
		{
			WoWUnit boss = null;
			const uint lavaBombId = 191457;
			AddAvoidObject(ctx => true, 6, lavaBombId);
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 15, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(14));
		}

		[EncounterHandler(29314, "Zuramat the Obliterator")]
		public Composite ZuramatTheObliteratorEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelEnemy("Shroud of Darkness", ScriptHelpers.EnemyDispelType.Magic, ctx => boss));
		}

		[EncounterHandler(31134, "Cyanigosa")]
		public Composite CyanigosaEncounter()
		{
			WoWUnit boss = null;
			var blizzardIds = new uint[] { 59369, 58693 };
			AddAvoidObject(ctx => true, 10, blizzardIds);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 20,
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 100),
					new ScriptHelpers.AngleSpan(180, 100)));
		}
	}
}
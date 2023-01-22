
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
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
	public class Uldaman : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 22; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-6061.87, -2956.325, 209.7706); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-228.2558, 40.31088, -46.0186); }
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				switch (unit.Entry)
				{
					case 3560: // healing totem
					case 6017: // Lava sprout totem
						outgoingunits.Add(unit);
						break;
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
					case 3560: // healing totem
					case 6017: // Lava sprout totem
						p.Score += 500;
						break;
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;
					if (unit != null)
					{
						if (unit.Entry == JadespineBasiliskId && unit.HasAura("Reflection") && Me.IsRange() && Me.IsDps())
							return true;
						if (unit.Entry == EarthenGuardianId && unit.HasAura("Whirlwind") && Me.IsMelee() && Me.IsDps())
							return true;
					}
					return false;
				});
		}

		#endregion


		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private WoWGameObject HallOfCraftersDoor
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 124367); }
		}

		private WoWGameObject AncientTreasure
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 141979); }
		}

		readonly WoWPoint _obsidianSentinelPlatformLoc = new WoWPoint(-169.0342, 389.459, -36.44312);
		readonly WoWPoint _obsidianSentinelPlatformExitLoc = new WoWPoint(-156.0965, 386.8745, -37.58194);

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			if (myLoc.Distance(_obsidianSentinelPlatformLoc) < 20 && myLoc.Z > -37f && location.Distance(myLoc) > 20)
			{
				Navigator.PlayerMover.MoveTowards(_obsidianSentinelPlatformExitLoc);
				if (Navigator.NavigationProvider.StuckHandler.IsStuck())
				{
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
				}
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		#region Root

		private const int TheChamberOfKhazmulQuestId = 27679;
		const int ArchaedasTheAncientStoneWatcherQuestId = 27680;

		[EncounterHandler(46241, "Aoren Sunglow", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(46235, "Lidia Sunglow", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(46236, "High Examiner Tae'thelan Bloodwatcher", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(0)]
		public Composite RootHandler()
		{
			PlayerQuest quest = null;
			return new PrioritySelector(
				new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(TheChamberOfKhazmulQuestId)) != null && quest.IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(TheChamberOfKhazmulQuestId)),

				new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(ArchaedasTheAncientStoneWatcherQuestId)) != null && quest.IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(ArchaedasTheAncientStoneWatcherQuestId))
				);
		}
		#endregion

		[LocationHandler(-347.3496, 125.7285, -49.82032, 4)]
		public Composite StuckAtThreeDwarvesHandler()
		{
			var movetoLoc = new WoWPoint(-349.752, 130.5805, -47.7855);
			return new PrioritySelector(
				new Decorator(ctx => !Me.Combat && Navigator.NavigationProvider.StuckHandler.IsStuck(),
					new Sequence(
						new Action(ctx => Navigator.PlayerMover.MoveTowards(movetoLoc)),
						new Action(ctx => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend)),
						new Action(ctx => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend))
						))
				);
		}


		[ObjectHandler(130511, "Altar of The Keepers")]
		public Composite AltarofTheKeepers()
		{
			WoWGameObject altar = null;
			WoWGameObject door = null;
			return new PrioritySelector(
				ctx =>
				{
					door = HallOfCraftersDoor; // cache the door object.
					return altar = ctx as WoWGameObject;
				},
				new Decorator(ctx => Targeting.Instance.IsEmpty() && door != null && door.State != WoWGameObjectState.Active,
					new PrioritySelector(
						new Decorator(ctx => !altar.WithinInteractRange, new Action(ctx => Navigator.MoveTo(altar.Location))),
						new Decorator(ctx => altar.WithinInteractRange && Me.ChannelObjectGuid == 0,
							new Sequence(
								new Action(ctx => altar.Interact()),
								new WaitContinue(2, ctx => !Me.Combat && Me.ChannelObjectGuid == 0, new ActionAlwaysSucceed())))
						)));
		}


		[ObjectHandler(141979, "Ancient Treasure")]
		public Composite AncientTreasureHandler()
		{
			return new Decorator<WoWGameObject>(
				chest => BossManager.CurrentBoss == null && chest.CanLoot && !chest.InUse,
				ScriptHelpers.CreateInteractWithObjectContinue(141979, 4));
		}

		[EncounterHandler(7206, "Ancient Stone Keeper")]
		public Composite AncientStoneKeeperHandler()
		{
			const uint sandStormId = 7226;
			var tankLoc = new WoWPoint(-42.94647, 220.7556, -48.32799);
			// avoid the sand storms
			AddAvoidObject(ctx => !Me.IsTank(), 8, sandStormId);
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				new Decorator(ctx => Me.IsTank() && boss.CurrentTargetGuid == Me.Guid,
					ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 5)));
		}

		#region Galgann Firehammer

		private const uint GalgannFirehammerId = 7291;

		[EncounterHandler(7291, "Galgann Firehammer")]
		public Composite GalgannFirehammerHandler()
		{
			// range shouldn't get caught in the fire nova aoe.
			WoWUnit boss = null;
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, 10, o => o.Entry == GalgannFirehammerId && o.ToUnit().CurrentTargetGuid != Me.Guid && o.ToUnit().IsAlive);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(7030, "Shadowforge Geologist")]
		public Composite ShadowforgeGeologistHandler()
		{
			const int flameSpikeId = 8814;
			const int flameLashId = 3356;
			// stay out of the flame spike.
			AddAvoidObject(ctx => true, 5, flameSpikeId);
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, flameLashId, flameSpikeId));
		}

		#endregion

		#region Grimlok

		private const uint JadespineBasiliskId = 4863;

		[EncounterHandler(4854, "Grimlok")]
		public Composite GrimlokHandler()
		{
			const int chainBoltId = 8292;
			const int shrinkId = 11892;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, chainBoltId, shrinkId));
		}

		[EncounterHandler(4863, "Jadespine Basilisk")]
		public Composite JadespineBasiliskHandler()
		{
			const int crystallineSlumberId = 3636;
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Crystalline Slumber", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateDispelEnemy("Bloodlust", ScriptHelpers.EnemyDispelType.Magic, ctx => unit),
				ScriptHelpers.CreateDispelEnemy("Reflection", ScriptHelpers.EnemyDispelType.Magic, ctx => unit),
				ScriptHelpers.CreateInterruptCast(ctx => unit, crystallineSlumberId));
		}

		#endregion

		#region Ironaya

		private const uint IronayaId = 7228;

		[ObjectHandler(124371, "Keystone")]
		public Composite KeystoneObject()
		{
			WoWGameObject keystone = null;
			return new PrioritySelector(
				ctx => keystone = ctx as WoWGameObject,
				new Decorator(ctx => !Me.Combat && keystone.Locked == false && Me.IsTank(), ScriptHelpers.CreateInteractWithObject(ctx => keystone)));
		}

		[EncounterHandler(7228, "Ironaya")]
		public Composite IronayaHandler()
		{
			WoWUnit boss = null;
			// range should stay away to avoid geting stunned. 
			AddAvoidObject(ctx => Me.IsRange(), 5, o => o.Entry == IronayaId && o.ToUnit().CurrentTargetGuid != Me.Guid && o.ToUnit().IsAlive);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// avoid getting cleaved 
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 10, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)));
		}

		#endregion

		#region Archaedas

		private const uint EarthenGuardianId = 7076;


		[EncounterHandler(2748, "Archaedas", Mode = CallBehaviorMode.Proximity)]
		public Composite ArchaedasHandler()
		{
			const uint altarofTheArchaedas = 133234;

			AddAvoidObject(ctx => !Me.IsTank(), 11, o => o.Entry == EarthenGuardianId && o.ToUnit().HasAura("Whirlwind"));
			return
				new PrioritySelector(
					new Decorator<WoWUnit>(
						boss => boss.HasAura("Freeze Anim") && boss.DistanceSqr <= 30 * 30 && boss.ZDiff < 8,
						new PrioritySelector( ctx => ObjectManager.ObjectList.FirstOrDefault(o => o.Entry ==altarofTheArchaedas ),
							// Note: even if in-game cogwheel for this object shows that it's usable we can only channel on it from a closer distance
							new Decorator<WoWGameObject>(altar => altar.DistanceSqr > 4 * 4,
								new Helpers.Action<WoWGameObject>(altar => Navigator.MoveTo(altar.Location))),
							new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							new Sequence(
								new Helpers.Action<WoWGameObject>(altar => altar.Interact()),
								new Sleep(2000),
								new WaitContinue(12, ctx => !Me.IsCasting && !Me.IsChanneling, new ActionAlwaysSucceed())))));
		}

		#endregion
	}
}
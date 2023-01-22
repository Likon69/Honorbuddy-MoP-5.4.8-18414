
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class TheNexus : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId { get { return 225; } }

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(3900.531, 6985.386, 69.4887); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(145.4159, -15.77039, -16.63676); }
		}

		private readonly WaitTimer _kiteCrystallineFrayerTimer = WaitTimer.FiveSeconds;

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				obj =>
				{
					if (obj.Entry == AnomalusId && ((WoWUnit)obj).HasAura("Rift Shield"))
						return true;
					// forces tank to move
					if (obj.Entry == CrystallineFrayerId && Me.IsTank() && Me.Combat)
					{
						_kiteCrystallineFrayerTimer.Reset();
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
					if (Me.IsDps() && unit.Entry == ChaoticRiftId)
						priority.Score += 500;
					else if (unit.Entry == CrystallineFrayerId)
						priority.Score -= 10000;
				}
			}
		}

		#endregion

		private const uint AnomalusId = 26763;
		private const uint ChaoticRiftId = 26918;

		private const uint CrystallineFrayerId = 26793;
		private const uint OrmorokId = 26794;


		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(55531, "Warmage Kaitlyn", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(55536, "Image of Warmage Kaitlyn", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(55537, "Image of Warmage Kaitlyn", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
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

		[ObjectHandler(192788, "Berinand's Research", ObjectRange = 20)]
		public Composite BerinandsResearchHandler()
		{
			const uint haveTheyNoShameQuestId = 13095;

			WoWGameObject book = null;
			return new PrioritySelector(ctx => book = ctx as WoWGameObject,
				new Decorator(ctx => Me.QuestLog.ContainsQuest(haveTheyNoShameQuestId) && !Me.QuestLog.GetQuestById(haveTheyNoShameQuestId).IsCompleted && !Me.Combat && !book.InUse,
					new PrioritySelector(
						ScriptHelpers.CreateInteractWithObject(ctx => book, 5000))));
		}

		[EncounterHandler(26731, "Grand Magus Telestra", Mode = CallBehaviorMode.Proximity, BossRange = 65)]
		public Composite GrandMagusTelestraEncounter()
		{
			var trashTankLoc = new WoWPoint(545.6187, 108.4139, -16.63844);
			var trashLoc = new WoWPoint(518.0992, 90.58775, -16.12559);

			WoWUnit boss = null, trash = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat && !Me.Combat,
					new PrioritySelector(
						ctx => trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashLoc, 10, u => true).FirstOrDefault(),
						ScriptHelpers.CreatePullNpcToLocation(ctx => trash != null, ctx => trash, ctx => trashTankLoc, 10)))
				);
		}

		[EncounterHandler(26763, "Anomalus")]
		public Composite AnomalusEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit
				);
		}

		[EncounterHandler(26794, "Ormorok the Tree-Shaper", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite OrmorokTheTreeShaperGauntletBehavior()
		{
			return new PrioritySelector(
				new Decorator(ctx => Me.IsTank() && Me.Combat && Targeting.Instance.IsEmpty() && !_kiteCrystallineFrayerTimer.IsFinished,
					new Action(ctx => Navigator.MoveTo(_ormorokRoomCenterLoc)))
				);
		}

		readonly WoWPoint _ormorokRoomCenterLoc = new WoWPoint(264.9587, -225.4145, -9.100944);


		[EncounterHandler(26794, "Ormorok the Tree-Shaper")]
		public Composite OrmorokTheTreeShaperEncounter()
		{
			WoWUnit boss = null;
			const uint crystalSpikeTriggerId = 27079;
			AddAvoidObject(ctx => true, 4, crystalSpikeTriggerId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(26723, "Keristrasza", Mode = CallBehaviorMode.Proximity)]
		public Composite KeristraszaEncounter()
		{
			var faceTimer = new Stopwatch();
			WoWGameObject sphere = null;
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						ctx => sphere =
							   ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
								   o => (o.Entry == 188526 || o.Entry == 188527 || o.Entry == 188528) && o.CanUse()).OrderBy(
									   o => o.DistanceSqr).FirstOrDefault(),
				// clear the room, 
						ScriptHelpers.CreateClearArea(() => boss.Location, 40, u => u != boss),
				// interact with the spheres.
						new Decorator(
							ctx => StyxWoW.Me.IsLeader() && sphere != null && Targeting.Instance.IsEmpty() && boss.DistanceSqr < 40 * 40,
							ScriptHelpers.CreateInteractWithObject(ctx => sphere))
						)),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => StyxWoW.Me.HasAura("Intense Cold") && StyxWoW.Me.Auras["Intense Cold"].StackCount >= 3,
							new Sequence(
								new Action(ctx => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend)),
								new WaitContinue(1, ctx => false, new ActionAlwaysSucceed()),
								new Action(ctx => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend)))),
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => Me.IsFollower() && !boss.IsMoving && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 100), new ScriptHelpers.AngleSpan(180, 100))
						))
				);
		}
	}
}
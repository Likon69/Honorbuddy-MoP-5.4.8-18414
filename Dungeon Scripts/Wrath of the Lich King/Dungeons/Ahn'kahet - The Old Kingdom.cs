using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class AhnkahetTheOldKingdom : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId { get { return 218; } }

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(3640.21, 2028.928, 2.59325); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint (333.0131, -1109.873, 69.77443); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret => { return false; });
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == TwilightVolunteerId && unit.CanSelect && unit.Attackable)
					{
						var boss = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == JedogaShadowseekerId);
						if (boss != null && (!boss.Attackable || !boss.CanSelect))
							outgoingunits.Add(unit);
					}
					if (StyxWoW.Me.HasAura("Insanity") && unit.CanSelect && unit.Attackable)
					{
						outgoingunits.Add(unit);
					}
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
					if (unit.Entry == AhnkaharGuardianId)
						priority.Score += 500;

					if (unit.Entry == SpellFlingerId && StyxWoW.Me.IsRange())
						priority.Score += 500;
				}
			}
		}

		#endregion

		#region Root

		private const uint TwilightVolunteerId = 30385;
		private const uint AhnkaharGuardianId = 30176;
		private const uint SpellFlingerId = 30278;
		private const uint PlagueWalkerId = 30283;
		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		[EncounterHandler(55658, "Seer Ixit", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
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


		[EncounterHandler(0, "Root")]
		public Composite RootEncounter()
		{
			// handle a tough trash pul.
			var trashToNadoxTankLoc = new WoWPoint(598.4812, -1022.298, 38.35839);
			var trashToNadoxWaitLoc = new WoWPoint(615.3074, -1021.679, 32.71127);
			var trashToNadoxLoc = new WoWPoint(638.6355, -1004.077, 22.94104);
			const int pupilNoMoreQuestId = 29825;
			const int reclaimingAhnKahetQuestId = 29826;
			const uint ahnkaharSwarmerId = 30338;

			WoWUnit[] trash = null;

			return new PrioritySelector(
					new Decorator(ctx => Me.QuestLog.ContainsQuest(pupilNoMoreQuestId) && Me.QuestLog.GetQuestById(pupilNoMoreQuestId).IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(pupilNoMoreQuestId)),

					new Decorator(ctx => Me.QuestLog.ContainsQuest(reclaimingAhnKahetQuestId) && Me.QuestLog.GetQuestById(reclaimingAhnKahetQuestId).IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(reclaimingAhnKahetQuestId)),

					new Decorator(
						ctx =>
						StyxWoW.Me.Location.DistanceSqr(trashToNadoxTankLoc) <= 25 * 25 &&
						ScriptHelpers.GetUnfriendlyNpsAtLocation(trashToNadoxLoc, 70, u => u.Entry == PlagueWalkerId).Any(),
						new PrioritySelector(
							ctx => trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashToNadoxLoc, 55, u => u.Entry != ahnkaharSwarmerId).ToArray(),
							ScriptHelpers.CreatePullNpcToLocation(
								ctx => trash.Length > 4,
								ctx => trash.Any(t => t.Location.DistanceSqr(trashToNadoxLoc) <= 7 * 7),
								ctx => trash[0],
								ctx => trashToNadoxTankLoc,
								ctx => StyxWoW.Me.IsTank() ? trashToNadoxWaitLoc : trashToNadoxTankLoc,
								10))));
		}

		#endregion


		[EncounterHandler(29309, "Elder Nadox")]
		public Composite ElderNadoxEncounter()
		{
			return new PrioritySelector(
				);
		}

		#region Prince Taldaram

					WaitTimer PrinceTaldaramCombatTimer = new WaitTimer(TimeSpan.FromSeconds(6));

		[EncounterHandler(29308, "Prince Taldaram", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite PrinceTaldaramPreEncounter()
		{
			WoWUnit boss = null;
			WoWGameObject device = null;

			return new PrioritySelector(
				ctx =>
				{
					device =
						ObjectManager.GetObjectsOfType<WoWGameObject>()
							.Where(o => (o.Entry == 193093 || o.Entry == 193094) && o.State == WoWGameObjectState.Ready)
							.OrderBy(o => o.DistanceSqr)
							.FirstOrDefault
							();
					return boss = ctx as WoWUnit;
				},
				// handle vanish
				new Decorator(ctx => !PrinceTaldaramCombatTimer.IsFinished && boss == null && StyxWoW.Me.IsTank(), new ActionAlwaysSucceed()),
				new Decorator(
					ctx => device != null,
					new PrioritySelector(
						new Decorator(
							ctx => device.DistanceSqr > 5*5,
							new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(device.Location))),
						new Decorator(
							ctx => device.DistanceSqr <= 5*5 && !StyxWoW.Me.Combat,
							ScriptHelpers.CreateInteractWithObject(ctx => device))
						))
				);
		}

		[EncounterHandler(29308, "Prince Taldaram")]
		public Composite PrinceTaldaramEncounter()
		{
			WoWUnit boss = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Action(
					ctx =>
					{
						PrinceTaldaramCombatTimer.Reset();
						return RunStatus.Failure;
					}));
		}

		#endregion

		private const uint JedogaShadowseekerId = 29310;

		[EncounterHandler(29310, "Jedoga Shadowseeker", Mode = CallBehaviorMode.Proximity)]
		public Composite JedogaShadowseekerEncounter()
		{
			var thunderStruckIds = new uint[] { 56926, 60029 };
			AddAvoidObject(ctx => !Me.IsCasting, 5, thunderStruckIds);

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateClearArea(() => boss.Location, 30, u => u != boss)
				);
		}



		[EncounterHandler(29311, "Herald Volazj", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite HeraldVolazjEncounter()
		{
			WoWUnit boss = null;
			var trashLoc = new WoWPoint(539.0362, -521.6564, 26.35604);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateClearArea(() => trashLoc, 100, u => u != boss)
				);
		}
	}
}
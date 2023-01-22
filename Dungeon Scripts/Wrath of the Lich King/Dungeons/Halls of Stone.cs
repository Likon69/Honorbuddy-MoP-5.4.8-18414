
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Enums;
using Bots.DungeonBuddy.Profiles.Handlers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
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
	public class HallsOfStone : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 208; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(8921.595, -965.0018, 1039.163); }
		}

		public override WoWPoint ExitLocation
		{
			get
			{
				return new WoWPoint(1152.436, 819.3436, 195.3503);
			}
		}

		//  public override bool IsFlyingCorpseRun
		//     {
		//         get { return true; }
		//     }

		public override void OnEnter()
		{
			_talkedToBranAtToA = _escortedBranToEncounter = false;
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null)
					{
						if (unit.Entry == SjonnirTheIronshaperId && unit.HasAura("Lightning Ring") && Me.IsMelee() && Me.IsDps())
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
					if (unit.CurrentTargetGuid != 0 && unit.CurrentTarget.Entry == BrannId) // targeting Brann
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
					if (unit.CurrentTarget != null && unit.CurrentTarget.Entry == BrannId)
						priority.Score += 500;
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var lootChest = DungeonBuddySettings.Instance.LootMode != LootMode.Off && !Me.Combat;

			foreach (var obj in incomingunits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null && _tribualOfAgesChestIds.Contains(gObj.Entry) && gObj.CanUse())
				{
					if (ScriptHelpers.IsBossAlive("Tribunal of Ages"))
						ScriptHelpers.MarkBossAsDead("Tribunal of Ages", "Event is over");
					if (lootChest)
						outgoingunits.Add(gObj);
				}
			}
		}

		#endregion

		private readonly uint[] _entranceQuestIds = {29850, 29848};

		private const uint BrannId = 28070;
		private const uint SjonnirTheIronshaperId = 27978;
		private bool _escortedBranToEncounter, _talkedToBranAtToA;
		private const uint HallsOfStoneQuestId = 13207;

		readonly WoWPoint _lastBossLocation = new WoWPoint(1295.208, 667.157, 189.6078);
		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public async Task<bool> RootLogic(WoWUnit supplies)
		{
			// port outside and back in to hand in quests once dungeon is complete.
			// QuestPickupTurninHandler will handle the turnin
			return IsComplete && !Me.Combat
					&& _entranceQuestIds.Any(ScriptHelpers.IsQuestInLogComplete)
					&& LootTargeting.Instance.IsEmpty()
					&& Me.Location.DistanceSqr(_lastBossLocation) < 50 * 50
					// The turnin for Halls of Stone quest is at last boss so we don't want to port until it's turned in.
					// The proximity handler for Brann should handle the turnin at last boss.
					&& !ScriptHelpers.IsQuestInLogComplete(HallsOfStoneQuestId)
					&& await ScriptHelpers.PortOutsideAndBackIn();
		}

		const int BossDoorDoodad1 = 191296;	

		[EncounterHandler(55835, "Kaldir Ironbane", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(28070, "Brann Bronzebeard.", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			// brann runs off if someone starts the event so don't go chasing after him
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location) || npc.IsMoving)
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		[EncounterHandler(27977, "Krystallus")]
		public Func<WoWUnit, Task<bool>> KrystallusEncounter()
		{
			// run from party members that have Petrifying Grip
			AddAvoidObject(ctx => Me.HasAura("Petrifying Grip"), 
				15, 
				u => !u.IsMe && u is WoWPlayer && ((WoWPlayer)u).HasAura("Petrifying Grip"));

			return async boss => false;
		}

		[EncounterHandler(27975, "Maiden of Grief")]
		public Func<WoWUnit, Task<bool>> MaidenOfGriefEncounter()
		{
			const uint stormOfGriefId = 50752;
			AddAvoidObject(ctx => true, 9, stormOfGriefId);

			return async boss => false;
		}

		[EncounterHandler(28234, "Tribunal of Ages", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>> TribunalOfAgesEncounter()
		{
			const uint searingGazeId = 28265;
			const uint darkMatterTargetId = 28237;
			//const uint tribunalOfAgesId = 28234;


			var brannOriginalLoc = new WoWPoint(1077.41, 474.1604, 207.7255);
			var brannEncounterEntranceLoc = new WoWPoint(939.6468, 375.4893, 207.4221);

			AddAvoidObject(
				ctx => (StyxWoW.Me.IsLeader() && !StyxWoW.Me.IsActuallyInCombat) || StyxWoW.Me.IsFollower(),
				6,
				searingGazeId);
			AddAvoidObject(
				ctx => (StyxWoW.Me.IsLeader() && !StyxWoW.Me.IsActuallyInCombat) || StyxWoW.Me.IsFollower(),
				8,
				darkMatterTargetId);

			WaitTimer waitToStartEncounter = null;

			return async boss =>
			{
				var brann = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BrannId);

				var isTank = Me.IsTank();

				if (!_escortedBranToEncounter)
				{
					if (!Targeting.Instance.IsEmpty())
						return false;

					if (brann == null)
					{
						if (Me.Location.DistanceSqr(brannOriginalLoc) > 50*50)
						{
							ScriptHelpers.SetLeaderMoveToPoi(brannOriginalLoc);
							return false;
						}
						_escortedBranToEncounter = true;
					}
					else if (isTank)
					{
						var brannLoc = brann.Location;
						if (brannLoc.DistanceSqr(brannEncounterEntranceLoc) < 5*5)
						{
							_escortedBranToEncounter = true;
							Logger.Write("Escorted Brann to the Tribual of Ages encounter");
							return true;
						}

						if (brannLoc.DistanceSqr(_brannBossDoorLocation) < 5*5
							|| brannLoc.DistanceSqr(_brannAtLastBossByConsoleLoc) < 5 * 5)
						{
							_escortedBranToEncounter = true;
							ScriptHelpers.MarkBossAsDead("Tribunal of Ages", "Event is over");
							return true;
						}

						// escort brann to the 'Tribunal of Ages' encounter
						if (brann.Location.DistanceSqr(brannOriginalLoc) >= 3*3)
						{
							// 
							if (Me.Location.DistanceSqr(brann.Location) > 10*10)
							{
								ScriptHelpers.SetLeaderMoveToPoi(brann.Location);
								return false;
							}
							return await ScriptHelpers.TankTalkToAndEscortNpc(brann, brannOriginalLoc);
						}


						// pickup quest if needed. followers will pickup quest from the proximity handler for brann.
						if (brann.HasQuestAvailable(true) && await ScriptHelpers.PickupQuest(brann))
							return true;

						// wait for a few seconds to give group members a chance to pickup quests
						// before starting event
						if (waitToStartEncounter == null)
						{
							waitToStartEncounter = new WaitTimer(TimeSpan.FromSeconds(12));
							waitToStartEncounter.Reset();
						}
						if (!waitToStartEncounter.IsFinished && ScriptHelpers.GroupMembers.Any(g => g.Player != null && g.Player.Combat))
						{
							TreeRoot.StatusText = "Giving group members a chance to pickup quest";
							return true;
						}
						TreeRoot.StatusText = null;
						// start the event.
						return await ScriptHelpers.TalkToNpc(brann);
					}
				}
				else
				{
					// talk to Brann to start the event.
					if (brann != null && brann.CanGossip && isTank
						&& brann.Location.DistanceSqr(brannEncounterEntranceLoc) < 3*3)
					{
						return await ScriptHelpers.TalkToNpc(brann);
					}

					// move to encounter area
					if (brann == null)
					{
						var distSqrToEncounter = Me.Location.DistanceSqr(brannEncounterEntranceLoc);
						if (distSqrToEncounter > 50*50 && isTank)
						{
							ScriptHelpers.SetLeaderMoveToPoi(brannEncounterEntranceLoc);
							return false;
						}

						if (distSqrToEncounter < 30*30)
							ScriptHelpers.MarkBossAsDead("Tribunal of Ages", "Event is over");
					}
					// do nothing if tank and waiting for adds
					return isTank && Targeting.Instance.IsEmpty() && brann != null;
				}
				return false;
			};
		}

		private readonly uint[] _tribualOfAgesChestIds = { 190586, 193996 };

		#region Sjonnir The Ironshaper

		private bool IsLastBossDoorOpen
		{
			get
			{
				WoWGameObject obj = ObjectManager.GetObjectsOfType<WoWGameObject>()
					.FirstOrDefault(g => g.Entry == BossDoorDoodad1);
				if (obj == null)
					return false;

				var door = (obj.SubObj as WoWDoor);
				return door != null && !door.IsClosed;
			}
		}

		private readonly WoWPoint _brannBossDoorLocation = new WoWPoint(1199.685, 667.155, 196.2405);
		private readonly WoWPoint _brannToAEncounterFinishedLoc = new WoWPoint(917.253, 351.925, 203.7064);
		private readonly WoWPoint _brannAtLastBossByConsoleLoc = new WoWPoint(1308.33, 666.755, 189.6078);

		[EncounterHandler(27978, "Sjonnir The Ironshaper", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>> SjonnirTheIronshaperEncounter()
		{
			AddAvoidObject(
				ctx => true,
				10,
				o => o.Entry == SjonnirTheIronshaperId && o.ToUnit().HasAura("Lightning Ring"));
			AddAvoidObject(
				ctx => true,
				6,
				u => u is WoWPlayer && !u.IsMe && (u.ToUnit().HasAura("Static Charge") || Me.HasAura("Static Charge")));

			return async boss =>
			{
				if ((boss == null || !boss.Combat) && !IsLastBossDoorOpen && Targeting.Instance.IsEmpty())
				{
					var brann = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BrannId);
					if (!_talkedToBranAtToA 
						&& (brann == null
						&& Me.Location.DistanceSqr(_brannToAEncounterFinishedLoc) < 30*30)
						|| (brann != null 
						&& brann.Location.DistanceSqr(_brannToAEncounterFinishedLoc) > 40 * 40))
					{
						_talkedToBranAtToA = true;
					}

					var loc = brann != null
						? brann.Location
						: (_talkedToBranAtToA ? _brannBossDoorLocation : _brannToAEncounterFinishedLoc);

					if (Me.Location.DistanceSqr(loc) > 30 * 30)
					{
						ScriptHelpers.SetLeaderMoveToPoi(loc);
						return false;
					}

					return (!ScriptHelpers.WillPullAggroAtLocation(loc) || Me.IsTank())
						&& brann != null && await ScriptHelpers.TalkToNpc(brann);
				}

				return boss != null && boss.Combat
					&& await ScriptHelpers.DispelEnemy(
							"Lightning Shield",
							ScriptHelpers.EnemyDispelType.Magic,
							boss);
			};
		}

		#endregion

	}
}
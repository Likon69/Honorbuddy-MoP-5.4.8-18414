using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Bots.DungeonBuddy.Enums;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class MagistersTerrace : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 198; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(12882.49, -7341.705, 65.53025); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-5.751736, 0.2760417, -0.2375327); }
		}

		public override bool IsComplete
		{
			get 
			{
				return base.IsComplete
					&& (!ScriptHelpers.HasQuest(HardToKillQuestId) || !ScriptHelpers.IsQuestInLogComplete(HardToKillQuestId))
					&& (!ScriptHelpers.HasQuest(ARadicalNotionQuestId) || !ScriptHelpers.IsQuestInLogComplete(ARadicalNotionQuestId))
				&& (!ScriptHelpers.HasQuest(TwistedAssociationsQuestId) || !ScriptHelpers.IsQuestInLogComplete(TwistedAssociationsQuestId));
			}
		}


		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			// if looting bosses then we don't need to proceed.
			if (DungeonBuddySettings.Instance.LootMode != LootMode.Off)
				return; 

			var needItemOffKael = ScriptHelpers.HasQuest(HardToKillQuestId)
				&& !ScriptHelpers.IsQuestInLogComplete(HardToKillQuestId);

			var needItemOffVexallus = ScriptHelpers.HasQuest(ARadicalNotionQuestId)
				&& !ScriptHelpers.IsQuestInLogComplete(ARadicalNotionQuestId); 

			// we only require special loot handling for quest item pickup
			if (!needItemOffKael && !needItemOffVexallus)
				return;

			foreach (var unit in incomingunits.OfType<WoWUnit>())
			{
				// make sure quest items gets looted off bosses.
				if (needItemOffKael && unit.Entry == KaelthasSunstriderId && unit.CanLoot)
					outgoingunits.Add(unit);

				if (needItemOffVexallus && unit.Entry == VexallusId && unit.CanLoot)
					outgoingunits.Add(unit);
			}
		}

		#endregion

		#region Root

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private const uint ARadicalNotionQuestId = 29686;
		private const uint TwistedAssociationsQuestId = 29687;
		private const uint TheScryersScryerQuestId = 11490;
		private const uint HardToKillQuestId = 29685;

		const uint KaelthasSunstriderId = 24664;
		private const uint KalecgosId = 24848;
		const uint VexallusId = 24744;

		[EncounterHandler(55007, "Exarch Larethor", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(24822, "Tyrith", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(24848, "Kalecgos", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public async Task<bool> QuestPickupTurninBehavior(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}


		[ObjectHandler(187578, "Scrying Orb", ObjectRange = 35)]
		public async Task<bool> ScryingOrbHandler(WoWGameObject gObj)
		{
			if (!ScriptHelpers.HasQuest(TheScryersScryerQuestId) )
				return false;

			if (await ScriptHelpers.CancelCinematicIfPlaying())
				return true;

			if (ScriptHelpers.IsQuestInLogComplete(TheScryersScryerQuestId))
			{
				var kalecgos = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == KalecgosId);
				// wait for kalecgos to spawn.
				return kalecgos == null;
			}

			return await ScriptHelpers.InteractWithObject(gObj, 5000);
		}


		[ObjectHandler(188173, "Use object to port outside", ObjectRange = 100)]
		public async Task<bool> PortOutsideFarmMode(WoWGameObject obj)
		{
			return DungeonBuddySettings.Instance.DungeonType == DungeonType.Farm && IsComplete
					&& BotPoi.Current.Type == PoiType.None
					&& await ScriptHelpers.InteractWithObject(obj);
		}

		#endregion

		#region Selin Fireheart

		[EncounterHandler(24723, "Selin Fireheart", Mode = CallBehaviorMode.Proximity)]
		public Func<WoWUnit, Task<bool>> SelinFireheartEncounter()
		{
			var insideDoor = new WoWPoint(220.0599, 0.4775179, -2.847029);
			var doorLeftSide = new WoWPoint(214.8013, 5.930538, -2.826467);
			var doorRightSide = new WoWPoint(214.563, -5.284084, -2.908259);

			return async boss =>
						{
							if (!boss.Combat)
							{
								var clearedRoom = !ScriptHelpers.GetUnfriendlyNpsAtLocation(insideDoor, 25, u => u != boss).Any();
								if (clearedRoom)
								{
									var myLoc = Me.Location;
									if (Me.IsLeader())
									{
										// move in the room if not inside already.
										if (!myLoc.IsPointLeftOfLine(doorLeftSide, doorRightSide)
											|| myLoc.GetNearestPointOnSegment(doorLeftSide, doorRightSide).DistanceSqr(myLoc) < 5*5)
										{
											return (await CommonCoroutines.MoveTo(boss.Location)).IsSuccessful();
										}
										// check if group members are in room.
										if (!Me.PartyMembers.All(p => p.Location.IsPointLeftOfLine(doorLeftSide, doorRightSide)))
										{
											await ScriptHelpers.StopMovingIfMoving();
											return true;
										}
									}
									else
									{
										// force followers to move inside door.
										if (!myLoc.IsPointLeftOfLine(doorLeftSide, doorRightSide)
											|| myLoc.GetNearestPointOnSegment(doorLeftSide, doorRightSide).DistanceSqr(myLoc) < 5*5)
										{
											return (await CommonCoroutines.MoveTo(insideDoor)).IsSuccessful();
										}
									}
								}
								else
								{
									// clear room
									await ScriptHelpers.ClearArea(insideDoor, 25, u => u != boss);
									return false;
								}
							}
							return false;
						};
		}

		#endregion

	}
}
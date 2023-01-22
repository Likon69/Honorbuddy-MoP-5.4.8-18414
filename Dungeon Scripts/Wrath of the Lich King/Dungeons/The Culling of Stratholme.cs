
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using Bots.DungeonBuddy.Enums;
using Buddy.Coroutines;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class TheCullingOfStratholme : Dungeon
	{
		#region Overrides of Dungeon

		private const uint ArthasId = 26499;

		public override uint DungeonId
		{
			get { return 209; }
		}

		private readonly WoWPoint _frontExit = new WoWPoint(1427.404, 564.2408, 38.19347);
		private readonly WoWPoint _endExit = new WoWPoint(2237.993, 1473.944, 132.3637);


		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-8756.695, -4461.556, -200.9562); }
		}

		public override WoWPoint ExitLocation
		{
			get
			{
				var myLoc = Me.Location;
				return myLoc.DistanceSqr(_frontExit) < myLoc.DistanceSqr(_endExit)
					? _frontExit
					: _endExit;
			}
		}

		public override void OnEnter()
		{
			_cratesDone = _gauntletStarted = _arthusSummoned = _gauntlet2Done = false;
			_suspiciousCrates.CycleTo(_suspiciousCrates.First);
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null)
					{
						if (unit.Entry == RisenZombieId && !unit.Combat)
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var isTank = Me.IsTank();

			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (isTank)
					{
						var currentTarget = unit.CurrentTarget;
						if (currentTarget != null && currentTarget.Entry == ArthasId)
						{
							outgoingunits.Add(unit);
						}
					}
					if (!isTank && unit.Entry == RisenZombieId && unit.Combat) // Risen Zombie.. kill these buggers
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			var isTank = Me.IsTank();
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (isTank)
					{
						var currentTarget = unit.CurrentTarget;
						if (currentTarget != null && currentTarget.Entry == ArthasId)
							priority.Score += 150;
					}
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			if (DungeonBuddySettings.Instance.LootMode == LootMode.Off || Me.Combat)
				return;
			foreach (var obj in incomingunits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null && gObj.Entry == MalGanisChestId && gObj.CanUse())
					outgoingunits.Add(gObj);
			}
		}

		#endregion

		#region Root

		private const uint RisenZombieId = 27737;
		private const uint ArcaneDisruptorId = 37888;
		private const uint ChromieOutsideTownId = 27915;

		private readonly CircularQueue<WoWPoint> _suspiciousCrates = new CircularQueue<WoWPoint>
																	{
																		new WoWPoint (1661.9, 869.708, 119.7079),
																		new WoWPoint (1590.184, 655.1675, 101.8955)
																	};


		private bool _arthusSummoned;
		private bool _cratesDone;
		private bool _gauntlet2Done;
		private bool _gauntletStarted;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private WoWUnit Arthas
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == ArthasId); }
		}

		private WoWItem ArcaneDisruptor
		{
			get { return StyxWoW.Me.BagItems.FirstOrDefault(b => b.IsValid && b.Entry == ArcaneDisruptorId); }
		}

		private bool HasArcaneDisruptor
		{
			get { return ArcaneDisruptor != null; }
		}


		readonly Regex _worldStateUIInfoRegex = new Regex(@"[\s,\S]*?(?<num>\d+)");
		int PlaquedCratesRevealed
		{
			get
			{
				var uiStr = Lua.GetReturnVal<string>("return select (4, GetWorldStateUIInfo(1))", 0);
				if (string.IsNullOrEmpty(uiStr))
					return 0;
				var match = _worldStateUIInfoRegex.Match(uiStr);
				if (!match.Success)
					return 0;
				return int.Parse(match.Groups["num"].Value);
			}
		}

		private TimeCachedValue<WoWPoint> _packLoc;
		private WoWPoint PackLoc
		{
			get
			{
				return _packLoc ?? (_packLoc = new TimeCachedValue<WoWPoint>(
					TimeSpan.FromSeconds(1),
					() => GetPackLocFromPoiId(PoiId)));
			}
		}


		[EncounterHandler(26527, "Chromie", Mode = CallBehaviorMode.Proximity, BossRange = 65)]
		[EncounterHandler(30997, "Chromie At End", Mode = CallBehaviorMode.Proximity, BossRange = 65)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		[EncounterHandler(26527, "Chromie", Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> ChromieEncounter(WoWUnit npc)
		{
			if (IsComplete)
				return false;
			// take teleport .
			if (!ScriptHelpers.IsBossAlive("Chrono-Lord Epoch") || _gauntletStarted)
				return await ScriptHelpers.TalkToNpc(npc);

			if (_cratesDone || HasArcaneDisruptor)
				return false;

			// pickup Arcane Disruptor 
			if (!npc.WithinInteractRange)
				return (await CommonCoroutines.MoveTo(npc.Location)).IsSuccessful();
			await ScriptHelpers.TalkToNpc(npc, 1, 1, 1);
			// if we didn't recieve the disruptor than crates have been dealt with.
			if (!await Coroutine.Wait(4000, () => HasArcaneDisruptor))
				_cratesDone = true;
			return true;
		}

		readonly WoWPoint _chromieLoc = new WoWPoint(1550.081, 574.4117, 92.60664);
		private async Task<bool> CratesEncounter()
		{
			if (PlaquedCratesRevealed == 5)
			{
				_cratesDone = true;
				return true;
			}

			var disruptor = ArcaneDisruptor;
			// talk to chromie to get the disruptor
			// The proximity behavior will do the talking..
			if (disruptor == null)
			{
				if (Me.Location.DistanceSqr(_chromieLoc) > 10*10)
					return (await CommonCoroutines.MoveTo(_chromieLoc, "Chromie")).IsSuccessful();
				return false;
			}

			var movetoLoc = _suspiciousCrates.Peek();
			var crate = ObjectManager.GetObjectsOfType<WoWGameObject>()
						.Where(o => o.Entry == SuspiciousGrainCrateId)
						.OrderBy(o => o.DistanceSqr)
						.FirstOrDefault();

			// Do a distance check since there's a bugged create spawn that's underneath terrain.
			if (crate != null)
			{
				if (Navigator.AtLocation(crate.Location))
				{
					disruptor.UseContainerItem();
					return true;
				}
				// if we can't navigate to crate then drop down
				if ((await CommonCoroutines.MoveTo(crate.Location, "Crate")).IsSuccessful())
					return true;
			}
			if (Me.Location.DistanceSqr(movetoLoc) < 15*15)
			{
				_suspiciousCrates.Dequeue();
				movetoLoc = _suspiciousCrates.Peek();
				if (movetoLoc == _suspiciousCrates.First)
					_cratesDone = true;
			}
			return (await CommonCoroutines.MoveTo(movetoLoc)).IsSuccessful();
		}
		const uint SuspiciousGrainCrateId = 190094;
		readonly WoWPoint _questTurninLoc = new WoWPoint(1813.298, 1283.578, 142.2429);
		readonly WoWPoint _chromieStartEventLoc = new WoWPoint(1813.298, 1283.578, 142.2428);
		readonly WoWPoint _arthasStartEventLoc = new WoWPoint(2047.948, 1287.598, 142.8352);

		private async Task<bool> GauntletEncounter()
		{
			WoWUnit arthas = Arthas;
			var isTank = Me.IsTank();
			var gate = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 188686);
			if (gate != null && gate.State == WoWGameObjectState.Active)
			{
				if (ScriptHelpers.IsBossAlive("Chrono-Lord Epoch"))
					ScriptHelpers.MarkBossAsDead("Chrono-Lord Epoch");
				if (!_cratesDone)
					_cratesDone = true;
			}
			WoWPoint packLoc = PackLoc;
			var nearPackLoc = packLoc != WoWPoint.Zero && Me.Location.DistanceSqr(packLoc) <= 10 * 10;

			if (packLoc != WoWPoint.Zero)
			{
				_gauntletStarted = _arthusSummoned = _cratesDone = true;

				// move to pack location
				if (Targeting.Instance.IsEmpty() && !nearPackLoc)
					ScriptHelpers.SetLeaderMoveToPoi(packLoc);
			}

			if (!_cratesDone)
				return await CratesEncounter();

			if (!_arthusSummoned)
			{
				if (arthas == null)
				{
					var chromieAtOutsideTown = ObjectManager.GetObjectsOfType<WoWUnit>()
						.FirstOrDefault(u => u.Entry == ChromieOutsideTownId);
					// turn in Dispelling Illusions and pickup the followup.
					if (ScriptHelpers.SupportsQuesting && ScriptHelpers.IsQuestInLogComplete(13149))
					{
						if (chromieAtOutsideTown == null && !Navigator.AtLocation(_questTurninLoc))
							return (await CommonCoroutines.MoveTo(_questTurninLoc, "Chromie to turnin quest")).IsSuccessful();
						if (chromieAtOutsideTown != null)
							return await ScriptHelpers.TurninQuest(chromieAtOutsideTown);
					}
					if (chromieAtOutsideTown != null
						&& chromieAtOutsideTown.HasQuestAvailable()
						&& await ScriptHelpers.PickupQuest(chromieAtOutsideTown))
					{
						return true;
					}

					// summon arthas.
					if (_cratesDone)
					{
						if (!Navigator.AtLocation(_chromieStartEventLoc))
							return (await CommonCoroutines.MoveTo(_chromieStartEventLoc, "Chromie to start event")).IsSuccessful();
						if (chromieAtOutsideTown != null)
							await ScriptHelpers.TalkToNpc(chromieAtOutsideTown, 1, 1, 1, 1);
						_arthusSummoned = true;
					}
				}
				else
				{
					_arthusSummoned = true;
				}
			}
			var needToStartEvent = arthas != null && arthas.Location.DistanceSqr(_arthasStartEventLoc) <= 3*3;
			if (needToStartEvent)
				_gauntletStarted = false;

			// talk to arthus to start the event.
			if (!_gauntletStarted && _arthusSummoned)
			{
				if (Me.Location.DistanceSqr(_arthasStartEventLoc) > 25*25)
				{
					ScriptHelpers.SetLeaderMoveToPoi(_arthasStartEventLoc);
				}
				else
				{
					if (needToStartEvent && Me.IsTank() && Me.PartyMembers.All(u => u.DistanceSqr <= 40*40))
					{
						if (!arthas.WithinInteractRange)
							return await ScriptHelpers.TalkToNpc(arthas);

						if (await ScriptHelpers.TalkToNpc(arthas))
							_gauntletStarted = true;
					}
					// wait for Arthas
					return true;
				}
			}

			// Wait for packs to spawn.	
			if ((packLoc == WoWPoint.Zero || nearPackLoc) && isTank && _gauntletStarted && Targeting.Instance.IsEmpty())
				return true;
			return false;
		}

		static readonly WoWPoint PackLoc2105 = new WoWPoint(2350.629, 1186.954, 130.4522);
		static readonly WoWPoint PackLoc2106 = new WoWPoint(2253.875, 1169.751, 138.283);
		static readonly WoWPoint PackLoc2107 = new WoWPoint(2176.074, 1253.801, 135.1181);
		static readonly WoWPoint PackLoc2108 = new WoWPoint(2204.891, 1331.051, 129.4392);
		static readonly WoWPoint PackLoc2109 = new WoWPoint(2134.766, 1355.917, 132.0546);

		static int PoiId
		{
			get { return Lua.GetReturnVal<int>("return select(10, GetMapLandmarkInfo(1))", 0);}
		}

		static WoWPoint GetPackLocFromPoiId(int poiId)
		{
			switch (poiId)
			{
				case 2105:
					return PackLoc2105;
				case 2106:
					return PackLoc2106;
				case 2107:
					return PackLoc2107;
				case 2108:
					return PackLoc2108;
				case 2109:
					return PackLoc2109;
				default:
					return WoWPoint.Zero;
			}
		}

		#endregion


		[EncounterHandler(26532, "Chrono-Lord Epoch", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>>  ChronoLordEpochEncounter()
		{
			var arthasStartLoc = new WoWPoint(2366.24, 1195.253, 131.9611);
			var arthasEndLoc = new WoWPoint(2425.898, 1118.842, 148.0759);

			return async boss =>
			{
				if (ScriptHelpers.IsBossAlive("Meathook") 
					|| ScriptHelpers.IsBossAlive("Salramm the Fleshcrafter"))
				{
					return await GauntletEncounter();
				}
				var arthus = Arthas;
				if (arthus == null || arthus.Location.DistanceSqr(arthasEndLoc) > 10*10)
				{
					return await ScriptHelpers.TankTalkToAndEscortNpc(arthus, arthasStartLoc, 10, 1, 1);
				}
				return false;
			};
		}


		[EncounterHandler(26533, "Mal'Ganis", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>> MalGanisGaunletEncounter()
		{
			var arthasStartLoc = new WoWPoint(2425.898, 1118.842, 148.0759);
			var arthasGauntletStartLoc = new WoWPoint(2534.988, 1126.163, 130.7619);
			var arthasGauntletEndLoc = new WoWPoint(2363.44, 1404.906, 128.7849);

			return async boss =>
			{
				var arthas = Arthas;

				if (arthas != null)
				{
					var arthasLoc = arthas.Location;

					if (arthasLoc.DistanceSqr(arthasStartLoc) <= 3 * 3 && arthas.CanGossip)
						return await ScriptHelpers.TalkToNpc(arthas);

					if (arthasLoc.DistanceSqr(arthasGauntletEndLoc) <= 3 * 3)
					{
						_gauntlet2Done = true;
						return await ScriptHelpers.TalkToNpc(arthas);
					}
				}
				else if (!_gauntlet2Done && Me.Location.DistanceSqr(arthasGauntletStartLoc) < 5 * 5)
				{
					// Arthas will despawn momentarily when arriving at that this location so pause 
					// for a few seconds and verify he is gone for good.
					await Coroutine.Sleep(4000);
					if (Arthas == null)
						_gauntlet2Done = true;
				}

				return !_gauntlet2Done && await ScriptHelpers.TankTalkToAndEscortNpc(arthas, arthasGauntletStartLoc);
			};
		}

		private const uint MalGanisChestId = 190663;

		[EncounterHandler(26533, "Mal'Ganis")]
		public Func<WoWUnit, Task<bool>> MalGanisEncounter()
		{
			var bossFrontalArc = new ScriptHelpers.AngleSpan(0, 180);
			return async boss =>
			{
				var isTank = Me.IsTank();
				var isTargetingMe = boss.CurrentTargetGuid == Me.Guid;
				if (!isTank && !isTargetingMe && !boss.IsMoving && boss.Distance < 15 
					&& await ScriptHelpers.AvoidUnitAngles(boss, bossFrontalArc))
				{
					return true;
				}
				return isTank && isTargetingMe && await ScriptHelpers.TankFaceUnitAwayFromGroup(15);
			};
		}

		WoWUnit ChromieAtEnd
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 30997); }
		}

		[LocationHandler(2306.77, 1496.78, 128.367, 50)]
		public async Task<bool> WaitForChromie(WoWPoint loc)
		{
			if (!Targeting.Instance.IsEmpty()
				|| !ScriptHelpers.IsQuestInLogComplete(13151)
				|| ChromieAtEnd != null)
			{
				return false;
			}
			return await Coroutine.Wait(40000, () => ChromieAtEnd != null);
		}

	}
}
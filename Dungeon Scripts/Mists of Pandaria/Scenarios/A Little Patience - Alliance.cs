
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
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
	public class ALittlePatienceAlliance : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 589; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == KorkronCommandoId && unit.HasAura("Whirlwind") && Me.IsMelee())
							return true;
						if (unit.Entry == KorkronAssailantId)
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			if (Me.HasAura("Nodded Off"))
				_seekSpiritKissedSpirtTimer.Reset();
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == SpiritKissedSpriteId && !Me.Combat && !_seekSpiritKissedSpirtTimer.IsFinished && !Me.BagItems.Any(i => i.Entry == SpiritKissedWaterId) &&
						Me.IsTank())
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
					if (unit.Entry == SpiritKissedSpriteId)
						priority.Score = 200 - unit.Distance;
				}
			}
		}

		public override void OnEnter()
		{
			campLoc = WoWPoint.Zero;
			waitForAttackTimer = null;
		}

		#endregion

		private const uint CommanderScargashId = 68474;
		private const uint SwampGasId = 68816;
		private readonly WaitTimer _refreshLandMarksTimer = new WaitTimer(TimeSpan.FromSeconds(1));
		private WaitTimer waitForAttackTimer;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private List<WoWLandMark> LandMarks
		{
			get
			{
				if (_refreshLandMarksTimer.IsFinished)
				{
					_refreshLandMarksTimer.Reset();
					StyxWoW.Landmarks.Refresh();
				}
				return StyxWoW.Landmarks.LandmarkList;
			}
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidObject(ctx => true, 5, SwampGasId);
			return new PrioritySelector();
		}

		[EncounterHandler(68474, "Commander Scargash")]
		public Composite CommanderScargashEncounter()
		{
			AddAvoidObject(ctx => true, o => Me.IsMoving ? 25 : 15, o => o.Entry == CommanderScargashId && o.ToUnit().CurrentTargetGuid == Me.Guid);
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[ScenarioStage(2)]
		public Composite StageTwoBehavior()
		{
			return new PrioritySelector(
				new Decorator(
					ctx => waitForAttackTimer == null,
					new Action(
						ctx =>
						{
							waitForAttackTimer = new WaitTimer(TimeSpan.FromSeconds(20));
							waitForAttackTimer.Reset();
						})),
				new Decorator(ctx => Targeting.Instance.IsEmpty() && !waitForAttackTimer.IsFinished, new ActionAlwaysSucceed()));
		}

		#region Stage One

		private const uint KorkronAssailantId = 67818;
		private const uint SpiritKissedWaterId = 92751;
		private const uint MeatyHaunchId = 92444;
		private const uint BrittleRootId = 91906;
		private const uint BattleRationsId = 91971;
		private const uint SnakeOilId = 92470;
		private const uint SpiritKissedSpriteId = 68347;
		private const uint LiquidFireId = 92745;
		private const uint JungleHobsId = 92750;
		private const uint MasterBrownstoneId = 68376;
		private const string MapVehicleLocationLuaFormat = @"local _, left, top, right, bot = GetCurrentMapZone() 
local x, y,_,_,i = GetBattlefieldVehicleInfo({0})
y, x =(1.0-x)*left+x*right , (1.0-y)*top+y*bot 
local isDone=0;
if i == 'Trap Gold' then isDone = 1 end
return x,y,isDone
";
		private const uint PandarenConstructionSiteId = 68895;

		private const uint NourishmentId = 68423;
		private const uint WoodpileId = 68419;
		private const uint FoodId = 68672;
		private const uint MoundofSoilId = 68709;
		private const uint BurgeoningSaplingId = 68699;
		private const uint KorkronCommandoId = 67688;
		private const uint CannonBarrelId = 68100;
		private const uint SustenanceId = 68082;
		private const uint CannonballsId = 68112;
		private const uint KrasariIronSupplyId = 68343;
		private const uint VittlesId = 68270;
		private const uint PandarenKegId = 68269;
		private const uint DragonCannonId = 68279;
		private const uint BlastbrewCaudronId = 68271;
		private const uint WakenedMoguId = 68102;

		private const uint SnacksId = 68481;

		private const uint RiverbladeMarauderId = 68775;
		private const uint BogrotId = 67974;

		private readonly uint[] ConstructionSitesIds = new[]
													   {
														   NourishmentId, WoodpileId, FoodId, MoundofSoilId, BurgeoningSaplingId, CannonBarrelId, SustenanceId, CannonballsId,
														   KrasariIronSupplyId, VittlesId, PandarenKegId, DragonCannonId, BlastbrewCaudronId, SnacksId, PandarenConstructionSiteId
													   };

		private readonly uint[] _attackerIds = new[] { RiverbladeMarauderId, BogrotId, WakenedMoguId, KorkronCommandoId };
		private readonly uint[] _stageOneItems = new[] { MeatyHaunchId, BrittleRootId, BattleRationsId, LiquidFireId };
		private WaitTimer _seekSpiritKissedSpirtTimer = new WaitTimer(TimeSpan.FromSeconds(15));

		private WoWPoint campLoc = WoWPoint.Zero;

		[ScenarioStage(1, "Prepare the Defenses")]
		public Composite StageOneBehavior()
		{
			ScenarioStage stage = null;
			return new PrioritySelector(ctx => stage = ctx as ScenarioStage, StartEventBehavior(), DefendCampsBehavior());
		}

		private Composite StartEventBehavior()
		{
			const int gossipIconIndex = 193;
			WoWLandMark gossipLandMark = null;
			WoWUnit gossipNpc = null;
			return
				new PrioritySelector(
					new Decorator(
						ctx =>
						Targeting.Instance.IsEmpty() &&
						(gossipLandMark = LandMarks.Where(l => l.TextureIndex == gossipIconIndex).OrderBy(l => l.Location.DistanceSqr(Me.Location)).FirstOrDefault()) != null,
						new PrioritySelector(
							ctx => gossipNpc = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.CanGossip && u.Location.Distance(gossipLandMark.Location) < 20),
							new Decorator(ctx => gossipNpc == null, new Action(ctx => Navigator.MoveTo(gossipLandMark.Location))),
							new Decorator(ctx => gossipNpc != null, ScriptHelpers.CreateTalkToNpc(ctx => gossipNpc)))));
		}

		private Composite DefendCampsBehavior()
		{
			WoWItem item = null;
			WoWUnit site = null;
			WoWUnit enemy = null;
			WoWUnit masterBrownstone = null;
			var masterBrownstoneLoc = new WoWPoint(-1239.139, 877.9915, 13.91179);

			return new PrioritySelector(
				new Decorator(ctx => campLoc == WoWPoint.Zero, new Action(ctx => campLoc = GetNextCampLocation())),
				new Decorator(
					ctx => campLoc != WoWPoint.Zero && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						ctx =>
						{
							item = Me.BagItems.FirstOrDefault(i => _stageOneItems.Contains(i.Entry));
							return
								site =
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .Where(
												 u =>
												 ConstructionSitesIds.Contains(u.Entry) && u.Location.Distance(campLoc) < 100 &&
												 Navigator.CanNavigateFully(Me.Location, u.Location))
											 .OrderBy(u => u.DistanceSqr)
											 .FirstOrDefault();
						},
				// clear camp of hostile NPCs
						new Decorator(
							ctx =>
							(enemy = ScriptHelpers.GetUnfriendlyNpsAtLocation(campLoc, 150, u => _attackerIds.Contains(u.Entry)).FirstOrDefault()) != null &&
							BotPoi.Current.NoPoi(),
							new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(enemy.Location))),
				// dump a bucket of water on Master Brownstone.
						new Decorator(
							ctx => Me.HasAura("Nodded Off") && Me.BagItems.Any(i => i.Entry == SpiritKissedWaterId),
							new PrioritySelector(
								ctx => masterBrownstone = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == MasterBrownstoneId),
								new Decorator(
									ctx => masterBrownstone != null && masterBrownstone.WithinInteractRange,
									new Action(
										ctx =>
										{
											masterBrownstone.Interact();
											_seekSpiritKissedSpirtTimer.Stop();
										})),
								new Decorator(
									ctx => masterBrownstone != null && !masterBrownstone.WithinInteractRange, new Action(ctx => Navigator.MoveTo(masterBrownstone.Location))),
								new Decorator(ctx => masterBrownstone == null, new Action(ctx => Navigator.MoveTo(masterBrownstoneLoc))))),
				// use items at camp..
						new Decorator(
							ctx => item != null && site != null,
							new PrioritySelector(
								new Decorator(ctx => site.Distance > 35, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(site.Location))),
								new Decorator(ctx => site.Distance <= 35 && site.Distance > 3, new Action(ctx => Navigator.MoveTo(site.Location))),
								new Decorator(
									ctx => site.Distance <= 3,
									new PrioritySelector(
										new PrioritySelector(
											new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
											new Decorator(ctx => Me.Mounted, new ActionRunCoroutine(ctx => CommonCoroutines.Dismount())),
											new Action(ctx => item.UseContainerItem())))))),
				// move to next camp
						new Decorator(
							ctx => item == null || site == null,
							new PrioritySelector(
								new Decorator(ctx => campLoc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(campLoc))),
								new Decorator(ctx => campLoc.Distance(Me.Location) <= 5, new Action(ctx => campLoc = GetNextCampLocation())))))));
		}

		private WoWPoint GetNextCampLocation()
		{
			return GetCampLocations().OrderByDescending(l => l.Distance2DSqr(Me.Location)).FirstOrDefault();
		}

		[ObjectHandler(216227, "Brittle Root", ObjectRange = 45)]
		public Composite BrittleRootHandler()
		{
			WoWGameObject root = null;
			return new PrioritySelector(
				ctx => root = ctx as WoWGameObject,
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && GetItemCount(BrittleRootId) < 5 && ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 1,
					new PrioritySelector(
						new Decorator(ctx => root.WithinInteractRange, new Action(ctx => root.Interact())),
						new Decorator(ctx => !root.WithinInteractRange, new Action(ctx => Navigator.MoveTo(root.Location))))));
		}

		private int GetItemCount(uint itemId)
		{
			return (int)Me.BagItems.Sum(i => i != null && i.ItemInfo != null && i.Entry == itemId ? i.StackCount : 0);
		}

		[EncounterHandler(68102, "Wakened Mogu")]
		public Composite WakenedMoguEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(ctx => unit = ctx as WoWUnit);
		}

		[EncounterHandler(67688, "Kor'kron Commando")]
		public Composite KorkronCommandoEncounter()
		{
			WoWUnit unit = null;
			AddAvoidObject(ctx => true, 8, o => o.Entry == KorkronCommandoId && o.ToUnit().HasAura("Whirlwind"));
			return new PrioritySelector(ctx => unit = ctx as WoWUnit, new Decorator(ctx => Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}


		private List<WoWPoint> GetCampLocations()
		{
			var list = new List<WoWPoint>();
			using (StyxWoW.Memory.AcquireFrame())
			{
				var numberOfVehicles = Lua.GetReturnVal<int>("return GetNumBattlefieldVehicles()", 0);
				for (int i = 1; i <= numberOfVehicles; i++)
				{
					var rawLoc = Lua.GetReturnValues(string.Format(MapVehicleLocationLuaFormat, i));
					if (rawLoc[2] == "1")
						continue;
					var x = float.Parse(rawLoc[0], CultureInfo.InvariantCulture);
					var y = float.Parse(rawLoc[1], CultureInfo.InvariantCulture);
					var heights = Navigator.FindHeights(x, y);
					var myLoc = Me.Location;
					var z = heights != null && heights.Any() ? heights.OrderBy(h => Math.Abs(h - myLoc.Z)).FirstOrDefault() : 0;
					list.Add(new WoWPoint(x, y, z));
				}
			}
			return list;
		}

		#endregion
	}
}
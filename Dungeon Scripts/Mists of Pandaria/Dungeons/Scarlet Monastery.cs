
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Patchables;
using Styx.TreeSharp;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWCache;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	#region Normal Difficulty

	public class ScarletMonastery : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 164; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(2920.317, -799.8921, 160.3323); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(1124.471, 504.2796, 0.9892024); }
		}

		public override void OnEnter()
		{
			_checkForZealot = false;
			_eastPoolSideBs =
				new DynamicBlackspot(
					() => ScriptHelpers.GetUnfriendlyNpsAtLocation(_eastPoolSideAvoidLoc, 15).Any(),
					() => _eastPoolSideAvoidLoc,
					LfgDungeon.MapId,
					15,
					name: "East pool trash");
			_westPoolSideBs =
				new DynamicBlackspot(
					() => ScriptHelpers.GetUnfriendlyNpsAtLocation(_westPoolSideAvoidLoc, 15).Any(),
					() => _westPoolSideAvoidLoc,
					LfgDungeon.MapId,
					15,
					name: "West pool trash");
			DynamicBlackspotManager.AddBlackspot(_eastPoolSideBs);
			DynamicBlackspotManager.AddBlackspot(_westPoolSideBs);
		}

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspot(_eastPoolSideBs);
			DynamicBlackspotManager.RemoveBlackspot(_westPoolSideBs);
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null)
					{
						if (unit.Combat && !unit.TaggedByMe && !unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
							return true;
						if (unit.Entry == TrainingDummyId || unit.Entry == TrainingDummyId2)
							return true;
						// ignore npcs that are fighting other npcs
						if ((unit.Entry == ScarletCenturionId || unit.Entry == ZombifiedCorpseId || unit.Entry == PileOfCorpsesId) && !unit.TaggedByMe && unit.Combat &&
							Me.Combat)
							return true;
						// ignore brother korloff if he's near other unfriendlies
						if (unit.Entry == BrotherKorloffId && !unit.Combat &&
							ScriptHelpers.GetUnfriendlyNpsAtLocation(unit.Location, 30, u => u.Entry != BrotherKorloffId).Any())
							return true;
						// ignore brother korloff if I'm melee and he's casting firestorm kick
						if (unit.Entry == BrotherKorloffId && unit.CastingSpellId == FirestormKickId && Me.IsMelee())
							return true;
						// kill this zealot before engaging CommanderDurand
						if (_commanderDurandIds.Contains(unit.Entry) && !unit.Combat && _checkForZealot)
							return true;
					}
					return false;
				});
		}

		public override bool IsComplete
		{
			get
			{
				return (!ScriptHelpers.SupportsQuesting || !ScriptHelpers.HasQuest(UntoDustThouShaltReturnQuestId)
					|| _waitForQuestToCompleteTime.IsFinished) && base.IsComplete;
			}
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == ScarletZealot && !Me.Combat && Me.IsTank() && unit.Location.DistanceSqr(_scarletZealotLoc) <= 10 * 10)
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
					if (unit.Entry == PileOfCorpsesId)
						priority.Score += 300;

					else if (unit.Entry == EvictedSoul)
						priority.Score += 1000;
					else if (unit.Entry == ThalnostheSoulrenderId && Me.IsDps())
						priority.Score += 900;
				}
			}
		}

		#region MoveTo override
		// will try to fix this in the mesh.. pita

		//readonly WoWPoint _poolMoveto =new WoWPoint(974.1915, 605.1055, 0.8462725);
		//readonly WoWPoint _poolctmTo = new WoWPoint(975.623, 605.201, 0.8462954);
		//private bool _performPoolJump;
		//public override MoveResult MoveTo(WoWPoint location)
		//{
		//	var myLoc = Me.Location;
		//	// check if bot should jump into the pool!
		//	if (myLoc.X > 978 && (location.X < 942 || (location.X < 975 && location.Y > 590 && location.Y < 620)))
		//	{
		//		_performPoolJump = true;
		//	}
		//	return base.MoveTo(location);
		//}

		#endregion


		#endregion

		private const uint PileOfCorpsesId = 59722;
		private const uint EvictedSoul = 59974;
		private const uint ScarletCenturionId = 59746;
		private const uint ZombifiedCorpseId = 59771;
		private const uint TrainingDummyId = 60197;
		private const uint TrainingDummyId2 = 64446;
		private const uint BrotherKorloffId = 59223;
		private const uint ScorchedEarth = 59507;
		private const uint ScarletZealot = 58590;
		private readonly uint[] _commanderDurandIds = { 60040 };
		private const int FirestormKickId = 113764;
		private const int UntoDustThouShaltReturnQuestId = 31516;
		private const uint HighInquisitorWhitemaneId = 3977;
		private const uint BladesOfTheAnointedId = 87390;

		private readonly WoWPoint _scarletZealotLoc = new WoWPoint(760.7621, 551.4827, 12.80546);
		private bool _checkForZealot;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public async Task<bool> RootHandler(WoWUnit npc)
		{
			return await UntoDustThouShaltReturnQuestHandler();
		}

		#region Path Avoidance

		private readonly WoWPoint _westPoolSideAvoidLoc = new WoWPoint(937.1406, 625.408, 1.160698);
		private readonly WoWPoint _eastPoolSideAvoidLoc = new WoWPoint(939.0349, 584.8527, 1.177113);

		private DynamicBlackspot _westPoolSideBs;
		private DynamicBlackspot _eastPoolSideBs;
		#endregion


		[EncounterHandler(59706, "Fuel Tank", Mode = CallBehaviorMode.Proximity, BossRange = 10)]
		public async Task<bool> FuelTankEncounter(WoWUnit fuelTank)
		{
			return fuelTank.TransportGuid == 0 && Me.Combat && await ScriptHelpers.InteractWithObject(fuelTank, 0, true);
		}

		#region Quest

		[EncounterHandler(64842, "Lilian Voss", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(64839, "Lilian Voss", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(64838, "Hooded Crusader", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(64827, "Hooded Crusader", Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable()
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		[EncounterHandler(64855, "Blade of the Anointed", Mode = CallBehaviorMode.Proximity, BossRange = 55)]
		[EncounterHandler(64854, "Blade of the Anointed", Mode = CallBehaviorMode.Proximity, BossRange = 55)]
		public async Task<bool> BladeoftheAnointedEncounter(WoWUnit npc)
		{
			if (!npc.HasQuestAvailable() && !npc.HasQuestTurnin())
				return false;

			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;

			var pathDist = Navigator.PathDistance(Me.Location, npc.Location);
			if (!pathDist.HasValue || pathDist.Value > 55)
				return false;

			return npc.HasQuestAvailable()
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		[ObjectHandler(214296, "Blade of the Anointed")]
		[ObjectHandler(214284, "Blade of the Anointed")]
		public async Task<bool> BladeoftheAnointedHandler(WoWGameObject blade)
		{
			return ScriptHelpers.SupportsQuesting && blade.CanLoot && !blade.InUse && !Me.Combat
				&& !ScriptHelpers.WillPullAggroAtLocation(blade.Location)
				&& await ScriptHelpers.InteractWithObject(blade, 2000);
		}

		readonly WaitTimer _waitForQuestToCompleteTime = new WaitTimer(TimeSpan.FromSeconds(30));
		public async Task<bool> UntoDustThouShaltReturnQuestHandler()
		{
			if (!ScriptHelpers.SupportsQuesting
				|| !ScriptHelpers.HasQuest(UntoDustThouShaltReturnQuestId)
				|| ScriptHelpers.IsQuestInLogComplete(UntoDustThouShaltReturnQuestId))
			{
				return false;
			}

			WoWUnit whiteMane = ObjectManager.GetObjectsOfType<WoWUnit>()
				.FirstOrDefault(u => u.Entry == HighInquisitorWhitemaneId && u.IsDead);

			var questItem = Me.BagItems.FirstOrDefault(u => u.Entry == BladesOfTheAnointedId);

			if (whiteMane == null || questItem == null)
				return false;

			if (whiteMane.DistanceSqr > 4.5 * 4.5)
				return (await CommonCoroutines.MoveTo(whiteMane.Location)).IsSuccessful();
			await ScriptHelpers.StopMovingIfMoving();
			questItem.UseContainerItem();
			_waitForQuestToCompleteTime.Reset();
			await CommonCoroutines.SleepForLagDuration();
			return true;
		}

		#endregion

		[EncounterHandler(59223, "Brother Korloff", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite BrotherKorloffEncounter()
		{
			WoWUnit boss = null;
			const int blazingFists = 114807;
			WoWUnit[] trash = null;

			AddAvoidObject(
				ctx => true,
				30,
				o =>
				o.Entry == BrotherKorloffId && !o.ToUnit().Combat && o.ToUnit().IsAlive &&
				ScriptHelpers.GetUnfriendlyNpsAtLocation(o.Location, 30, u => u.Entry != BrotherKorloffId).Any());

			AddAvoidObject(ctx => true, 10, u => u.Entry == BrotherKorloffId && u.ToUnit().CastingSpellId == FirestormKickId && u.ToUnit().CurrentTargetGuid != Me.Guid);
			AddAvoidObject(ctx => true, 6, u => u.Entry == BrotherKorloffId && u.ToUnit().CastingSpellId == blazingFists && u.ToUnit().CurrentTargetGuid != Me.Guid, u => u.Location.RayCast(u.Rotation, 3));
			AddAvoidObject(ctx => true, 3, ScorchedEarth);

			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(boss.Location, 40, u => u.Entry != BrotherKorloffId).ToArray();
					return boss;
				},
				new Decorator(
					ctx => !boss.Combat && trash.Any() && Me.IsTank() && !Me.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => trash.Any(t => t.Location.Distance(boss.Location) >= 25),
							ScriptHelpers.CreateClearArea(() => boss.Location, 40, u => u != boss && u.Location.Distance(boss.Location) >= 25)),
						new Decorator(ctx => !trash.Any(t => t.Location.Distance(boss.Location) >= 25), new ActionAlwaysSucceed()))),
				new Decorator(ctx => boss.Combat && Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		/* disabled due to pathising issues. 
		[EncounterHandler(64855, "Blade of the Anointed", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(64854, "Blade of the Anointed", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite BladeoftheAnointedEncounter()
		{
			WoWUnit blade = null;

			return new PrioritySelector(ctx => blade = ctx as WoWUnit,
				new Decorator(ctx => blade.QuestGiverStatus == QuestGiverStatus.TurnIn && Targeting.Instance.IsEmpty() && !ScriptHelpers.WillPullAggroAtLocation(blade.Location),
					ScriptHelpers.CreateTurninQuest(ctx => blade)),

				new Decorator(ctx => blade.QuestGiverStatus == QuestGiverStatus.Available && Targeting.Instance.IsEmpty() && !ScriptHelpers.WillPullAggroAtLocation(blade.Location),
					ScriptHelpers.CreatePickupQuest(ctx => blade))
					);
		}

		 */

		[LocationHandler(836.4258f, 605.6751f, 13.26288f, 30, "Pull Groups out of chapel")]
		public Composite ClearChapelBehavior()
		{
			var waitLoc = new WoWPoint(836.4258f, 605.6751f, 13.26288f);
			var pullToLoc = new WoWPoint(837.4616, 590.2504, 13.28209);

			WoWUnit pullUnit = null;
			return new PrioritySelector(
				ctx => pullUnit = ScriptHelpers.GetUnfriendlyNpsAtLocation(waitLoc, 35).FirstOrDefault(),
				ScriptHelpers.CreatePullNpcToLocation(
					ctx => !ScriptHelpers.IsBossAlive("Brother Korloff") && pullUnit != null, ctx => true, ctx => pullUnit, ctx => pullToLoc, ctx => waitLoc, 5));
		}

		[EncounterHandler(60040, "Commander Durand", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(60106, "Commander Durand", Mode = CallBehaviorMode.Proximity)]
		public Composite CommanderDurandEncounter()
		{
			//WoWUnit zealot = null;
			return new PrioritySelector(
				// kill the scarlet zealot that rezes a Judicator before engaging Commander Durand.
				new Decorator(
					ctx => Me.IsTank() && !Me.Combat && !_checkForZealot,
					new PrioritySelector(
						new Decorator(ctx => Me.Location.Distance(_scarletZealotLoc) <= 10, new Action(context => _checkForZealot = true)),
						new Decorator(ctx => Me.Location.Distance(_scarletZealotLoc) > 10, new Action(context => ScriptHelpers.SetLeaderMoveToPoiPS(_scarletZealotLoc))))));
		}

		[EncounterHandler(3977, "High Inquisitor Whitemane")]
		public Composite HighInquisitorWhitemaneEncounter()
		{
			WoWUnit boss = null;
			const int massResurrection = 113134;
			const int heal = 12039;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateInterruptCast(ctx => boss, massResurrection, heal),
				ScriptHelpers.CreateDispelGroup("Power Word: Shield", ScriptHelpers.PartyDispelType.Magic));
		}

		#region Thalnos the Soulrender

		private const uint ThalnostheSoulrenderId = 59789;

		[EncounterHandler(59789, "Thalnos the Soulrender")]
		public Composite ThalnostheSoulrenderEncounter()
		{
			const int spiritGaleSpellId = 115289;
			const uint spiritGaleObjectId = 2857;
			WoWUnit boss = null;
			AddAvoidObject(ctx => true, 4, spiritGaleObjectId);

			AddAvoidLocation(
				ctx => true,
				2,
				m => ((WoWMissile)m).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => boss != null && boss.IsValid && m.CasterGuid == boss.Guid && m.SpellId == spiritGaleSpellId));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Evict Soul", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateInterruptCast(ctx => boss, spiritGaleSpellId));
		}

		#endregion
	}

	#endregion

	#region Heroic Difficulty

	public class ScarletMonasteryHeroic : ScarletMonastery
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 474; }
		}

		#endregion
	}

	#endregion

}
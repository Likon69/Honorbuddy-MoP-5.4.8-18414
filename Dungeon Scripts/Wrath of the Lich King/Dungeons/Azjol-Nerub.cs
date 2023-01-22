using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class AzjolNerub : Dungeon
	{
		#region Overrides of Dungeon

		private const float TopLevelZ = 665;
		private const float MiddleLevelZ = 640;
		private readonly WoWPoint _middleLevelCtmLoc = new WoWPoint(506.6927,548.0245,654.8941);

		public override uint DungeonId
		{
			get { return 204; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(3670.406, 2174.702, 36.43874); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint (408.9006, 799.6625, 832.2418); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null)
					{
						if ((unit.IsTargetingMyPartyMember || unit.IsTargetingMeOrPet) && unit.DistanceSqr > 30 * 30)
							return true;
						if (unit.Entry == 29209 && StyxWoW.Me.IsTank())
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
					if (unit.Entry == 28619)
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
					if (unit.Entry == WebWrapId && StyxWoW.Me.IsDps())
						priority.Score += 500;

					if (unit.Entry == HadronoxId && StyxWoW.Me.IsDps() && unit.TaggedByMe)
						priority.Score += 500;
				}
			}
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			var myLevel = myLoc.Z > TopLevelZ ? 2 : myLoc.Z > MiddleLevelZ ? 1 : 0;
			var destinationLevel = location.Z > TopLevelZ ? 2 : location.Z > MiddleLevelZ ? 1 : 0;
			// CTM of the pile at center level. 
			if (myLevel == 1 && myLoc.Z > 661)
			{
				Navigator.PlayerMover.MoveTowards(_middleLevelCtmLoc);
				return MoveResult.Moved;
			}
			if (myLevel < destinationLevel)
			{
				// we can't travel to a higher level so we need to port out and back in.
				ScriptHelpers.TelportOutsideLfgInstance();
				Navigator.NavigationProvider.Clear();
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		#endregion

		private const uint WebWrapId = 28619;
		private const uint WatcherSilthikId = 28731;
		private const uint WatcherGashraId = 28730;
		private const uint WatcherNarjilId = 28729;
		private const uint HadronoxId = 28921;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(55564, "Reclaimer A'zak", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			const int deathOfTheTraitorKingQuestId = 29807;
			const int theGatewatchersTalismanQuestId = 29811;

			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					new PrioritySelector(
						new Decorator(
							ctx => !Me.QuestLog.ContainsQuest(deathOfTheTraitorKingQuestId) && !Me.QuestLog.GetCompletedQuests().Contains(deathOfTheTraitorKingQuestId),
							ScriptHelpers.CreatePickupQuest(ctx => unit, deathOfTheTraitorKingQuestId)),
						new Decorator(
							ctx => !Me.QuestLog.ContainsQuest(theGatewatchersTalismanQuestId) && !Me.QuestLog.GetCompletedQuests().Contains(theGatewatchersTalismanQuestId),
							ScriptHelpers.CreatePickupQuest(ctx => unit, theGatewatchersTalismanQuestId)))),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(28684, "Krik'thir the Gatewatcher", Mode = CallBehaviorMode.Proximity, BossRange = 65)]
		public Composite KrikthirTheGatewatcherEncounter()
		{
			WoWUnit gashra = null, narjil = null;
			WoWUnit boss = null;
			var tankLoc = new WoWPoint(541.2601, 701.8342, 776.805);
			return new PrioritySelector(
				ctx =>
				{
					boss = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == WatcherSilthikId && u.IsAlive);
					gashra = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == WatcherGashraId && u.IsAlive);
					narjil = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == WatcherNarjilId && u.IsAlive);
					return ctx as WoWUnit;
				},
				ScriptHelpers.CreatePullNpcToLocation(ctx => boss != null, ctx => boss, ctx => tankLoc, 10),
				ScriptHelpers.CreatePullNpcToLocation(ctx => gashra != null, ctx => gashra, ctx => tankLoc, 10),
				ScriptHelpers.CreatePullNpcToLocation(ctx => narjil != null, ctx => narjil, ctx => tankLoc, 10));
		}

		[EncounterHandler(28921, "Hadronox", Mode = CallBehaviorMode.Proximity, BossRange = 150)]
		public Composite HadronoxEncounter()
		{
			WoWUnit boss = null;
			WoWUnit trash = null;
			const uint acidCloudId = 53400;

			var trashTankSpot = new WoWPoint(507.6383, 515.5826, 748.325);
			var trashLoc = new WoWPoint(529.6913, 547.1257, 731.8326);
			AddAvoidObject(ctx => !Me.IsCasting, 5, o => o.Entry == acidCloudId && o.ZDiff < 10);
			var trashIds = new uint[] { 29117, 28922, 29118 };

			return new PrioritySelector(
				ctx =>
				{
					trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashLoc, 17, u => trashIds.Contains(u.Entry)).FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => !boss.TaggedByMe && !boss.IsTargetingMeOrPet && !boss.IsTargetingMyPartyMember,
					new PrioritySelector(
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => trash != null && trash != boss,
							ctx => Targeting.Instance.FirstUnit == null || !Me.IsActuallyInCombat && trash != null ,
							ctx => trash,
							ctx => trashTankSpot,
							ctx => trashTankSpot,
							10),
						new Decorator(
							ctx => StyxWoW.Me.Location.DistanceSqr(trashTankSpot) > 25 * 25 && ScriptHelpers.GroupMembers.All(p => p.IsAlive && p.Location.DistanceSqr(Me.Location) < 70 * 70) ,
							new Action(ctx => Navigator.MoveTo(trashTankSpot))))),
				ScriptHelpers.CreatePullNpcToLocation(
					ctx => boss != null, ctx => boss.IsTargetingMeOrPet || boss.IsTargetingMyPartyMember, ctx => boss, ctx => StyxWoW.Me.Location, ctx => trashTankSpot, 10));
		}


		[EncounterHandler(29120, "Anub'arak", Mode = CallBehaviorMode.Proximity)]
		public Composite AnubarakEncounter()
		{
			WoWUnit boss = null;
			const uint anubarakId = 29120;
			var startLoc = new WoWPoint(550.2374, 275, 223.8891);
			const uint impaleTargetId = 29184;

			AddAvoidObject(ctx => true, 4, impaleTargetId);
			AddAvoidObject(
				ctx => StyxWoW.Me.IsRange() && !Me.IsCasting,
				15,
				o => o.Entry == anubarakId && o.ToUnit().CurrentTargetGuid != Me.Guid && !o.ToUnit().IsMoving && o.ToUnit().IsAlive);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(ctx => StyxWoW.Me.Y > 277, new Action(ctx => Navigator.MoveTo(startLoc))),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => Me.IsFollower() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 8, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				new Decorator(ctx => Me.IsTank() && Me.CurrentTargetGuid == boss.Guid && boss.CurrentTargetGuid == Me.Guid, ScriptHelpers.CreateTankFaceAwayGroupUnit(15)));
		}
	}
}
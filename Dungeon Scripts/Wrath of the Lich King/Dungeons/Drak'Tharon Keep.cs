
using System.Collections.Generic;
using System.Linq;
using Styx;
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
	public class DrakTharonKeep : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 214; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(4774.611, -2023.276, 229.3549); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-518.0573, -480.0317, 10.97494); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(ret => { return false; });
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null) { }
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null) { }
			}
		}

		#endregion

		private const string ZombieDpsRotation =
@"local _,s = GetActionInfo(120 + 4) 
if s then  print(s) CastSpellByID(s) end
s = GetActionInfo(120 + 3) 
if s then CastSpellByID(s) end 
s = GetActionInfo(120 + 1) 
if s then CastSpellByID(s) end 
";

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(55677, "Kurzel", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			const int whatTheScourgeDredQuestId = 29828;

			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					new PrioritySelector(
						new Decorator(
							ctx => !Me.QuestLog.ContainsQuest(whatTheScourgeDredQuestId) && !Me.QuestLog.GetCompletedQuests().Contains(whatTheScourgeDredQuestId),
							ScriptHelpers.CreatePickupQuest(ctx => unit, whatTheScourgeDredQuestId)))),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(0, "Root Behavior")]
		public Composite RootBehavior()
		{
			var shadowVoidIds = new uint[] { 55847, 59014 };

			AddAvoidObject(ctx => true, 4, shadowVoidIds);
			return new PrioritySelector();
		}


		[EncounterHandler(26624, "Wretched Belcher")]
		public Composite WretchedBelcherEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => Me.IsFollower() && unit.CurrentTargetGuid != Me.Guid && !unit.IsMoving && unit.Distance < 15, ctx => unit, new ScriptHelpers.AngleSpan(0, 180)),
				new Decorator(ctx => StyxWoW.Me.CurrentTargetGuid == unit.Guid && unit.CurrentTargetGuid == Me.Guid, ScriptHelpers.CreateTankFaceAwayGroupUnit(15)));
		}


		[EncounterHandler(26630, "Trollgore", Mode = CallBehaviorMode.Proximity, BossRange = 110)]
		public Composite TrollgoreEncounter()
		{
			WoWUnit boss = null;
			List<WoWUnit> belchers = null;
			const uint TrollgoreId = 26630;
			const uint wretchedBelcherId = 26624;
			var corpseExposionIds = new[] { 49555, 59807 };

			var trashTankLoc = new WoWPoint(-355.6056, -624.7963, 11.02102);
			var followerWaitLoc = new WoWPoint(-347.375, -614.9806, 11.01204);
			var pullLoc = new WoWPoint(-338.9121, -630.8854, 11.38);
			var roomCenterLoc = new WoWPoint(-312.3778, -659.7048, 10.28416);

			AddAvoidObject(
				ctx => !Me.IsCasting,
				5,
				o => o.Entry == TrollgoreId && corpseExposionIds.Contains(o.ToUnit().CastingSpellId) && o.ToUnit().CurrentTargetGuid != 0,
				o => o.ToUnit().CurrentTarget.Location);

			return new PrioritySelector(
				ctx =>
				{
					belchers = ScriptHelpers.GetUnfriendlyNpsAtLocation(roomCenterLoc, 30, u => u.Entry == wretchedBelcherId);
					return boss = ctx as WoWUnit;
				},
				ScriptHelpers.CreatePullNpcToLocation(
					ctx => belchers.Any(),
					ctx => belchers[0].DistanceSqr <= 40 * 40 && (belchers.Count == 1 || belchers[0].Location.DistanceSqr(belchers[1].Location) > 25 * 25),
					ctx => belchers[0],
					ctx => trashTankLoc,
					ctx => StyxWoW.Me.IsTank() ? pullLoc : followerWaitLoc,
					10));
		}

		[EncounterHandler(26631, "Novos the Summoner")]
		public Composite NovosTheSummonerEncounter()
		{
			const uint arcaneFieldId = 47346;
			const uint blizardId = 49034;
			AddAvoidObject(ctx => true, 12, arcaneFieldId);
			AddAvoidObject(ctx => !Me.IsCasting, 8, blizardId);

			return new PrioritySelector();
		}

		[EncounterHandler(27483, "King Dred", Mode = CallBehaviorMode.Proximity, BossRange = 120)]
		public Composite KingDredEncounter()
		{
			WoWUnit boss = null;
			var tankLoc = new WoWPoint(-494.3439, -721.4702, 30.24773);
			var dredSafeLoc = new WoWPoint(-535.8426, -664.3137, 30.2464);
			var trashLoc = new WoWPoint(-525.6827, -714.9271, 30.24642);
			WoWUnit trash = null;
			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashLoc, 30, u => u != boss).FirstOrDefault();
					return boss;
				},
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => StyxWoW.Me.IsTank() && Targeting.Instance.FirstUnit == null && StyxWoW.Me.Location.DistanceSqr(tankLoc) > 25 * 25,
							new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(tankLoc))),

						ScriptHelpers.CreatePullNpcToLocation(
							ctx => trash != null && Me.Location.DistanceSqr(tankLoc) < 40 * 40 && !Me.Combat, 
							ctx => boss.Location.DistanceSqr(dredSafeLoc) <= 20 * 20 ,
							ctx => trash, 
							ctx => tankLoc, 
							ctx => tankLoc, 
							10),

						ScriptHelpers.CreatePullNpcToLocation(
							ctx => trash == null && Me.Location.DistanceSqr(tankLoc) < 40 * 40 && !Me.Combat, 
							ctx => boss.Location.DistanceSqr(trashLoc) <= 25 * 25 , 
							ctx => boss, 
							ctx => tankLoc, 
							ctx => tankLoc, 
							10))));
		}

		[EncounterHandler(26632, "The Prophet Tharon'ja")]
		public Composite TheProphetTharonjaEncounter()
		{
			var badstuffIds = new uint[] { 49548, 59969, 49518, 59971 };
			AddAvoidObject(ctx => true, 7, badstuffIds);

			return new PrioritySelector(new Decorator(ctx => StyxWoW.Me.HasAura("Gift of Tharon'ja"), new Action(ctx => Lua.DoString(ZombieDpsRotation))));
		}
	}
}
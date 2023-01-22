using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class MaraudonEarthSongFalls : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 273; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-1381.243, 2918.000, 73.42503); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(1008.538, -461.3261, -43.5484); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var t in units)
			{
				var prioObject = t.Object;

				//Shardlings
				if (prioObject.Entry == 11783)
				{
					t.Score += 400;
				}
			}
		}

		#endregion

		private const int PrincessTheradrasQuestId = 27692;
		private const uint LandslideId = 12203;
		private const uint PrincessTheradrasId = 12201;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootHandler()
		{
			PlayerQuest quest = null;
			return
				new PrioritySelector(
					new Decorator(
						ctx => (quest = Me.QuestLog.GetQuestById(PrincessTheradrasQuestId)) != null && quest.IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(PrincessTheradrasQuestId)));
		}

		[EncounterHandler(12203, "Landslide")]
		public Composite LandslideFight()
		{
			const uint landslideId = 12203;
			var tankLoc = new WoWPoint(360.0094, -180.9783, -59.89912);
			WoWUnit boss = null;
			// ranged should stay away from boss to not get hit by the 'Trample' and AOE stun ability.
			AddAvoidObject(
				ctx => Me.IsRange() && !Me.IsCasting && ScriptHelpers.Tank != null && ScriptHelpers.Tank.IsAlive,
				10,
				o => o.Entry == landslideId && o.ToUnit().Combat && o.ToUnit().CurrentTargetGuid != Me.Guid);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit, new Decorator(ctx => Me.IsTank() && boss.CurrentTargetGuid == Me.Guid, ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 10)));
		}

		[EncounterHandler(12201, "Princess Theradras")]
		public Composite PrincessTheradrasFight()
		{
			var tankLoc = new WoWPoint(15.12897, 63.57555, -126.2918);
			WoWUnit boss = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit, new Decorator(ctx => Me.IsTank() && boss.CurrentTargetGuid == Me.Guid, ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 10)));
		}
	}
}
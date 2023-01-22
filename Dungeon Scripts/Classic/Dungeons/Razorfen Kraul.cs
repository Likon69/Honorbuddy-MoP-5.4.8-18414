using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class RazorfenKraul : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 16; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-4459.123, -1659.543, 81.59359); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(1939.186, 1538.528, 82.28346); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null) { }
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
					if (unit.Entry == HealingWardVId || unit.Entry == EarthGrabId)
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
					if (unit.Entry == HealingWardVId || unit.Entry == EarthGrabId)
						priority.Score += 500;
				}
			}
		}

		#endregion

		#region Root
		const uint HealingWardVId = 2992;
		private const uint EarthGrabId = 6066;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		public Composite RootBehavior()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(44402, "Auld Stonespire", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(44415, "Spirit of Agamaggan", Mode = CallBehaviorMode.Proximity)]
		public Composite AuldStonespireEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		#endregion

		[EncounterHandler(4428, "Death Speaker Jargba")]
		public Composite DeathSpeakerJargbaBehavior()
		{
			const int dominateMindId = 7645;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, dominateMindId));
		}


		[EncounterHandler(4424, "Aggem Thorncurse")]
		public Composite AggemThorncurseBehavior()
		{
			const int chainHealId = 14900;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, chainHealId));
		}

		[EncounterHandler(4422, "Agathelos the Raging")]
		public Composite AgathelosTheRagingBehavior()
		{
			WoWUnit boss = null;
			const uint agathelosTheRagingId = 4422;

			AddAvoidObject(ctx => Me.IsRange(), 12, o => o.Entry == agathelosTheRagingId && o.ToUnit().IsAlive && o.ToUnit().CurrentTargetGuid != Me.Guid);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(4421, "Charlga Razorflank")]
		public Composite CharlgaRazorflankBehavior()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelEnemy("Renew", ScriptHelpers.EnemyDispelType.Magic, ctx => boss));
		}
	}
}
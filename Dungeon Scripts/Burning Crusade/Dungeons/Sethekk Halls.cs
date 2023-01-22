using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Tripper.MeshMisc;
using Tripper.Navigation;
using Tripper.RecastManaged.Detour;
using Tripper.Tools.Math;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class SethekkHalls : Dungeon
	{
		#region Overrides of Dungeon

		private readonly Vector3 _shortcutBlackspotLoc = new Vector3(44.46614f, 185.8761f, -10);
		private bool _shortcutBlocked;

		public override uint DungeonId
		{
			get { return 150; }
		}

		public override WoWPoint Entrance
		{
			get
			{
				if (_shortcutBlocked) _shortcutBlocked = false;
				return new WoWPoint(-3361.518, 4655.588, -101.0466);
			}
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-9.0, -0.7, .001); }
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits)
			{
				if (unit.Entry == CharmingTotemId) //  Charming Totem
					outgoingunits.Add(unit);
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == CharmingTotemId) //  Charming Totem
						priority.Score += 400;

					if (unit.Entry == TimeLostControllersId && StyxWoW.Me.IsDps())
						priority.Score += 210;

					if (unit.Entry == SethekkProphetId && StyxWoW.Me.IsDps())
						priority.Score += 200;

					if (unit.Entry == TimeLostScryerId && StyxWoW.Me.IsDps())
						priority.Score += 190;
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(unit => unit is WoWPlayer);
		}

		public override void OnEnter()
		{
			_endBossShortcutBlackspot = new DynamicBlackspot(
				() => !IsShortcutGateOpen, 
				() => _shortcutBlackspotLoc, 
				LfgDungeon.MapId, 
				50, 
				50,
				"Shortcut to last boss");

			DynamicBlackspotManager.AddBlackspot(_endBossShortcutBlackspot);
		}

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspot(_endBossShortcutBlackspot);
			_endBossShortcutBlackspot = null;
		}


		#endregion

		#region Root

		private const uint CharmingTotemId = 20343;
		private const uint TimeLostControllersId = 18327;
		private const uint SethekkProphetId = 18325;
		private const uint TimeLostScryerId = 18319;
		private const uint SethekkSpiritId = 18703;
		private const uint BrotherAgainstBrotherQuestId = 29605;

		private WoWUnit _ikiss;
		private WoWUnit _syth;

		static private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(54840, "Isfar", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(54847, "Dealer Vijaad", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public Composite WatcherJhangQuestHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location),
					new PrioritySelector(
						new Decorator(
							ctx => unit.HasQuestAvailable(true),
							ScriptHelpers.CreatePickupQuest(ctx => unit)),
						new Decorator(
							ctx => unit.HasQuestTurnin(),
							ScriptHelpers.CreateTurninQuest(ctx => unit))))
				);
		}

		[EncounterHandler(0, "Root")]
		public Composite RootHandler()
		{
			AddAvoidObject(ctx => !Me.IsCasting, obj => Me.IsRange() && Me.IsMoving ? 8 : 6, SethekkSpiritId);

			return new PrioritySelector();
		}

		[EncounterHandler(18956, "Lakka", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public Composite LakkaEncounter()
		{
			return new Decorator<WoWUnit>(unit =>
							ScriptHelpers.HasQuest(BrotherAgainstBrotherQuestId) 
							&& !ScriptHelpers.IsQuestInLogComplete(BrotherAgainstBrotherQuestId) 
							&& !Me.Combat 
							&& !ScriptHelpers.WillPullAggroAtLocation(unit.Location),
						// talk to Lakka
						ScriptHelpers.CreateTalkToNpc(ctx => ctx as WoWUnit));
		} 

		private DynamicBlackspot _endBossShortcutBlackspot;
		private const uint ShortcutGateId = 183398;

		private bool IsShortcutGateOpen
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWGameObject>().Any(g => g.Entry == ShortcutGateId && ((WoWDoor) g.SubObj).IsOpen);
			}
		}

		#endregion

		#region Darkweaver Syth

		[EncounterHandler(18472, "Darkweaver Syth")]
		public Composite DarkweaverSythEncounter()
		{
			return new PrioritySelector(
				ctx => _syth = ctx as WoWUnit,
				ScriptHelpers.CreateSpreadOutLogic(ctx => true, ctx => _syth.Location, 15, 30));
		}

		#endregion


		#region Talon King Ikiss

		[EncounterHandler(18473, "Talon King Ikiss")]
		public Composite TalonKingIkissEncounter()
		{
			var pilarLocations = new List<WoWPoint>
								{
									new WoWPoint(22.67584, 309.4335, 26.59805),
									new WoWPoint(67.06754, 308.9184, 26.62627),
									new WoWPoint(67.86166, 262.8065, 26.36894),
									new WoWPoint(22.76415, 264.541, 26.66963)
								};

			return new PrioritySelector(
				ctx => _ikiss = ctx as WoWUnit,
				ScriptHelpers.CreateLosLocation(
					ctx => _ikiss.CastingSpellId == 38197 || _ikiss.CastingSpellId == 40425,
					ctx => _ikiss.Location,
					ctx => pilarLocations.OrderBy(loc => StyxWoW.Me.Location.DistanceSqr(loc)).FirstOrDefault(),
					ctx => 10));
		}

		#endregion

	}
}
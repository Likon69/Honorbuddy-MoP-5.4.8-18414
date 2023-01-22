
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class UtgardePinnacle : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 203; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1234.077, -4860.071, 218.295); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(590.6429, -327.9592, 110.1452); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit == null)
						return false;

					if (unit.Entry == MindlessServantId && !unit.Combat)
						return true;
					if (unit.Entry == DragonflayerSpectatorId)
						return true;
					if (ret.Entry == GraufId)
						return true;
					if (unit.Entry == SkadiId && (unit.HasAura("Ride Vehicle") || unit.HasAura("Whirlwind") && Me.IsDps() && Me.IsMelee()))
						return true;
					return false;
				});
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
				if (unit != null)
				{
					if (unit.Entry == RitualChannelerId)
						priority.Score += 500;
					if (unit.Entry == DragonflayerSeerId && StyxWoW.Me.IsDps())
						priority.Score += 500;
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			if (!ScriptHelpers.HasQuest(JunkInMyTrunkQuestId) || ScriptHelpers.IsQuestInLogComplete(JunkInMyTrunkQuestId))
				return;

			foreach (var obj in incomingunits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null)
				{
					// objects for the quest 'Junk in my Trunk'
					if (_questItemIds.Contains(gObj.Entry)
						&& !ScriptHelpers.WillPullAggroAtLocation(gObj.Location)
						&& gObj.DistanceSqr < 30 * 30
						&& !gObj.InUse && gObj.CanUse())
					{
						var pathDist = Me.Location.PathDistance(gObj.Location, 30f);
						if (!pathDist.HasValue || pathDist.Value >= 30f)
							continue;
						outgoingunits.Add(gObj);
					}
				}				
			}
		}

		public override void OnEnter()
		{
			_shortcutDoorBlackspot =
				new DynamicBlackspot(
					() =>
						_applyShortcutDoorBlackspot ??
						(_applyShortcutDoorBlackspot = new TimeCachedValue<bool>(TimeSpan.FromSeconds(4), ShouldApplyBlackspotAtDoorByShortcut)),
					() => _shortcutDoorLoc,
					LfgDungeon.MapId,
					15,
					10,
					"Door by shortcut");
			
			DynamicBlackspotManager.AddBlackspot(_shortcutDoorBlackspot);
		}

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspot(_shortcutDoorBlackspot);
			_shortcutDoorBlackspot = null;
		}

		#endregion

		private const uint RitualChannelerId = 27281;
		private const uint DragonflayerSeerId = 26554;
		private const uint DragonflayerSpectatorId = 26667;
		private const uint GraufId = 26893;
		private const uint SkadiId = 26693;
		private const uint MindlessServantId = 26536;

		readonly WoWPoint _shortcutDoorLoc = new WoWPoint(445.6213, -324.9473, 104.0242);
		private DynamicBlackspot _shortcutDoorBlackspot;
		const uint ShortcutDoorId = 192174;
		private TimeCachedValue<bool> _applyShortcutDoorBlackspot;

		const uint JadeStatueId = 192945;
		const uint UntarnishedSilverBarId = 192941;
		const uint ShinyBaubleId = 192943;
		const uint GoldenGobletId = 192944;
		private const uint JunkInMyTrunkQuestId = 13131;
		private const uint VengeanceBeMineQuestId = 13132;
		readonly WoWPoint _lastBossLocation = new WoWPoint(392.8011, -289.0229, 109.2514);

		private readonly uint[] _questItemIds = { JadeStatueId, UntarnishedSilverBarId, ShinyBaubleId, GoldenGobletId };

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		static bool ShouldApplyBlackspotAtDoorByShortcut()
		{
			var door = ObjectManager.GetObjectsOfType<WoWGameObject>()
				.FirstOrDefault(g => g.Entry == ShortcutDoorId);
			return door == null || ((WoWDoor) door.SubObj).IsClosed;
		}

		private readonly uint[] _questIdsAtEntrance =
		{
			JunkInMyTrunkQuestId,
			VengeanceBeMineQuestId
		};

		[EncounterHandler(30871, "Brigg Smallshanks", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(56072, "Image of Argent Confessor Paletress", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location) )
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}


		[EncounterHandler(0, "Root behavior")]
		public async Task<bool> RootHandler(WoWUnit npc)
		{
			// port outside and back in to hand in quests once dungeon is complete.
			// QuestPickupTurninHandler will handle the turnin
			return IsComplete && !Me.Combat
					&& _questIdsAtEntrance.Any(ScriptHelpers.IsQuestInLogComplete)
					&& BotPoi.Current.Type == PoiType.None
					&& Me.Location.DistanceSqr(_lastBossLocation) < 50 * 50
					&& await ScriptHelpers.PortOutsideAndBackIn();
		}

		[EncounterHandler(26861, "King Ymiron")]
		public Composite KingYmironEncounter()
		{
			const uint spiritFountId = 27339;
			AddAvoidObject(ctx => true, o => Me.IsMoving && Me.IsRange() ? 15 : 10, spiritFountId);

			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateDispelEnemy("Bane", ScriptHelpers.EnemyDispelType.Magic, ctx => boss));
		}

		#region Svala Sorrowgrave

		[EncounterHandler(26668, "Svala Sorrowgrave", Mode = CallBehaviorMode.Proximity)]
		public Composite SvalaSorrowgraveEncounter()
		{
			WoWUnit sorrowgrave = null;
			const uint ritualTargetId = 27327;
			const int ritualOfTheSwordId = 48276;

			AddAvoidObject(
				ctx =>
					!Me.IsTank() && sorrowgrave != null && sorrowgrave.IsValid &&
					(sorrowgrave.CastingSpellId == ritualOfTheSwordId || sorrowgrave.ChanneledCastingSpellId == ritualOfTheSwordId),
				10,
				o => o.Entry == ritualTargetId && !ScriptHelpers.GetUnfriendlyNpsAtLocation(o.Location, 30, u => u.Entry == RitualChannelerId).Any());

			return new PrioritySelector(
				ctx => sorrowgrave = ctx as WoWUnit,
				// wait for the bitch to spawn. 
				new Decorator<WoWUnit>(
					svala => svala != null && StyxWoW.Me.IsTank() && !StyxWoW.Me.IsActuallyInCombat && !svala.Attackable && svala.DistanceSqr < 35 * 35,
					new PrioritySelector(
						new Decorator<WoWUnit>(svala => svala.DistanceSqr > 10 * 10, new Helpers.Action<WoWUnit>(svala => Navigator.MoveTo(svala.Location))),
						new ActionAlwaysSucceed())));
		}

		[EncounterHandler(29281, "Svala", Mode = CallBehaviorMode.Proximity)]
		public Composite SvalaEncounter()
		{
			return new PrioritySelector(
				// wait for the bitch to spawn. 
				new Decorator<WoWUnit>(
					svala => svala != null && StyxWoW.Me.IsTank() && !StyxWoW.Me.IsActuallyInCombat && svala.DistanceSqr < 35 * 35,
					new PrioritySelector(
						new Decorator<WoWUnit>(svala => svala.DistanceSqr > 10 * 10, new Helpers.Action<WoWUnit>(svala => Navigator.MoveTo(svala.Location))),
						new ActionAlwaysSucceed())));
		}

		#endregion

		#region Gortok Palehoof

		private const uint StasisGeneratorId = 188593;
		private const uint GortokPalehoofId = 26687;


		[EncounterHandler(26685, "Massive Jormungar")]
		public Composite MassiveJormungarEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// frontal spray. 
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 15,
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(15));
		}

		[EncounterHandler(26687, "Gortok Palehoof")]
		public Composite GortokPalehoofEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// cleave
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 8,
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(8));
		}

		[LocationHandler(274.8584, -452.0601, 104.7027, 40, "Gortok Palehoof")]
		public Composite CreateBehavior_StartGortokPalehoofEncounter()
		{
			return new PrioritySelector(
				ctx => ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(o => o.Entry == GortokPalehoofId),
				new Decorator(
					ctx => ctx != null,
					new PrioritySelector(
				// interact with the Stasis Generator and wait for mini-bosses to spawn.
						new Decorator<WoWUnit>(
							boss => boss.HasAura("Freeze Anim") && StyxWoW.Me.IsTank() && Targeting.Instance.IsEmpty(),
							new PrioritySelector(
								ctx => ObjectManager.GetObjectsOfTypeFast<WoWGameObject>().FirstOrDefault(g => g.Entry == StasisGeneratorId),
								new Decorator<WoWGameObject>(gen => gen.CanUse(), ScriptHelpers.CreateInteractWithObject(ctx => (WoWGameObject)ctx, 12)),
								new ActionAlwaysSucceed())))));
		}

		#endregion

		#region Skadi the Ruthless

		private readonly WoWPoint _skadiTankSpot = new WoWPoint(486.1297, -515.4338, 104.723);

		[EncounterHandler(26893, "Grauf", Mode = CallBehaviorMode.Proximity, BossRange = 1000)]
		public Composite CreateBehavior_GraufEncounter()
		{
			WoWDynamicObject freezingCloud = null;
			WoWGameObject harpoon = null, harpoonLancher = null;
			var throwHarpoonsLoc = new WoWPoint(520.4827, -541.5633, 119.8416);

			return new PrioritySelector(
				new Decorator<WoWUnit>(
					grauf => grauf.HasAura("Ride Vehicle"),
					new PrioritySelector(
						ctx =>
						{
							freezingCloud = ObjectManager.GetObjectsOfType<WoWDynamicObject>().FirstOrDefault(o => o.Entry == 47579);
							harpoon = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Entry == 192539).OrderBy(o => o.DistanceSqr).FirstOrDefault();
							harpoonLancher =
								ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Entry == 192176 && o.CanUse()).OrderBy(o => o.DistanceSqr).FirstOrDefault();
							return ctx;
						},
				// freezing cloud is on west/left side.
						new Decorator(
							ctx => freezingCloud != null && freezingCloud.Y >= -512 && StyxWoW.Me.Y > -516 && StyxWoW.Me.Y < -492,
							new Action(
								ctx =>
								{
									var moveToLoc = StyxWoW.Me.Location;
									moveToLoc.Y = -516.5f;
									WoWMovement.ClickToMove(moveToLoc);
								})),
				// freezing cloud is on east/right side
						new Decorator(
							ctx => freezingCloud != null && freezingCloud.Y < -512 && StyxWoW.Me.Y < -510.5,
							new Action(
								ctx =>
								{
									var moveToLoc = StyxWoW.Me.Location;
									moveToLoc.Y = -510f;
									WoWMovement.ClickToMove(moveToLoc);
								})),
				// only have the dps pick up the harpoons and only when there is no freezing cloud on ground.
						new Decorator(
							ctx => harpoon != null && freezingCloud == null && (StyxWoW.Me.IsDps() || !Me.GroupInfo.IsInParty),
							ScriptHelpers.CreateInteractWithObject(ctx => harpoon, 0, true)),
				// check if shadi is in front of the harpoons and if we have any harpoons..
						new Decorator<WoWUnit>(
							grauf => grauf.Location.DistanceSqr(throwHarpoonsLoc) <= 20 * 20 && harpoonLancher != null && StyxWoW.Me.BagItems.Any(i => i.Entry == 37372),
							ScriptHelpers.CreateInteractWithObject(ctx => harpoonLancher, 0, true)),
				// move towards the end of the gauntlet
						new Decorator(
							ctx => StyxWoW.Me.IsTank() && StyxWoW.Me.Location.DistanceSqr(_skadiTankSpot) > 15 * 15,
							new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_skadiTankSpot))))));
		}

		[EncounterHandler(26693, "Skadi the Ruthless")]
		public Composite SkadiTheRuthlessEncounter()
		{
			AddAvoidObject(
				ctx => !Me.IsTank(),
				o => Me.IsRange() && Me.IsMoving ? 15 : 10,
				o => o.Entry == SkadiId && o.ToUnit().Combat && (o.ToUnit().HasAura("Whirlwind") || Me.IsRange()));

			return new Decorator<WoWUnit>(
				skadi => skadi.HasAura("Ride Vehicle"),
				new PrioritySelector(
					new Decorator<WoWUnit>(
						skadi => StyxWoW.Me.IsTank() && skadi.CurrentTargetGuid == Me.Guid,
						ScriptHelpers.CreateTankUnitAtLocation(ctx => _skadiTankSpot, 5))));
		}

		#endregion
	}
}
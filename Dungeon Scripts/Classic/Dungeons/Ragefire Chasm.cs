using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Bots.DungeonBuddy.Profiles.Handlers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class RagefireChasm : Dungeon
	{
		#region Overrides of Dungeon

		private bool appliedSpellBlacklist;

		public override uint DungeonId
		{
			get { return 4; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1820.646, -4427.459, -20.67462); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(2.109175, -3.876605, -14.15257); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (Targeting.TargetPriority t in units)
			{
				WoWObject prioObject = t.Object;
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;
					if (unit != null)
					{
						if (unit.Entry == SlagmawId && unit.HasAura("Magnaw Submerge") && !Me.IsHealer)
							return true;
					}
					return false;
				});
		}

		public override void OnEnter()
		{
			appliedSpellBlacklist = false;
			BossManager.OnBossKill += BossManager_OnBossKill;
		}

		public override void OnExit()
		{
			BossManager.OnBossKill -= BossManager_OnBossKill;
			if (appliedSpellBlacklist)
			{
				SpellBlacklist.Remove(_blacklistedSpells);
				appliedSpellBlacklist = false;
			}
		}

		private void BossManager_OnBossKill(Boss boss)
		{
			if (boss.Entry == SlagmawId)
			{
				SpellBlacklist.Remove(_blacklistedSpells);
				appliedSpellBlacklist = false;
			}
		}

		#endregion

		private const uint SlagmawId = 61463;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Root

		private const uint LavaId1 = 61560;
		private const uint LavaId2 = 61601;
		private const int NoOrcLeftBehindQuestId = 30984;

		private readonly int[] _blacklistedSpells =
			{
				109132, // monk's roll.
			};

		private readonly WoWPoint _lavaLocation1 = new WoWPoint(-100.9294, -33.67703, -29.74805);
		private readonly WoWPoint _lavaLocation2 = new WoWPoint(-260.9831, 7.593772, -49.85098);

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			//AddAvoidObject(ctx => true, 10, o => o.Entry == LavaId1 && o.Z > -33.6, o => _lavaLocation1);
			// second lava location does so little damage I'm not sure its worth avoiding.
			// AddAvoidObject(ctx => true, 17, o => o.Entry == LavaId2 && o.Z > -49, o => _lavaLocation2);
			return new PrioritySelector();
		}

		[EncounterHandler(61716, "Invoker Xorenth", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(61724, "Commander Bagran", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(61823, "High Sorceress Aryna", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(61822, "SI:7 Field Commander Dirken", Mode = CallBehaviorMode.Proximity, BossRange = 40)]

		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(61780, "Suspicious Rock", Mode = CallBehaviorMode.Proximity, BossRange = 15)]
		public Composite SuspiciousRockEncounter()
		{
			PlayerQuest quest = null;
			WoWUnit rock = null;
			return new PrioritySelector(
				ctx => rock = ctx as WoWUnit,
				new Decorator(
					ctx => (quest = Me.QuestLog.GetQuestById(NoOrcLeftBehindQuestId)) != null && !quest.IsCompleted && !Me.Combat, ScriptHelpers.CreateTalkToNpc(ctx => rock)));
		}

		/* Navigation issues getting to these. these can be skipped and still be able to complete quest but might take 2 runs. 
		[EncounterHandler(61680, "Kor'kron Scout", Mode = CallBehaviorMode.Proximity, BossRange = 20)]
		public Composite KorkronScoutEncounter()
		{
			PlayerQuest quest = null;
			WoWUnit scout = null;
			var hangingLocs = new[] { new WoWPoint(-288.154, 137.8819, -25.4445), new WoWPoint(-173.5599, 6.632007, -30.54592) };

			return new PrioritySelector(
				ctx => scout = ctx as WoWUnit,
				new Decorator(
					ctx =>
					(quest = Me.QuestLog.GetQuestById(NoOrcLeftBehindQuestId)) != null && !quest.IsCompleted && !Me.Combat &&
					hangingLocs.Any(l => scout.Location.Distance(l) < 10),
					new PrioritySelector(
						new Decorator(ctx => scout.WithinInteractRange, new Action(ctx => scout.Interact())),
						new Decorator(
							ctx => !scout.WithinInteractRange,
							new Action(ctx => Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, scout.Location, Navigator.PathPrecision + 2)))))));
		}
		*/
		#endregion

		[EncounterHandler(61408, "Adarogg")]
		public Composite AdaroggEncounter()
		{
			const uint demonicLeapId = 61409;
			AddAvoidObject(ctx => true, 6, u => u.Entry == demonicLeapId && u.ToUnit().HasAura("Inferno Charge"));
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// avoid the flame breath
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 90)));
		}

		[EncounterHandler(61412, "Dark Shaman Koranthal")]
		public Composite KoranthalEncounter()
		{
			WoWUnit boss = null;
			const int twistedElementsId = 119300;
			var shadowStormMissileIds = new[] { 119965, 119984 };
			// avoid the shadow storm
			AddAvoidLocation(ctx => true, 2, o => ((WoWMissile)o).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => shadowStormMissileIds.Contains(m.SpellId)));

			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, twistedElementsId));
		}

		[EncounterHandler(61463, "Slagmaw", Mode = CallBehaviorMode.Proximity)]
		public Composite SlagmawEncounter()
		{
			WoWUnit boss = null;
			//var spitTimer = new WaitTimer(TimeSpan.FromSeconds(4));
			//const int lavaSpitId = 119434;
			var roomCenterLoc = new WoWPoint(-244.7897, 155.2252, -18.72345);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !appliedSpellBlacklist,
					new Action(
						ctx =>
						{
							SpellBlacklist.Apply(_blacklistedSpells);
							appliedSpellBlacklist = true;
						})),
				// disable movement to prevent Combat routine to attempt to get behind boss and pull nearby groups
				new Decorator(
					ctx => boss.IsWithinMeleeRange && Me.IsDps() && Me.IsMelee() && Me.CurrentTargetGuid == boss.Guid && ScriptHelpers.MovementEnabled,
					new Action(ctx => ScriptHelpers.DisableMovement(() => boss.IsWithinMeleeRange && Me.CurrentTargetGuid == boss.Guid))),
				ScriptHelpers.CreateSpreadOutLogic(ctx => true, ctx => roomCenterLoc, 3, 15),
				new Decorator(ctx => Targeting.Instance.IsEmpty() && Me.IsTank() && boss.Combat, new ActionAlwaysSucceed()));
		}

		[EncounterHandler(61528, "Lava Guard Gordoth")]
		public Composite GordothEncounter()
		{
			return new PrioritySelector();
		}

		#region Nested type: SpellBlacklist

		private static class SpellBlacklist
		{
			// by highvoltz - hackish method to stop combat routines from casting certain spells.
			private const string BlacklistSpellLuaFormat = @"
if spellBlacklist == nil then spellBlacklist={{}} end
local ids = ""{0}""
for id in string.gmatch(ids, ""%d+"") do
	local name = GetSpellInfo(tonumber(id))
	if name ~= nil then spellBlacklist[name]=true end
end
if  originalIsUsableSpell == nil then
	originalIsUsableSpell = IsUsableSpell
	IsUsableSpell = function (spell, bookType)
		if type(spell)=='string' and spellBlacklist[spell] then
			return nil, nil
		end
		return originalIsUsableSpell(spell, bookType)
	end
end";

			private const string RemoveSpellBlacklistLuaFormat = @"
if spellBlacklist == nil then return end
local ids = ""{0}""
for id in string.gmatch(ids, ""%d+"") do
	local name = GetSpellInfo(tonumber(id))
	if name ~= nil and spellBlacklist[name] then spellBlacklist[name]=nil end
end
local cnt = 0 
for _, _ in pairs(spellBlacklist) do cnt = cnt + 1 end
if cnt == 0 then
	IsUsableSpell = originalIsUsableSpell
	originalIsUsableSpell = nil
	spellBlacklist = nil
end
";

			public static void Apply(params int[] spellIds)
			{
				var idsString = spellIds.Select(i => i.ToString(CultureInfo.InvariantCulture)).Aggregate((a, b) => a + "," + b);
				Lua.DoString(string.Format(BlacklistSpellLuaFormat, idsString));
			}

			public static void Remove(params int[] spellIds)
			{
				var idsString = spellIds.Select(i => i.ToString(CultureInfo.InvariantCulture)).Aggregate((a, b) => a + "," + b);
				Lua.DoString(string.Format(RemoveSpellBlacklistLuaFormat, idsString));
			}

			public static bool IsBlacklisted(int spellId)
			{
				return Lua.GetReturnVal<bool>(string.Format("local name = GetSpellInfo({0}) return name and spellBlacklist and spellBlacklist[name]", spellId), 0);
			}
		}

		#endregion
	}
}
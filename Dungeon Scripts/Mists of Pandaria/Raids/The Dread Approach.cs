using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using Bots.DungeonBuddy.Avoidance;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class TheDreadApproach : Dungeon
	{
		#region Overrides of Dungeon

		private bool appliedSpellBlacklist;

		public override uint DungeonId
		{
			get { return 529; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(165.4734, 4069.003, 255.8758); }
		}

		public override void OnExit()
		{
			if (appliedSpellBlacklist)
			{
				SpellBlacklist.Remove(_blacklistedSpells);
				appliedSpellBlacklist = false;
			}
		}

		private void ApplySpellBacklist()
		{
			SpellBlacklist.Apply(_blacklistedSpells);
			appliedSpellBlacklist = true;
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == GaralonsLegId)
						{
							if (Me.IsRange())
								return true;
							if (!unit.InLineOfSpellSight)
								return true;
							if (Me.IsMelee())
							{
								if (!Navigator.CanNavigateFully(Me.Location, unit.Location))
									return true;
								// have melee ignore the legs that are by the wall bcause of potential issues getting to them e.g. having to run in front of boss or stand in the puddles.
								var transport = unit.Transport;
								if (transport != null)
								{
									var transportLoc = transport.Location;
									var travelingClockwise =
										!_garalonRoomCenter.IsPointLeftOfLine(transportLoc, transportLoc.RayCast(WoWMathHelper.NormalizeRadian(transport.Rotation), 10));
									var relativeLoc = unit.RelativeLocation;
									return relativeLoc.Y < 0 && !travelingClockwise || relativeLoc.Y > 0 && travelingClockwise;
								}
							}
						}
					}
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
					if (unit is WoWPlayer)
						priority.Score += 1000; // dps MCed players.
					else if (unit.Entry == GaralonsLegId && Me.IsMelee())
					{
						priority.Score = 5000 - unit.Distance;
					}
				}
			}
		}

		public override void OnEnter()
		{
			if (Me.IsTank())
			{
				Alert.Show(
					"Tanking Not Supported",
					string.Format(
						"Tanking is not supported in the {0} script. If you wish to stay in raid and play manually then press 'Continue'. Otherwise you will automatically leave raid.",
						Name),
					30,
					true,
					true,
					null,
					() => Lua.DoString("LeaveParty()"),
					"Continue",
					"Leave");
			}
			else
			{
				Alert.Show(
					"Do Not AFK",
					"It is highly recommended you do not afk while in a raid and be prepared to intervene if needed in the event something goes wrong or you're asked to perform a certain task.",
					20,
					true,
					false,
					null,
					null,
					"Ok");
			}
		}

		#endregion

		private const uint ZephyrId = 63599;
		private const uint TrashVersionTempestStalkerId = 64373;

		private readonly int[] _blacklistedSpells =
		{
			6544, // Heroic Leap
			109132, // monk's roll.
			36554 // Shadowstep 
		};

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidObject(
				ctx => !Me.IsCasting,
				7,
				u => u.Entry == ZephyrId,
				o =>
				{
					var loc = o.Location;
					return Me.Location.GetNearestPointOnSegment(loc, loc.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 15));
				});
			return new PrioritySelector();
		}

		#region Imperial Vizier Zor'lok

		// get in noice canceling shield.
		// ingore sonic ring, too many
		// bunch up for aoe heals.
		// don't get traped on a platform when boss moves to center.

		private const int NoiseCancellingAreaTriggerSpellId = 122731;

		[EncounterHandler(62980, "Imperial Vizier Zor'lok")]
		public Composite ImperialVizierZorlokEncounter()
		{
			WoWUnit boss = null;
			WoWAreaTrigger noiceCancellingArea = null;
			WoWPoint raidLoc = WoWPoint.Zero;

			return new PrioritySelector(
				ctx =>
				{
					noiceCancellingArea =
						ObjectManager.GetObjectsOfType<WoWAreaTrigger>()
									 .Where(a => a.SpellId == NoiseCancellingAreaTriggerSpellId)
									 .OrderBy(a => a.DistanceSqr)
									 .FirstOrDefault();
					raidLoc = ScriptHelpers.GetGroupCenterLocation();
					return boss = ctx as WoWUnit;
				},
				// get the Noice Cancelling buff.
				new Decorator(ctx => noiceCancellingArea != null && noiceCancellingArea.Distance > 3.5, new Action(ctx => Navigator.MoveTo(noiceCancellingArea.Location))),
				// stack up on raid location.
				new Decorator(
					ctx =>
					raidLoc.Distance(Me.Location) > 10 && (Me.HasAura("Pheromones of Zeal") || !Me.HasAura("Noise Cancelling") && Me.IsRange() && noiceCancellingArea == null),
					new Action(ctx => Navigator.MoveTo(raidLoc))));
		}

		#endregion

		#region Blade Lord Ta'yak

		private const uint TempestStalkerId = 62908;
		private const uint UnleashedWest1TornadoId = 63278;
		private const uint UnleashedWest2TornadoId = 63299;
		private const uint UnleashedWest3TornadoId = 63300;

		private const uint UnleashedEast1TornadoId = 63301;
		private const uint UnleashedEast2TornadoId = 63302;
		private const uint UnleashedEast3TornadoId = 63303;

		private const uint StormUnleashedWestLocStalkerId = 63423;
		private const uint StormUnleashedEastLocStalkerId = 63424;

		private const uint StormUnleashedEast1StalkerId = 63212;
		private const uint StormUnleashedEast2StalkerId = 63213;
		private const uint StormUnleashedEast3StalkerId = 63214;

		private const uint StormUnleashedWest1StalkerId = 63207;
		private const uint StormUnleashedWest2StalkerId = 63208;
		private const uint StormUnleashedWest3StalkerId = 63209;

		private const uint GaleWindId = 63292;

		private readonly uint[] _unleashedTornadoIds =
		{
			UnleashedWest1TornadoId, UnleashedWest2TornadoId, UnleashedWest3TornadoId, UnleashedEast1TornadoId,
			UnleashedEast2TornadoId,
			UnleashedEast3TornadoId
		};

		private readonly uint[] _unleashedTornadoStartIds =
		{
			StormUnleashedEast1StalkerId, StormUnleashedEast2StalkerId, StormUnleashedEast3StalkerId,
			StormUnleashedWest1StalkerId, StormUnleashedWest2StalkerId, StormUnleashedWest3StalkerId
		};

		private WoWUnit _tayak;

		// dodge the whirlwinds id 62908 (stationary) and 63299 travels in straight line
		// group up on Unseen strike target.(red mark)
		[EncounterHandler(62543, "Blade Lord Ta'yak")]
		public Func<WoWUnit, Task<bool>> BladeLordTayakEncounter()
		{
			AddAvoidObject(ctx => true, 5, TempestStalkerId);
			AddAvoidObject(ctx => true, 4, GaleWindId);
			//AddAvoidObject(ctx => BladeLordTayakPhaseTwo, 4, _unleashedTornadoStartIds);

			AddAvoidObject(
				ctx => true,
				4,
				o => _unleashedTornadoIds.Contains(o.Entry) && o.Distance < 15,
				o =>
				{
					var loc = o.Location;
					return Me.Location.GetNearestPointOnSegment(loc, loc.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 30));
				});
			// Avoid placed to the north side of path to prevent bot from going off path
			AddAvoidLocation(
				ctx => BladeLordTayakPhaseTwo,
				7,
				o =>
				{
					var loc = Me.Location;
					loc.X = -2105.246f;
					return loc;
				});

			// Avoid placed to the south side of path to prevent bot from going off path
			AddAvoidLocation(
				ctx => BladeLordTayakPhaseTwo,
				7,
				o =>
				{
					var loc = Me.Location;
					loc.X = -2133.169f;
					return loc;
				});

			return async boss =>
			{
				_tayak = boss;
				var unseenStrikeTarget = Me.RaidMembers.FirstOrDefault(r => !r.IsMe && r.HasAura("Unseen Strike"));

				if (unseenStrikeTarget != null)
				{
					if (unseenStrikeTarget.DistanceSqr > 4*4)
						return (await CommonCoroutines.MoveTo(unseenStrikeTarget.Location, unseenStrikeTarget.SafeName)).IsSuccessful();
					if (ScriptHelpers.MovementEnabled)
						ScriptHelpers.DisableMovement(() => unseenStrikeTarget.IsValid && !AvoidanceManager.IsRunningOutOfAvoid);
					return false;
				}
				if (boss.HealthPercent <= 20.1)
				{
					if (boss.DistanceSqr > 10*10)
						return (await CommonCoroutines.MoveTo(boss.Location, boss.SafeName)).IsSuccessful();

					if (ScriptHelpers.MovementEnabled && !AvoidanceManager.IsRunningOutOfAvoid && Me.IsHealer())
					{
						ScriptHelpers.DisableMovement(
							() =>
								boss != null && boss.IsValid && boss.IsAlive && boss.DistanceSqr <= 10*10 && !AvoidanceManager.IsRunningOutOfAvoid);
						
					}
				}

				return false;
			};
		}

		private PerFrameCachedValue<bool> _bladeLordTayakPhaseTwo;

		private bool BladeLordTayakPhaseTwo
		{
			get
			{
				return _bladeLordTayakPhaseTwo ??
						(_bladeLordTayakPhaseTwo =
							new PerFrameCachedValue<bool>(() => _tayak != null && _tayak.IsValid && _tayak.IsAlive && _tayak.HealthPercent <= 20.1));
			}
		}

		#endregion

		#region Garalon

		private const uint GaralonsLegId = 63053;
		private const uint GaralonId = 63191;
		private const uint PheromoneTrailId = 63021;
		private readonly WoWPoint _garalonRoomCenter = new WoWPoint(-1926.08, 475.6746, 470.9576);

		// dps the legs when alive
		// don't go under boss,
		// stay out of the puddles.
		// don't stand in front.
		// note: there're 2 Garalon ID's. This one isn't targetable but it contains the rotation where as the other one faces a fixed direction.
		[EncounterHandler(62164, "Garalon", BossRange = 130, Mode = CallBehaviorMode.Proximity)]
		public Composite GaralonEncounter()
		{
			WoWPoint moveTo = WoWPoint.Zero;
			WoWUnit garalon = null;
			WoWPlayer tank = null;
			var bottomOfStepLoc = new WoWPoint(-1968.789, 476.0225, 470.9576);
			// dont stand under boss.
			AddAvoidObject(ctx => true, () => _garalonRoomCenter, 45, o => Me.IsRange() && Me.IsMoving ? 26 : 20, GaralonId);
			// puddles left by Pheromones. only avoid those within 30 yards because the sheer number of these cause a performance hit 
			AddAvoidObject(ctx => true, 4, u => u.Entry == PheromoneTrailId && u.DistanceSqr <= 900, null, true);
			// avoid players with Pheromones.
			AddAvoidObject(ctx => !Me.HasAura("Pheromones"), 10, o => o is WoWPlayer && !o.IsMe && o.ToPlayer().HasAura("Pheromones"));

			return new PrioritySelector(
				ctx => garalon = ctx as WoWUnit,
				new Decorator(ctx => !appliedSpellBlacklist, new Action(ctx => ApplySpellBacklist())),
				new Decorator(
					ctx => Me.HasAura("Pheromones"),
					new PrioritySelector(
						ctx => tank = ScriptHelpers.Tank,
						new ActionSetActivity("Transfering Pheromones to tank"),
						new Decorator(ctx => tank != null && tank.IsAlive, new Action(ctx => Navigator.MoveTo(tank.Location))))),
				// avoid  standing in front.
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => garalon.Combat && garalon.Distance < 50, ctx => garalon, new ScriptHelpers.AngleSpan(0, 120)),
				// get off the stairs.
				new Decorator(
					ctx => !garalon.Combat && (moveTo = ScriptHelpers.GetGroupCenterLocation()) != WoWPoint.Zero && moveTo.X < -1972.993,
					new PrioritySelector(
						new Decorator(
							ctx => Me.X > -1972.993, new PrioritySelector(new ActionSetActivity("Moving to bottome of stairs"), new Action(ctx => Navigator.MoveTo(moveTo)))))));
		}

		#endregion

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
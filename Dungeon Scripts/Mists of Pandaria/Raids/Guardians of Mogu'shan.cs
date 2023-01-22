using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
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
	public class GuardiansOfMogushan : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 527; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(3978.82, 1115.574, 497.136); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == FengTheAccursedId && (unit.CastingSpellId == EpicenterId || unit.ChanneledCastingSpellId == EpicenterId) && Me.IsMelee())
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
					if (_stoneGuardIds.Contains(unit.Entry) && unit.HasAura("Solid Stone"))
						priority.Score -= 2000;
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

		private const int AmethystPoolId = 116235;

		//private const int AmethystPetrificationId = 116060;
		//private const int JadePetrificationId = 116008;
		//private const int CobaltPetrificationId = 115861;
		//private const int JaderPetrificationId = 116038;
		private const uint CobaltMineId = 65803;
		private const uint JasperGuardianId = 59915;
		private const uint JadeGuardianId = 60043;
		private const uint AmethystGuardianId = 60047;
		private const uint CoboltGuardianId = 60051;

		private const int EpicenterId = 116018;
		private const uint FengTheAccursedId = 60009;
		private readonly uint[] _stoneGuardIds = new[] { JasperGuardianId, JadeGuardianId, AmethystGuardianId, CoboltGuardianId };
		private readonly uint[] _trollExplosives = new uint[] { 60388, 60389, 60390, 60391, 60392, 60393, 60394, 60395, };

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private bool StoneGuardianIsDone
		{
			get
			{
				return
					!(ScriptHelpers.IsBossAlive("Jasper Guardian") && ScriptHelpers.IsBossAlive("Jade Guardian") && ScriptHelpers.IsBossAlive("Amethyst Guardian") &&
					  ScriptHelpers.IsBossAlive("Cobalt Guardian"));
			}
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector(
				new Decorator(
					ctx => !StoneGuardianIsDone,
				// set the context to an array of all the Stone Gaurd bosses.
				// This is a little more efficient then handling each one indiviually. 
					new PrioritySelector(
						ctx => ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => _stoneGuardIds.Contains(u.Entry) && u.IsAlive && u.Combat).ToArray(),
						new Decorator(ctx => ctx != null && ((WoWUnit[])ctx).Any(), TheStoneGuardEncounter()))));
		}

		public Composite TheStoneGuardEncounter()
		{
			AddAvoidObject(ctx => true, 4, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == AmethystPoolId);
			AddAvoidObject(ctx => !(Me.IsCasting && Me.CurrentCastTimeLeft >= TimeSpan.FromSeconds(1)), 7, CobaltMineId);
			WoWUnit[] bosses = null;
			// Jasper Chains seems very trivial so ignoring it. 
			return new PrioritySelector(ctx => bosses = ctx as WoWUnit[]);
		}

		// Wild fire. drop it out of group if  debuf is on you. avoid it on ground.
		// Lightning Charge. moves in a straight line, avoid it.
		// Run from group if you have the Arcane explosion debuf.
		// move onto platform to avoid getting shut out.

		[EncounterHandler(60009, "Feng the Accursed", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite FengTheAccursedEncounter()
		{
			const int wildfireSparkSpellId = 116784;
			const uint wildfireSparkId = 60438;
			const int arcaneResonanceId = 116417;
			const uint lightningChargeId = 60241;

			AddAvoidObject(ctx => true, 3, wildfireSparkId);
			AddAvoidObject(ctx => true, 7, o => o is WoWPlayer && o.ToPlayer().HasAura(arcaneResonanceId) && !o.IsMe);
			// AddAvoidObject(
			//     ctx => Me.IsRange(), 15, o => o.Entry == FengTheAccursedId && (o.ToUnit().CastingSpellId == EpicenterId || o.ToUnit().ChanneledCastingSpellId == EpicenterId));
			AddAvoidObject(
				ctx => true,
				5,
				o => o.Entry == lightningChargeId,
				o =>
				{
					var loc = o.Location;
					return Me.Location.GetNearestPointOnSegment(loc, loc.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 30));
				});

			var roomCenterLoc = new WoWPoint(4041.803, 1341.639, 466.3051);
			var insideGateLoc = new WoWPoint(4008.481, 1341.765, 466.3039);
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// move onto the platform to not get locked out when encounter starts
				new Decorator(
					ctx => !boss.Combat && ScriptHelpers.Tank != null && ScriptHelpers.Tank.Z >= 466,
					new PrioritySelector(
						new ActionSetActivity("Moving onto platform"),
						new Decorator(ctx => Me.Z < 466, new Action(ctx => Navigator.MoveTo(insideGateLoc))),
				// disable movement to prevent raid following while waiting inside gate
						new Decorator(ctx => ScriptHelpers.MovementEnabled, new Action(ctx => ScriptHelpers.DisableMovement(() => boss.IsAlive && !boss.Combat))))),
				// Boss combat behavior
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
				// Handle Wild fire and Arcane Resonance
						new Decorator(
							ctx => Me.HasAura(wildfireSparkSpellId) || Me.HasAura(arcaneResonanceId),
							new PrioritySelector(
								ctx => // find a location at the ouside prerimeter of the boss area that has no wild fires 
								(from point in GetPointsAroundCircle(roomCenterLoc, 30, 20)
								 let myLoc = Me.Location
								 let bossLoc = boss.Location
								 where !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(point)) && point.Distance(bossLoc) > 15 && point.Distance(bossLoc) <= 40
								 orderby point.DistanceSqr(myLoc)
								 select point).FirstOrDefault(),
								new ActionSetActivity("Running away from raid because I have Wild fire or Arcane Resonance"),
								new Decorator<WoWPoint>(
									runto => runto != WoWPoint.Zero && runto.Distance(Me.Location) > 4, new Helpers.Action<WoWPoint>(runto => Navigator.MoveTo(runto))),
								new Decorator<WoWPoint>(
									runto => runto != WoWPoint.Zero && ScriptHelpers.MovementEnabled && runto.Distance(Me.Location) <= 4,
									new Action(ctx => ScriptHelpers.DisableMovement(() => Me.HasAura(wildfireSparkSpellId) || Me.HasAura(arcaneResonanceId)))))),
				// stay behind the boss 
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => Me.IsMelee() && boss.IsWithinMeleeRange, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				// range should stay near center when not dropping wildfire or have Arcane Resonance
						new Decorator(
							ctx => Me.IsRange() && !Me.HasAura(wildfireSparkSpellId) && !Me.HasAura(arcaneResonanceId) && Me.Location.Distance(roomCenterLoc) > 15,
							new Action(ctx => Navigator.MoveTo(roomCenterLoc))),
						new Decorator(ctx => Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()))));
		}

		private IEnumerable<WoWPoint> GetPointsAroundCircle(WoWPoint centerLoc, float radius, float stepDegree)
		{
			const float pix2 = (float)(Math.PI * 2);
			var stepRadian = WoWMathHelper.DegreesToRadians(stepDegree);
			for (float ang = 0; ang < pix2; ang += stepRadian)
			{
				yield return centerLoc.RayCast(ang, radius);
			}
		}

		// Test explosives, moves in staight line and and should be avoided 
		// run from NPC that fixates on you

		[EncounterHandler(60143, "Gara'jal the Spiritbinder", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite GarajaltheSpiritbinderEncounter()
		{
			AddAvoidObject(
				ctx => !Me.IsCasting,
				4,
				o => _trollExplosives.Contains(o.Entry),
				o =>
				{
					var loc = o.Location;
					return Me.Location.GetNearestPointOnSegment(loc, loc.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 30));
				});
			var insideDoorLoc = new WoWPoint(4239.904, 1342.293, 454.153);
			var roomCenterLoc = new WoWPoint(4277.229, 1341.445, 454.4593);

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// move onto the platform to not get locked out when encounter starts
				new Decorator(
					ctx => !boss.Combat && boss.Attackable,
					new PrioritySelector(
						new Decorator(
							ctx => Me.Location.Distance(roomCenterLoc) >= 40,
							new PrioritySelector(new ActionSetActivity("Moving inside room."), new Action(ctx => Navigator.MoveTo(insideDoorLoc)))),
						new Decorator(
							ctx => Me.Location.Distance(roomCenterLoc) < 40 && ScriptHelpers.MovementEnabled,
							new Action(ctx => ScriptHelpers.DisableMovement(() => boss.IsAlive && !boss.Combat))))),
				// combat behavior
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
				// Enter spirit World behavior.. todo.
						new Decorator(ctx => !Me.HasAura("Voodoo Doll") && !Me.HasAura("Crossed Over"), new PrioritySelector()))));
		}
	}
}
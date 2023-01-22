using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using Bots.DungeonBuddy.Profiles.Handlers;
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
	public class TerraceOfEndlessSpring : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 526; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(958.4459, -51.12228, 513.1506); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == LeiShiId && unit.HasAura("Protect"))
							return true;
						if (unit.Entry == TerrorSpawnId && !Me.IsMelee())
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
					if (unit.Entry == CorruptedWatersId)
						outgoingunits.Add(unit);

					else if (unit.Entry == UnstableShaId && Me.IsDps())
						outgoingunits.Add(unit);

					else if (unit.Entry == TerrorSpawnId && unit.Distance < 80 && Me.IsMelee())
						outgoingunits.Add(unit);

					else if (_terraceGuardians.Contains(unit.Entry) && unit.Attackable && unit.Distance <= 40)
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
					if (unit.Entry == CorruptedWatersId && Me.IsDps())
						priority.Score += 5000;

					else if (unit.Entry == UnstableShaId && Me.IsDps())
						priority.Score += (Me.IsRange() ? 5000 : -1000) - unit.Distance;

					else if (unit.Entry == TerrorSpawnId && Me.IsMelee() && unit.Distance <= 100)
						priority.Score = 5000 - unit.Distance;

					else if (_terraceGuardians.Contains(unit.Entry) && unit.Attackable && unit.Distance <= 40)
						priority.Score += 5000;
				}
			}
		}

		public override void IncludeHealTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			// Some CRs remove all WoWUnits.
			if (ScriptHelpers.IsViable(_tsulong) && !_tsulong.IsHostile && _tsulong.IsAlive)
				outgoingunits.Add(_tsulong);
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

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Protectors of the Endless

		private const uint CorruptedWatersId = 60621;
		private WoWPoint _protectersCenterLoc = new WoWPoint(-1017.489, -3048.979, 12.82353);

		private const uint ProtectorKaolanId = 60583;
		private const uint ElderRegailId = 60585;
		private const uint ElderAsaniId = 60586;

		private readonly uint[] _protectorIds = {ProtectorKaolanId, ElderRegailId, ElderAsaniId};

		[EncounterHandler(60585, "Elder Regail")]
		public Func<WoWUnit, Task<bool>> ElderRegailEncounter()
		{
			// Don't aggro any of the protectors
			AddAvoidObject(
				ctx => true,
				25,
				o => _protectorIds.Contains(o.Entry) && !o.ToUnit().Combat && o.ToUnit().IsAlive);
			
			AddAvoidObject(
				ctx => true, 
				7, 
				o => o is WoWPlayer && (Me.HasAura("Lightning Prison") 
					&& !o.IsMe 
					|| !Me.HasAura("Lightning Prison") 
					&& o.ToUnit().HasAura("Lightning Prison")));

			return async boss => false;
		}
		#endregion

		#region Tsulong

		private const uint SunbeamId = 62849;
		private const uint UnstableShaId = 62919;
		private const uint Tsulongid = 62442;
		private WoWUnit _tsulong;
		[EncounterHandler(62442, "Tsulong", Mode = CallBehaviorMode.Proximity)]
		public Func<WoWUnit, Task<bool>> TsulongEncounter()
		{
			const uint maxDreadShadowsStack = 8;
			const int nightMaresMissileId = 122770;

			AddAvoidObject(
				ctx => true, 
				25,
				o => o.Entry == Tsulongid && !o.ToUnit().Combat && o.ToUnit().HealthPercent == 100 );
			
			AddAvoidLocation(
				ctx => true,
				9, 
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == nightMaresMissileId));

			return async boss =>
						{
							_tsulong = boss;
							var sunbeam = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == SunbeamId);
							// move to sunbeam
							if ( sunbeam != null
								&& Me.HasAura("Dread Shadows")
								&& Me.Auras["Dread Shadows"].StackCount >= maxDreadShadowsStack)
							{
								return (await CommonCoroutines.MoveTo(sunbeam.Location, "Sun Beam")).IsSuccessful();
							}
							// dispell boss							
							if (await ScriptHelpers.DispelFriendlyUnit("Terrorize", ScriptHelpers.PartyDispelType.Magic, boss))
								return true;

							var bossRotation = WoWMathHelper.NormalizeRadian(boss.Rotation);
							// note: sometimes rotation will end up being zero if value is read while frame is not locked.
							if (bossRotation != 0f && Me.IsHealer() && boss.IsFriendly)
							{
								// healers should stand in front of boss.
								if (!WoWMathHelper.IsSafelyFacing(boss.Location, bossRotation, Me.Location) || boss.Distance > 15)
								{
									var moveTo = boss.Location.RayCast(WoWMathHelper.NormalizeRadian(bossRotation), 10);
									return (await CommonCoroutines.MoveTo(moveTo)).IsSuccessful();
								}
								// disable movement to prevent CR from moving away...
								if (ScriptHelpers.MovementEnabled)
								{
									ScriptHelpers.DisableMovement(
										() => boss.IsValid && boss.IsFriendly && boss.IsSafelyFacing(Me) && boss.Distance <= 15);
								}
							}
							return false;
						};
		}

		#endregion

		#region Lei Shi

		private const uint LeiShiId = 62983;
		private const int GetAwayId = 123461;

		private readonly string[] _aoeSpellNames =
		{
			"Divine Storm","Thunder Clap","Heroic Leap",  "Arcane Explosion", "Rain of Fire", "Hurricane", "Fan of Knives", "Death and Decay",
			"Explosive Trap", "Magma Totem"
		};

		[EncounterHandler(62983, "Lei Shi")]
		public Func<WoWUnit, Task<bool>> LeiShiEncounter()
		{
			// stay away from the spray target.
			AddAvoidObject(ctx => true, 3, o => o.Entry == LeiShiId && o.ToUnit().CurrentTargetGuid != 0 && o.ToUnit().CurrentTargetGuid != Me.Guid, o => o.ToUnit().CurrentTarget.Location);

			return async boss =>
			{
				if (boss.CastingSpellId == GetAwayId && boss.Distance > 25)
				{
					Navigator.PlayerMover.MoveTowards(boss.Location);
					return true;
				}
				return false;
			};
		}

		[EncounterHandler(63099, "Lei Shi Hidden", Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> HiddenLeiShiEncounter(WoWUnit boss)
		{
			var aoeSpell = GetAoeSpell();
			if (Me.IsHealer() || aoeSpell == null)
				return false;
			var spellRange = (aoeSpell.IsMeleeSpell || aoeSpell.MaxRange == 0 ? 4 : 30);
			if (boss.DistanceSqr > spellRange*spellRange)
				return (await CommonCoroutines.MoveTo(boss.Location, "get closer to boss")).IsSuccessful();
		
			await ScriptHelpers.StopMovingIfMoving();

			if (SpellManager.CanCast(aoeSpell) 
				|| Me.Class == WoWClass.Shaman && Me.Totems.All(t => t.WoWTotem != WoWTotem.Magma))
			{
				SpellManager.Cast(aoeSpell);
				if (Me.CurrentPendingCursorSpell != null)
					SpellManager.ClickRemoteLocation(boss.Location);
				await ScriptHelpers.SleepForRandomReactionTime();
			}

			// dow't do anything
			return true;
		}

		private WoWSpell GetAoeSpell()
		{
			SpellFindResults results = null;
			return _aoeSpellNames.Where(s => SpellManager.FindSpell(s, out results)).Select(s => results.Override ?? results.Original).FirstOrDefault();
		}


		#endregion

		#region Sha of Fear

		private const uint YangGuoshiId = 61038;
		private const uint JinlunKunId = 61046;
		private const uint ChengKangId = 61042;
		private const uint TerrorSpawnId = 61034;
		private const uint ReturnToTheTerraceId = 65736;
		private const uint ShaGlobeId = 65691;
		const uint PenetratingBoltMissileId = 129077;
		private const uint PureLightTerraceId = 60788;

		private readonly uint[] _terraceGuardians = new[] { YangGuoshiId, JinlunKunId, ChengKangId };

		[EncounterHandler(60999, "Sha of Fear", BossRange = 15000)]
		public Func<WoWUnit, Task<bool>> ShaOfFearEncounter()
		{
			var safeZoneLoc = new WoWPoint(-1019.035, -2798.958, 38.3063);
			var randomSpotAtSafeLoc = WoWPoint.Zero;

			// don't stand on top of the terror spawns because they can still deflect attacks if standing too close.
			AddAvoidObject(ctx => true, 2, TerrorSpawnId);
			// avoid the missiles launched by the terror spawns.
			AddAvoidLocation(
				ctx => true,
				3, 
				o => ((WoWMissile)o).ImpactPosition, 
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == PenetratingBoltMissileId));

			return async boss =>
			{
				var returnToTerrace = ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(u => u.Entry == ReturnToTheTerraceId && u.Distance <= 50);

				// interact with the 'Return to the Terrace" portal
				if (returnToTerrace != null)
				{
					// there 'might' be an issue with using the 'WithinInteractRange' on this object so going to use a constant distance check.
					if (returnToTerrace.Distance > 3.5f)
						return (await CommonCoroutines.MoveTo(returnToTerrace.Location, "interact with portal")).IsSuccessful();
					returnToTerrace.Interact();
					return true;
				}

				var myTarget = Me.CurrentTarget;
				// stand behind the Terror Spawns..
				if (myTarget != null && myTarget.Entry == TerrorSpawnId)
				{
					if ( myTarget.IsFacing(Me))
						return await ScriptHelpers.GetBehindUnit(myTarget);
				}
				else if (Me.IsRange() && Me.Location.DistanceSqr(safeZoneLoc) < 60*60)
				{
					if (randomSpotAtSafeLoc == WoWPoint.Zero)
						randomSpotAtSafeLoc = ScriptHelpers.GetRandomPointAtLocation(safeZoneLoc, 7);
					// stand at safe spot to prevents player from getting feared.
					if (!AvoidanceManager.IsRunningOutOfAvoid && Me.Location.DistanceSqr(randomSpotAtSafeLoc) > 10 * 10)
						return (await CommonCoroutines.MoveTo(randomSpotAtSafeLoc, "Safe spot")).IsSuccessful();
				}

				return false;
			};
		}

		#endregion
	}
}
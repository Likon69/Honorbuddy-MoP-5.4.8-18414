using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Markup;
using Bots.DungeonBuddy.Enums;
using Bots.DungeonBuddy.Profiles.Handlers;
using CommonBehaviors.Actions;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.TreeSharp;
using Styx;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Cataclysm
{
	public class HeroicEndTime : Dungeon
	{
		#region Overrides of Dungeon

		/// <summary>
		///   The Map Id of this dungeon. This is the unique id for dungeons thats used to determine which dungeon, the script belongs to
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId
		{
			get { return 435; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-8281.537, -4453.549, -208.3219); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(3730.121, -427.7061, 112.455); }
		}

		/// <summary>
		///   IncludeTargetsFilter is used to add units to the targeting list. If you want to include a mob thats usually removed by the default filters, you shall use that.
		/// </summary>
		/// <param name="incomingunits"> Units passed into the method </param>
		/// <param name="outgoingunits"> Units passed to the targeting class </param>
		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (WoWObject obj in incomingunits)
			{
				var unit = obj.ToUnit();
				if (unit == null)
					continue;

				if (unit.Entry == EchoOfBaine && StyxWoW.Me.IsHealer())
					// make sure healer has something in target list so he heals lava damage.
					outgoingunits.Add(unit);

				if (unit.Entry == FountainOfLight)
					outgoingunits.Add(unit);

				if (unit.Entry == RisenGhoul)
					outgoingunits.Add(unit);
			}
		}

		/// <summary>
		///   RemoveTargetsFilter is used to remove units thats not wanted in target list. Like immune mobs etc.
		/// </summary>
		/// <param name="units"> </param>
		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;

					if (unit == null)
						return false;

					if (_runAwayFromSylvanas)
						return true;

					if (unit.Entry == EchoOfBaine && !StyxWoW.Me.IsHealer() && StyxWoW.Me.Z < BaineLavaZLevel)
						return true;

					if (unit.HasAura("Shadow of Obsidius"))
						return true;

					if (unit.Entry == EchoOfJaina && !unit.Combat && _ignoreJaina)
						return true;

					if (unit.Entry == EchoOfSylvanas && unit.HasAura("Calling of the Highborne") && unit.Combat && !StyxWoW.Me.IsHealer())
						return true;
					if (_doingMoonWalk && (StyxWoW.Me.IsTank() && (!unit.Combat || unit.Combat && unit.CurrentTarget == StyxWoW.Me) || StyxWoW.Me.IsDps()))
						return true;

					if (!_doingMoonWalk && _saberIds.Contains(unit.Entry) && !unit.Combat)
						return true;
					// ignore murozond if he's flying to not pull aggro on pull.
					// ToDo: find a replacement for unit.StateFlag. nolonger used after new HB.
					// if (unit.Entry == Murozond && StyxWoW.Me.IsDps() && unit.StateFlag != WoWStateFlag.None)
					//      return true;

					return false;
				});
		}


		/// <summary>
		///   WeighTargetsFilter is used to weight units in the targeting list. If you want to give priority to a certain npc, you should use this method.
		/// </summary>
		/// <param name="units"> </param>
		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var t in units)
			{
				var unit = t.Object.ToUnit();
				if (unit == null)
					continue;

				if (unit.Entry == FountainOfLight)
					t.Score += 500;

				if (unit.Entry == RisenGhoul)
					t.Score += 1000 + ((unit.MaxHealth - unit.CurrentHealth) * 1000) - unit.Location.DistanceSqr(_ghoulLoc);

				if (unit.Entry == InfiniteWarden)
					t.Score += 500;
			}
		}

		#endregion

		#region Encounter Handlers

		private const uint Nozdormu = 54476;
		private const uint Alurmi = 57864;
		private const uint Murozond = 54432;
		private const uint UndyingFlame = 54550;
		private const uint FountainOfLight = 54795;
		private const uint FragmentOfJainasStaff = 209318;
		private const uint FlarecoreEmber = 54446;
		private const uint RisenGhoul = 54191;
		private const uint EchoOfJaina = 54445;
		private const uint EchoOfSylvanas = 54123;
		private const uint EchoOfTyrandeId = 54544;
		private const uint PoolOfMoonlight = 54508;
		private const uint EchoOfBaine = 54431;
		private const int DestroyedPlatformDisplayId = 10886;
		private const float BaineLavaZLevel = 129.95f;
		private const uint BainesTotem = 54434;
		private const int ThrowTotem = 101603;
		private const uint HourglassOfTime = 209249;
		private const uint InfiniteWarden = 54923;

		private const uint NorthPlatform = 209693;
		private const uint EastPlatform = 209670;
		private const uint SouthPlatform = 209695;
		private const uint WestPlatform = 209694;

		private const uint TimeTransitDevice = 209441;

		private readonly DungeonArea _baineArea = new DungeonArea(
			new Vector2(4518.893f, 1207.923f), new Vector2(4286.12f, 1216.014f), new Vector2(4296.304f, 1588.682f), new Vector2(4505.072f, 1616.999f));

		private readonly WoWPoint _bainePortalLoc = new WoWPoint(4349.405, 1285.794, 147.6583);

		private readonly DungeonArea _jainaArea = new DungeonArea(
			new Vector2(3268.74f, 345.4715f), new Vector2(2959.062f, 294.5733f), new Vector2(2946.141f, 684.3937f), new Vector2(3242.467f, 707.7469f));

		private readonly WoWPoint _jainaPortalLoc = new WoWPoint(2991.796, 560.4416, 25.28839);

		private readonly DungeonArea _murozondArea = new DungeonArea(
			new Vector2(4261.447f, -589.5588f), new Vector2(4004.345f, -591.4884f), new Vector2(4010.591f, -248.5168f), new Vector2(4234.73f, -233.563f));

		private readonly WoWPoint _murozondPortalLoc = new WoWPoint(4035.225, -353.193, 122.541);
		private readonly uint[] _platformIds = { EastPlatform, SouthPlatform, NorthPlatform, WestPlatform };

		private readonly DungeonArea _startArea = new DungeonArea(
			new Vector2(3806.894f, -349.2974f),
			new Vector2(3787.267f, -477.232f),
			new Vector2(3678.344f, -478.5883f),
			new Vector2(3641.459f, -324.114f));

		private readonly WoWPoint _startPortalLoc = new WoWPoint(3697.036, -367.9028, 113.7821);

		private readonly DungeonArea _sylvanasArea = new DungeonArea(
			new Vector2(4000.151f, 1285.679f), new Vector2(4029.977f, 561.3909f), new Vector2(3565.349f, 577.9777f), new Vector2(3569.989f, 1269.092f));

		private readonly WoWPoint _sylvanasPortalLoc = new WoWPoint(3829.036, 1110.17, 84.14674);

		private const uint StartPortalId = 209441;
		private const uint SylvanasPortalId = 209443;
		private const uint TyrandePortalId = 209438;
		private const uint BainePortalId = 209439;
		private const uint MurozondPortalId = 209440;
		private const uint JainaPortalId = 209442;
		private readonly uint[] _timeTransitDevices =
		{
			TyrandePortalId,
			BainePortalId,
			MurozondPortalId,
			JainaPortalId,
			StartPortalId,
			SylvanasPortalId
		};

		private readonly DungeonArea _tyrandeArea = new DungeonArea(
			new Vector2(3009.238f, -246.5252f), new Vector2(2623.441f, -227.9786f), new Vector2(2658.125f, 298.742f), new Vector2(2993.23f, 312.0955f));

		private readonly WoWPoint _tyrandePortalLoc = new WoWPoint(2937.735, 83.60256, 6.670205);
		private readonly WaitTimer _useTransitTimer = new WaitTimer(TimeSpan.FromSeconds(15));
		private bool _activateHourGlass;

		/// <summary>
		///   Using 0 as BossEntry will make that composite the main logic for the dungeon and it will be called in every tick You can only have one main logic for a dungeon The context of the main composite is all units around as List <WoWUnit />
		/// </summary>
		/// <returns> </returns>

		#region Root
		LocalPlayer Me { get { return StyxWoW.Me; } }

		[EncounterHandler(0)]
		public Composite RootLogic()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(54751, "Nozdormu", Mode = CallBehaviorMode.Proximity)] 
		[EncounterHandler(54476, "Nozdormu", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(57864, "Alurmi", Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		#endregion

		#region Echo of Baine

		[EncounterHandler(54543, "Time-Twisted Drake")]
		public async Task<bool> TimeTwistedDrakeEncounter(WoWUnit npc)
		{
			return await ScriptHelpers.TankFaceUnitAwayFromGroup(17);
		}

		/// <summary>
		///   BossEntry is the Entry of the boss unit. (WoWUnit.Entry) BossName is optional. Its there just to make it easier to find which boss that composite belongs to. The context of the encounter composites is the Boss as WoWUnit
		/// </summary>
		/// <returns> </returns>
		[EncounterHandler(54431, "Echo of Baine", Mode = CallBehaviorMode.Proximity, BossRange = 210)]
		public Func<WoWUnit, Task<bool>> EchoOfBaineFightLogic()
		{
			WoWGameObject currentIsland = null;
			WoWUnit totem = null;
			var eastIslandStandLoc = new WoWPoint(4380.827f, 1412.093f, 130.823f);
			var southIslandStandPoint = new WoWPoint(4360.898f, 1457.463f, 130.0503f);
			var northIslandStandPoint = new WoWPoint(4389.616f, 1456.567f, 130.3061f);

			var lavaCenterLoc = new WoWPoint(4378.449, 1445.719, 127.5554);

			AddAvoidObject(ctx => true, 4, UndyingFlame);

			return async boss =>
			{
				var isTank = Me.IsTank();
				if (!boss.Combat)
				{
					var yCoord = Me.Y;
					if (isTank && yCoord < 1370 && Targeting.Instance.IsEmpty())
					{
						ScriptHelpers.SetLeaderMoveToPoi(boss.Location);
						return false;
					}
					if (yCoord > 1370 && !Me.Combat)
					{
						var moveTo = WoWPoint.Zero;
						if (ScriptHelpers.MovementEnabled)
							ScriptHelpers.DisableMovement(() => !boss.Combat);
						if (yCoord < 1400)
							moveTo = eastIslandStandLoc;
						if (yCoord > 1400 && yCoord < 1450)
							moveTo = StyxWoW.Me.IsRangeDps() ? southIslandStandPoint : northIslandStandPoint;
						if (yCoord > 1450 && isTank && ScriptHelpers.GroupMembers.All(
								p => p.Player != null && !p.Player.HasAura("Magma")
									&& p.Location.DistanceSqr(boss.Location) < 60*60
									&& p.IsAlive && p.HealthPercent >= 75))
						{
							moveTo = boss.Location;
						}

							// don't rely on getting a rez in the lava.
						if (Me.IsDead && Me.Z < BaineLavaZLevel)
						{
							Lua.DoString("UseSoulstone() RepopMe()");
							return true;
						}
						if (Me.HasAura("Magma"))
						{
							Logger.Write("[Echo Of Baine] Waiting for Magma debuf to drop");
							return true;
						}
						if (moveTo != WoWPoint.Zero && Me.Location.DistanceSqr(moveTo) > 3 * 3)
						{
							WoWMovement.ClickToMove(moveTo);
							return true;
						}
						if (!Me.IsHealer() || Me.Z < BaineLavaZLevel)
							return true;
					}
				}
				// inCombat behavior
				else
				{
					totem = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BainesTotem);
					WoWGameObject movetoIsland = null;
					using (StyxWoW.Memory.AcquireFrame())
					{
						WoWGameObject[] activeIslands = ObjectManager.GetObjectsOfType<WoWGameObject>()
							.Where(u => _platformIds.Contains(u.Entry) && u.DisplayId != DestroyedPlatformDisplayId)
							.ToArray();

						WoWGameObject islandBossIsOn = boss.Z >= BaineLavaZLevel ? activeIslands
							.FirstOrDefault(i => i.Location.DistanceSqr(boss.Location) <= 15 * 15) : null;

						var healer = ScriptHelpers.Healer;
						WoWGameObject islandHealerIsOn = healer != null && healer.Z >= BaineLavaZLevel
							? activeIslands.FirstOrDefault(i => i.Location.DistanceSqr(healer.Location) <= 15 * 15)
							: null;

						currentIsland = StyxWoW.Me.Z >= BaineLavaZLevel
											? activeIslands.FirstOrDefault(i => i.Location.DistanceSqr(StyxWoW.Me.Location) <= 15 * 15)
											: null;

						movetoIsland = null;

						if ((Me.IsMelee() && currentIsland != islandBossIsOn && islandBossIsOn != null) || (currentIsland == null))
						{
							movetoIsland =
								activeIslands.Where(
									i =>
									(activeIslands.Length > 1 && i != islandHealerIsOn || activeIslands.Length == 1) &&
									i.Location.DistanceSqr(boss.Location) <= 50 * 50).OrderBy(i => i.DistanceSqr).FirstOrDefault() ??
								activeIslands.OrderBy(i => i.DistanceSqr).FirstOrDefault();
						}				
				}

				// pickup totem
				if (totem != null && Me.PartyMembers.OrderBy(p => p.Location.DistanceSqr(totem.Location))
					.FirstOrDefault().IsMe)
				{
					Logger.Write("[Echo Of Baine] Picking up totem");
					totem.Interact();
					await CommonCoroutines.SleepForLagDuration();
					return true;
				}


				// island behavior 
				if (movetoIsland != null && movetoIsland != currentIsland)
				{
					WoWMovement.ClickToMove(movetoIsland.Location);
					if (ScriptHelpers.MovementEnabled)
						ScriptHelpers.DisableMovement(() => Me.Z < BaineLavaZLevel);
					return false;
				}
				// if boss is in lava then pull him to the center
				if (currentIsland != null && boss.Z < BaineLavaZLevel 
					&& ((Me.IsTank() && boss.CurrentTarget == Me) || Me.IsMeleeDps()))
				{
					WoWMovement.ClickToMove(currentIsland.Location);
					TreeRoot.StatusText = "[Echo Of Baine] Moving to center of island while boss is in lava.";
					return true;
				}
				if (await ScriptHelpers.DispelGroup("Molten Blast", ScriptHelpers.PartyDispelType.Magic))
					return true;
				if (Me.HasPendingSpell("Throw Totem"))
				{
					SpellManager.ClickRemoteLocation(boss.Location);
					Logger.Write("[Echo Of Baine] Throwing totem on boss");
					return true;
				}}
	

				if (Me.IsMeleeDps() && !Me.HasAura("Molten Fists") && currentIsland != null)
				{
					Logger.Write("[Echo Of Baine] Getting Molten Fists");
					WoWMovement.ClickToMove(WoWMathHelper.CalculatePointFrom(Me.Location, currentIsland.Location, 15));
					await CommonCoroutines.SleepForLagDuration();
					return true;
				}

				return false;
			};
		}

		#endregion

		#region Echo of Jaina

		private bool _ignoreJaina;

		[ObjectHandler(209318, "Fragment of Jaina's Staff", ObjectRange = 250)]
		public async Task<bool> FragmentofJainasStaffHandler(WoWGameObject fragment)
		{
			var distSqr = fragment.DistanceSqr;
			if (distSqr <= 25 * 25 && !StyxWoW.Me.Combat)
			{
				if (distSqr > 6*6)
					return (await CommonCoroutines.MoveTo(fragment.Location, fragment.SafeName)).IsSuccessful();
				return await ScriptHelpers.InteractWithObject(fragment, 2000);
			}
			if (BotPoi.Current.Type != PoiType.Hotspot)
				ScriptHelpers.SetLeaderMoveToPoi(fragment.Location, fragment.SafeName);
			return false;
		}

		[EncounterHandler(54445, "Echo of Jaina", Mode = CallBehaviorMode.Proximity, BossRange = 200)]
		public Func<WoWUnit, Task<bool>> EchoofJainaEncounter()
		{
			bool handleFlameCore = false;
			const int frostboltVolley = 101810;
			WoWUnit trash = null;
			const uint frostBladeId = 54494;

			AddAvoidObject(ctx => trash != null, 20, u => u.Entry == EchoOfJaina && u.ToUnit().IsAlive && !u.ToUnit().Combat);
			AddAvoidObject(ctx => true, 3, u => u.Entry == frostBladeId,
				u => Me.Location.GetNearestPointOnSegment(u.Location, u.Location.RayCast(u.Rotation, 30)));

			return async boss =>
			{
				if (!boss.Combat)
				{
					trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(boss.Location, 150, u => u != boss).FirstOrDefault();
					_ignoreJaina = trash != null;
					if (_ignoreJaina)
						return await ScriptHelpers.ClearArea(boss.Location, 150, u => u != boss);
					ScriptHelpers.SetLeaderMoveToPoi(boss.Location);
					return false;
				}
				WoWUnit flamecore = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == FlarecoreEmber && u.HasAura("Flarecore"));

				if (flamecore != null && Me.IsDps())
				{
					var rangedDps =
						ScriptHelpers.GetGroupMembersByRole(ScriptHelpers.GroupRoleFlags.RangedDps)
							.Where(p => p.IsAlive)
							.OrderBy(p => p.MaxHealth).FirstOrDefault();

					var meleeDps =
						ScriptHelpers.GetGroupMembersByRole(ScriptHelpers.GroupRoleFlags.MeleeDps)
							.Where(p => p.IsAlive)
							.OrderBy(p => p.MaxHealth).FirstOrDefault();

					if (rangedDps != null && rangedDps.IsMe)
						handleFlameCore = true;
					else if (rangedDps == null && meleeDps != null && meleeDps.IsMe)
						handleFlameCore = true;
					else handleFlameCore = false;
				}
				if (handleFlameCore && flamecore != null)
				{
					Logger.Write("Moving to Flamecore");
					return (await CommonCoroutines.MoveTo(flamecore.Location, "Flamecore")).IsSuccessful();
				}
				return await ScriptHelpers.InterruptCast(boss, frostboltVolley);
			};
		}

		#endregion

		#region Echo of Sylvanas

		private readonly WoWPoint _ghoulLoc = new WoWPoint(3823.271, 935.1199, 55.81496);
		private readonly WoWPoint _sylvanasLoc = new WoWPoint(3840.03, 914.043, 56.0547);
		private bool _runAwayFromSylvanas;

		[EncounterHandler(54123, "Echo of Sylvanas", Mode = CallBehaviorMode.Proximity, BossRange = 250)]
		public Func<WoWUnit, Task<bool>> EchoofSylvanasEncounter()
		{
			WoWUnit trash = null;
			const uint blightedArrows = 54403;
			var lastGhoulLoc = WoWPoint.Zero;
			var runToLoc = WoWPoint.Zero;

			AddAvoidObject(ctx => true, 6, blightedArrows);

			return async boss =>
			{
				if (!boss.Combat)
				{
					trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(boss.Location, 70, u => u != boss).FirstOrDefault();
					if (trash != null)
						return await ScriptHelpers.ClearArea(boss.Location, 70, u => u != boss);
					return false;
				}
				var ghouls = ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == RisenGhoul && u.IsAlive).ToList();
				_runAwayFromSylvanas = ghouls.Count > 0 && ghouls.Count < 8
					|| ghouls.Any(g => g.Location.Distance2DSqr(boss.Location) <= 7 * 7);

				WoWPoint rangeNukeSpot;
				if (!Targeting.Instance.IsEmpty() && Targeting.Instance.FirstUnit.Entry == RisenGhoul && !_runAwayFromSylvanas)
				{
					lastGhoulLoc = Targeting.Instance.FirstUnit.Location;
					rangeNukeSpot = StyxWoW.Me.Class == WoWClass.Hunter 
						? _sylvanasLoc 
						: WoWMathHelper.CalculatePointFrom(lastGhoulLoc, _sylvanasLoc, 5);
				}
				else
				{
					rangeNukeSpot = _sylvanasLoc;
				}

				if (_runAwayFromSylvanas)
					runToLoc = WoWMathHelper.CalculatePointFrom(lastGhoulLoc, boss.Location, 16);

				if (_runAwayFromSylvanas)
				{
					if (Me.Location.DistanceSqr(runToLoc) > 3 * 3)
					{
						if (Me.MovementInfo.MovingBackward)
							WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);

						if (!ScriptHelpers.MovementEnabled)
							ScriptHelpers.RestoreMovement();
						Logger.Write("[Echo Of Sylvanas] Running from Slyvanas");
						return (await CommonCoroutines.MoveTo(runToLoc)).IsSuccessful();
					}
					return true;
				}

				if (!boss.HasAura("Jump") && await ScriptHelpers.SpreadOut(boss.Location, 11, 30))
					return true;
				
				if (boss.HasAura("Jump"))
				{
					if (Me.IsRange())
					{
						if (Me.Location.DistanceSqr(rangeNukeSpot) > 3*3)
						{
							await CommonCoroutines.MoveTo(rangeNukeSpot);
							return true;
						}
					}
					else
					{
						// melee behavior for ghouls
						var tar = Targeting.Instance.FirstUnit;
						if (tar != null && tar.Entry == RisenGhoul)
						{
							// manually move to ghoul if a warrior or monk 
							// to prevent charging as it sometimes lands behind and causing them to die
							if ( (Me.Class == WoWClass.Warrior || Me.Class == WoWClass.Monk) && tar.DistanceSqr > 8 * 8)
								return (await CommonCoroutines.MoveTo(tar.Location)).IsSuccessful();

							if (ScriptHelpers.MovementEnabled)
								ScriptHelpers.DisableMovement(() => ScriptHelpers.IsViable(boss) && boss.HasAura("Jump"));
							
							BackpedalUnitToLocation(_sylvanasLoc, tar);
							return false;
						}
					}
				}
				if (await ScriptHelpers.DispelGroup("Shriek of the Highborne", ScriptHelpers.PartyDispelType.Magic))
					return true;
				// don't do anything if targeting is empty
				return Targeting.Instance.IsEmpty() && !Me.IsHealer();
			};
		}

		private void BackpedalUnitToLocation(WoWPoint destination, WoWUnit unit)
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				var unitLoc = unit.Location;
				var myLoc = Me.Location;

				var unitToDestinationDistance = unitLoc.Distance(destination);
				var meToDestinationDistance = myLoc.Distance(destination);

				var maxMeleeRange = unit.MeleeRange();
				//float minMeleeRange = maxMeleeRange;
				var me2Unit = myLoc - unit.Location;
				var unit2End = unit.Location - destination;
				me2Unit.Normalize();
				unit2End.Normalize();

				if (!WoWMovement.IsFacing)
					WoWMovement.ConstantFace(unit.Guid);

				if (unit.Distance > maxMeleeRange || meToDestinationDistance >= unitToDestinationDistance)
				{
					if (StyxWoW.Me.MovementInfo.MovingBackward)
						Lua.DoString("MoveBackwardStop()");
					if (StyxWoW.Me.MovementInfo.MovingStrafeLeft)
						Lua.DoString("StrafeLeftStop()");
					if (StyxWoW.Me.MovementInfo.MovingStrafeRight)
						Lua.DoString("StrafeRightStop()");

					if (!StyxWoW.Me.MovementInfo.MovingForward)
						Lua.DoString("MoveForwardStart()");
					return;
				}

				if (StyxWoW.Me.MovementInfo.MovingForward)
					Lua.DoString("MoveForwardStop()");

				if (myLoc.DistanceSqr(unitLoc) <= maxMeleeRange * maxMeleeRange && !StyxWoW.Me.MovementInfo.MovingBackward)
					Lua.DoString("MoveBackwardStart()");
				else if (StyxWoW.Me.MovementInfo.MovingBackward)
					Lua.DoString("MoveBackwardStop()");


				var dot = Math.Abs(me2Unit.Dot(unit2End));
				if (dot < 0.9f)
				{
					var isLeft = myLoc.IsPointLeftOfLine(unit.Location, destination);

					if (!StyxWoW.Me.MovementInfo.MovingStrafeLeft && isLeft)
						Lua.DoString("StrafeLeftStart()");

					if (!StyxWoW.Me.MovementInfo.MovingStrafeRight && !isLeft)
						Lua.DoString("StrafeRightStart()");
				}
				else if (StyxWoW.Me.MovementInfo.MovingStrafeLeft)
					Lua.DoString("StrafeLeftStop()");
				else if (StyxWoW.Me.MovementInfo.MovingStrafeRight)
					Lua.DoString("StrafeRightStop()");
			}
		}

		#endregion

		#region Echo of Tyrande

		private readonly uint[] _saberIds = { 54688, 54699, 54700 };
		private bool _doingMoonWalk;
		private WoWUnit _tyrande;

		readonly uint[] _eyeOfEluneIds =
		{ 
			54594, // range 20
			54597
		};
		PerFrameCachedValue<bool> _runFromEyesOfElune;

		bool RunFromEyesOfElune
		{
			get
			{
				return _runFromEyesOfElune ?? (_runFromEyesOfElune = new PerFrameCachedValue<bool>(
					() => ObjectManager.GetObjectsOfType<WoWUnit>()
						.Any(u => _eyeOfEluneIds.Contains(u.Entry) && u.DistanceSqr <= 10*10 
							&& u.Location.DistanceSqr(_tyrande.Location) > 18*18)));
			}
		}

		[EncounterHandler(54544, "Echo of Tyrande", Mode = CallBehaviorMode.Proximity, BossRange = 250)]
		public Func<WoWUnit, Task<bool>> EchoofTyrandeEncounter()
		{
			const int stardust = 102173;

			const uint moonLanceRoot = 54574;
			//var moonLanceIds = new uint[] { 54580, 54581, 54582 };

			// boss has a 15 yd radius cast speed slow aura
			AddAvoidObject(ctx => Me.IsRange() && !RunFromEyesOfElune, 15, o => o.Entry == EchoOfTyrandeId && o.ToUnit().Combat);

			WoWUnit poolOfMoonlight = null;
			return async boss =>
			{
				_tyrande = boss;
				// out of combat behavior
				if (!_tyrande.Combat)
				{
					bool bossIsActive = !_tyrande.HasAura("In Shadow");
					if (!bossIsActive)
					{
						poolOfMoonlight = ObjectManager.GetObjectsOfType<WoWUnit>()
							.FirstOrDefault(u => u.Entry == PoolOfMoonlight);

						var tank = ScriptHelpers.Tank;
						_doingMoonWalk = poolOfMoonlight != null && tank != null 
							&& tank.Location.DistanceSqr(poolOfMoonlight.Location) > 5 * 5;
					}
					if (!bossIsActive && poolOfMoonlight != null && StyxWoW.Me.IsTank())
					{
						if (!Navigator.AtLocation(poolOfMoonlight.Location))
						{
							ScriptHelpers.SetLeaderMoveToPoi(poolOfMoonlight.Location);
						} 
						else if (Targeting.Instance.IsEmpty() )
						{
							return true;
						}
						return false;
					}
					if (poolOfMoonlight == null)
					{
						ScriptHelpers.SetLeaderMoveToPoi(_tyrande.Location);
						return false;
					}
					return false;
				}
				// combat behavior

				var movePoint = Me.Location;
				if (RunFromEyesOfElune)
				{
					movePoint = WoWMathHelper.CalculatePointFrom(movePoint, _tyrande.Location, 11);
				}

				var lance = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == moonLanceRoot);

				if (lance != null)
				{
					var targetLoc = lance.Location;
					var moonlanceDirection = WoWMathHelper.CalculateNeededFacing(_tyrande.Location, targetLoc);
					if (WoWMathHelper.IsFacing(targetLoc, moonlanceDirection, StyxWoW.Me.Location, WoWMathHelper.DegreesToRadians(35)))
					{
						float newAngle = StyxWoW.Me.Location.IsPointLeftOfLine(_tyrande.Location, targetLoc) ? WoWMathHelper.NormalizeRadian(moonlanceDirection + WoWMathHelper.DegreesToRadians(35)) : WoWMathHelper.NormalizeRadian(moonlanceDirection - WoWMathHelper.DegreesToRadians(35));
						movePoint = _tyrande.Location.RayCast(newAngle, movePoint.Distance(_tyrande.Location));
					}
				}

				if (Me.Location.DistanceSqr(movePoint) > 3*3)
				{
					Navigator.PlayerMover.MoveTowards(movePoint);
					return true;
				}

				return await ScriptHelpers.SpreadOut(_tyrande.Location, 8, 20) 
					|| await ScriptHelpers.InterruptCast(_tyrande, stardust);
			};
		}

		#endregion

		#region Murozond
		const uint MurozondsTemporalCacheId = 209547;

		[EncounterHandler(54432, "Murozond", Mode = CallBehaviorMode.CurrentBoss, BossRange = 250)]
		public Func<WoWUnit, Task<bool>> MurozondSpawnLogic()
		{
			return async boss =>
			{
				if (!_murozondArea.IsPointInPoly(Me.Location))
					return false;

				// wait for Murozond to spawn after a wipe.
				if (boss == null && Me.IsTank() &&
					!ScriptHelpers.GetUnfriendlyNpsAtLocation(Me.Location, 200).Any())
				{
					if (!MurozondSpawnTimer.IsRunning)
						MurozondSpawnTimer.Start();
					if (MurozondSpawnTimer.Elapsed >= TimeSpan.FromMinutes(2)
						|| ObjectManager.ObjectList.Any(o => o.Entry == MurozondsTemporalCacheId))
					{
						ScriptHelpers.MarkBossAsDead("Murozond");
						return false;
					}
					TreeRoot.StatusText = "Waiting for Murozond to spawn";					
					return true;
				}

				if (await ScriptHelpers.PullNpcToLocation(
					() => boss != null && !boss.Combat && Me.IsTank() && boss.Distance2DSqr <= 30 * 30,
					boss,
					null,
					0))
				{
					return true;
				}
				return false;
			};
		}

		static readonly Stopwatch MurozondSpawnTimer = new Stopwatch();

		[EncounterHandler(54432, "Murozond",  BossRange = 250)]
		public Func<WoWUnit, Task<bool>> MurozondFightLogic()
		{
			const int distortionBomb = 101984;
			const uint distortionBombMissileId = 101983;

			AddAvoidObject(ctx => true, 9, distortionBomb);
			AddAvoidLocation(
				ctx => true, 
				9,
				o => ((WoWMissile)o).ImpactPosition, 
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == distortionBombMissileId));

			var heroismSpells = new[] {"Time Warp", "Heroism", "Bloodlust"};

			WoWGameObject hourglassOfTime = null;
			return async boss =>
			{
				var hourglassUsesLeft = Lua.GetReturnVal<int>(" return UnitPower('player', ALTERNATE_POWER_INDEX)", 0);

				_activateHourGlass = false;
				if (hourglassUsesLeft > 0)
				{
					hourglassOfTime = ObjectManager.GetObjectsOfType<WoWGameObject>()
						.FirstOrDefault(g => g.Entry == HourglassOfTime);

					// person that clicks the activator should be a ranged dps.
					var asignedHourglassActivator =
						ScriptHelpers.GetGroupMembersByRole(ScriptHelpers.GroupRoleFlags.RangedDps)
							.Where(p => p.IsAlive)
							.
							OrderBy(p => p.MaxHealth).FirstOrDefault() ?? // no ranged dps ? try healer
						ScriptHelpers.GetGroupMembersByRole(ScriptHelpers.GroupRoleFlags.Healer).FirstOrDefault(u => !u.HasAura(27827)) ??
						// if no ranged are alive then have a melee dps go click it.
						ScriptHelpers.GetGroupMembersByRole(ScriptHelpers.GroupRoleFlags.MeleeDps)
							.Where(p => p.IsAlive)
							.OrderBy(p => p.MaxHealth).FirstOrDefault();

					if (asignedHourglassActivator != null && asignedHourglassActivator.IsMe)
					{
						if (StyxWoW.Me.PartyMembers.Any(p => p.IsDead))
						{
							Logger.Write("we have a dead party member. interacting with hourglass");
							_activateHourGlass = true;
						}
						if (boss.HealthPercent / 16.66f < hourglassUsesLeft)
							_activateHourGlass = true;
					}
				}

				if (MurozondSpawnTimer.IsRunning)
					MurozondSpawnTimer.Reset();

				if (_activateHourGlass && hourglassOfTime != null)
				{
					if (hourglassOfTime.DistanceSqr > 20*20)
					{
						// there's no mesh under the hourglass so need to find one on the outside
						var loc = WoWMathHelper.CalculatePointFrom(Me.Location, hourglassOfTime.Location, 15);
						return (await CommonCoroutines.MoveTo(loc, hourglassOfTime.SafeName)).IsSuccessful();
					}

					Logger.Write("Interacting with Hourglass");
					hourglassOfTime.Interact();
					return true;
				}

				var heroismSpell = heroismSpells.FirstOrDefault(SpellManager.CanCast);
				if (heroismSpell != null)
				{
					SpellManager.Cast(heroismSpell);
					Logger.Write("Casting {0}", heroismSpell);
					return true;
				}
				return false;
			};
		}

		[ObjectHandler(209547, "Murozond's Temporal Cache", ObjectRange = 100)]
		public async Task<bool> MurozondsTemporalCacheHandler(WoWGameObject gObj)
		{
			// this chest spawns quite some distance from boss and is uaualy out 
			// of LootDistance range
			return await ScriptHelpers.LootChest(gObj);
		}

		#endregion

		private DungeonArea _myLastArea;
		private DungeonArea _lastEndArea;
		public override MoveResult MoveTo(WoWPoint location)
		{
			if (Me.CurrentMap.MapId != LfgDungeon.MapId)
				return base.MoveTo(location);

			if (!_useTransitTimer.IsFinished && StyxWoW.Me.IsTank())
				return MoveResult.Moved;
			var myLoc = StyxWoW.Me.Location;

			var myArea = GetAreaForLocation(myLoc);
			var endArea = GetAreaForLocation(location);

			if (myArea == null && Me.GroupInfo.LfgDungeonId > 0)
			{
				Logger.Write("[End Time] I don't know where I am... zoning outside");
				Lua.DoString("LFGTeleport(true)");
			}

			if (myArea != endArea && myArea != null && endArea != null)
			{
				if (myArea != _myLastArea || _lastEndArea != endArea)
				{
					_myLastArea = myArea;
					_lastEndArea = endArea;
					Logger.Write("Moving from {0} to {1}", GetAreaName(myArea), GetAreaName(endArea));
				}

				if (myArea == _baineArea && Me.GroupInfo.LfgDungeonId > 0) // no going through lava to get to portal...
				{
					// port out - bot will auto port back in. this is the only way to get to 1st area.
					Logger.Write("[End Time] Porting to entrance to avoid having to go through");
					Lua.DoString("LFGTeleport(true)");
				}

				var portalLoc = GetPortalLocationForArea(myArea);
				var portal =
					ObjectManager.GetObjectsOfType<WoWGameObject>()
					.FirstOrDefault(g => _timeTransitDevices.Contains(g.Entry) && g.Location.Distance2DSqr(portalLoc) < 100 * 100);

				if (portal == null)
					return Navigator.MoveTo(portalLoc);

				if (portal.DistanceSqr > 5 * 5)
					return Navigator.MoveTo(portal.Location);

				if (Me.IsMoving)
					WoWMovement.MoveStop();

				if (!GossipFrame.Instance.IsVisible)
				{
					portal.Interact();
				}
				else if (GossipFrame.Instance.GossipOptionEntries != null)
				{ 
					if (endArea == _startArea)
						GossipFrame.Instance.SelectGossipOption(0);
					else
						GossipFrame.Instance.SelectGossipOption(GossipFrame.Instance.GossipOptionEntries.Count - 1);
					_useTransitTimer.Reset();
				}

				return MoveResult.Moved;
			}
			return MoveResult.Failed;
		}		

		private string GetAreaName(DungeonArea area)
		{
			if (area == null)
				return "[null]";

			if (_startArea == area)
				return "Start Area";

			if (_jainaArea == area)
				return "Jaina Area";

			if (_sylvanasArea == area)
				return "Sylvanas Area";

			if (_baineArea == area)
				return "Baine Area";

			if (_tyrandeArea == area)
				return "Tyrande Area";

			if (_murozondArea == area)
				return "Murozond Area";
			return "Unknown Area";
		}

		private DungeonArea GetAreaForLocation(WoWPoint location)
		{
			if (_startArea.IsPointInPoly(location))
				return _startArea;

			if (_jainaArea.IsPointInPoly(location))
				return _jainaArea;

			if (_sylvanasArea.IsPointInPoly(location))
				return _sylvanasArea;

			if (_baineArea.IsPointInPoly(location))
				return _baineArea;

			if (_tyrandeArea.IsPointInPoly(location))
				return _tyrandeArea;

			if (_murozondArea.IsPointInPoly(location))
				return _murozondArea;

			return null;
		}

		private WoWPoint GetPortalLocationForArea(DungeonArea area)
		{
			if (area == _startArea)
				return _startPortalLoc;

			if (area == _jainaArea)
				return _jainaPortalLoc;

			if (area == _sylvanasArea)
				return _sylvanasPortalLoc;

			if (area == _baineArea)
				return _bainePortalLoc;

			if (area == _tyrandeArea)
				return _tyrandePortalLoc;

			if (area == _murozondArea)
				return _murozondPortalLoc;

			return WoWPoint.Zero;
		}

		#endregion
	}
}
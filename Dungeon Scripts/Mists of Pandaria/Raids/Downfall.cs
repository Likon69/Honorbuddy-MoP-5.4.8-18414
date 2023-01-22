using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Behaviors;
using Bots.DungeonBuddy.Helpers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class Downfall : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 725; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1242.479, 600.5036, 318.0787); }
		}

		#region Movement

		public override MoveResult MoveTo(WoWPoint location)
		{
			WoWPoint myLoc = Me.Location;

			return base.MoveTo(location);
		}

		#endregion

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					WoWUnit unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == CrawlerMineId && Me.IsMelee())
							return true;

						if (unit.Entry == DesecratedWeaponId && (Me.HasAura("Realm of Y'Shaarj") || Me.IsMelee()))
							return true;

						if (unit.Entry == EmpoweredDesecratedWeaponId && (unit.HealthPercent <= 1 || Me.IsMelee()))
							return true;

						if (unit.Entry == GarroshHellscreamId && unit.HasAura("Y'Shaarj's Protection"))
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (WoWObject obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == AmberId || unit.Entry == BloodId)
						outgoingunits.Add(unit);

					if (unit is WoWPlayer && (unit.HasAura("Touch of Y'Shaarj") || unit.HasAura("Empowered Touch of Y'Shaarj")))
						outgoingunits.Add(unit);

					if (unit.Entry == DesecratedWeaponId && Me.IsRange())
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (Targeting.TargetPriority priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.IsPlayer && (unit.HasAura("Touch of Y'Shaarj") || unit.HasAura("Empowered Touch of Y'Shaarj")))
					{
						priority.Score += 10000;
						continue;
					}

					switch (unit.Entry)
					{
						// these things explode when they reach fixated target. 
						case CrawlerMineId:
						case CrawlerMine2Id:
						case AmberId:
							priority.Score = 10000 - unit.Distance;
							break;
						// these spawn from the desecrate ability used by Garrosh
						case DesecratedWeaponId:
							priority.Score += 9500;
							break;
						case AutomatedShredderId:
							// trash from the Blackfuse encounter. DPS these when they're stunned and take additional damage.
							priority.Score += unit.HasAura("Death From Above") ? 9000 : -9000;
							break;
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

		#region Root Behavior
		
		// The bomb is no-longer being avoided but for the sake of trying to avoid any navigation 
		// problems especially when the floor gets covered with fire I'm not going to have bot move to them. 
		// These also cause players to take more damage so they're not that beneficial.
		private const uint AdrenalineBombSpellId = 147876;

		private const uint ResonatingAmberId = 72966;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Func<WoWUnit, Task<bool>> RootBehavior()
		{
			// trash before paragon of Klaxxi use this.
			AddAvoidObject(ctx => true, 8, ResonatingAmberId);

			AddAvoidObject(
				ctx => true,
				6,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == LaserGroundEffectSpellId;
				});

			return async npc => await ScriptHelpers.CancelCinematicIfPlaying();
		}

		#endregion

		#region Siegecrafter Blackfuse

		private const uint AutomatedShredderId = 71591;
		private const int DeathFromAboveMissileId = 147010;
		private const uint SawbladeId = 71532;
		private const uint ConveyorDeathBeamSpellId = 144282;
		private const uint CrawlerMineId = 74009;
		private const uint CrawlerMine2Id = 71788;
		private const uint ActivatedElectromagnetId = 71696;
		private const uint LaserTargetBunnyId = 71740;
		private const uint LaserGroundEffectSpellId = 143830;
		private const uint ShockwaveMissileId = 72052;

		private readonly WaitTimer _shockwaveMissileTimer = new WaitTimer(TimeSpan.FromSeconds(30));

		[EncounterHandler(71504, "Siegecrafter Blackfuse")]
		public Composite CreateBehavior_SiegecrafterBlackfuse()
		{
			WoWUnit boss = null;

			var roomCenter = new WoWPoint(1955.81, -5608.869, -309.3269);

			// avoid the sawblades. 
			AddAvoidObject(ctx => true, 5, o => o.Entry == SawbladeId && o.ToUnit().Transport != boss);

			// avoid the conveyor lasors and the laser ground effects left by the laser beam on the floor.... 
			AddAvoidObject(
				ctx => true,
				3,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == LaserGroundEffectSpellId;
				});

			AddAvoidLocation(
				ctx => true,
				15,
				m => ((WoWMissile)m).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == DeathFromAboveMissileId));

			AddAvoidObject(
				ctx => true,
				o => Me.IsRange() && Me.IsMoving ? 20 : 15,
				o => (o.Entry == CrawlerMineId || o.Entry == CrawlerMine2Id) && o.ToUnit().CurrentTargetGuid == Me.Guid);

			// Avoid the laser bunny
			AddAvoidObject(ctx => Me.HasAura("Locked On"), o => Me.IsRange() && Me.IsMoving ? 15 : 10, LaserTargetBunnyId);

			var magnetAvoidPoints = new List<object>();

			AddAvoidLocation(
				ctx => magnetAvoidPoints.Any() && ScriptHelpers.IsBossAlive("Siegecrafter Blackfuse"),
				7,
				o => (WoWPoint)o,
				() => magnetAvoidPoints);

			// avoid the shockwave missile waves.
			AddAvoidObject(
				ctx => true,
				10,
				o => o.Entry == ShockwaveMissileId,
				o =>
				{
					if (_shockwaveMissileTimer.IsFinished)
						_shockwaveMissileTimer.Reset();
					WoWPoint objLoc = o.Location;
					WoWPoint objToMeVector = Me.Location - objLoc;
					var objToMeRadians = (float)Math.Atan2(objToMeVector.Y, objToMeVector.X);
					double secondsPassed = _shockwaveMissileTimer.WaitTime.TotalSeconds - _shockwaveMissileTimer.TimeLeft.TotalSeconds;
					var step = (int)(secondsPassed / 4);
					// the rings spacings get wider the further from center.
					int dist = step * (15 + step);
					Logger.Write("Dist: {0}", dist);
					return objLoc.RayCast(objToMeRadians, dist);
				});

			return new PrioritySelector(
				ctx =>
				{
					// update magnet/sawblad avoid locations.
					List<WoWUnit> magnets = ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
						.Where(u => u.Entry == ActivatedElectromagnetId && u.HasAura("Magnetic Crush")).ToList();
					if (magnets.Any())
					{
						if (magnetAvoidPoints.Any())
							magnetAvoidPoints.Clear();

						List<WoWUnit> sawblades =
							ObjectManager.GetObjectsOfTypeFast<WoWUnit>().Where(u => u.Entry == SawbladeId && u.Transport != boss).ToList();
						if (sawblades.Any())
						{
							WoWPoint myLoc = Me.Location;
							foreach (WoWUnit magnet in magnets)
							{
								magnetAvoidPoints.AddRange(
									sawblades.Select(s => (object)myLoc.GetNearestPointOnSegment(s.Location, magnet.Location)));
							}
						}
					}
					else if (magnetAvoidPoints.Any())
					{
						magnetAvoidPoints.Clear();
					}
					return boss = ctx as WoWUnit;
				},
				// stay away from edge of platform to prevent getting pulled off by magnet.
				new Decorator(
					ctx => Me.Location.DistanceSqr(roomCenter) > 55 * 55,
					new Sequence(
						new ActionLogger("Moving towards platform center"),
						ScriptHelpers.CreateMoveToContinue(ctx => Me.Location.DistanceSqr(roomCenter) > 45 * 45, ctx => roomCenter, true)))
				);
		}

		#endregion

		#region Paragons of the Klaxxi

		private const uint SkeerTheBloodseekerId = 71152;
		private const uint HisekTheSwarmkeeperId = 71153;
		private const uint KarozTheLocustId = 71154;
		private const uint KorvenThePrimeId = 71155;
		private const uint KaztikTheManipulatorId = 71156;
		private const uint XarilThePoisonedMindId = 71157;
		private const uint RikkalTheDissectorId = 71158;
		private const uint IyyokukTheLucidId = 71160;
		private const uint KilrukTheWindReaverId = 71161;
		private const int MutateSpellId = 143337;
		const uint KovokId = 72927;

		private const uint SonicProjectionSpellId = 143765;

		private const uint AmberId = 71407;
		private const uint BloodId = 71542;
		private const uint KorthikHonorGuardId = 72954;
		private const uint ReactionYellowSpellId = 142737;


		private readonly uint[] _KlaxxiNpcWithSpecialAbilities_DPS =
		{
			// attacks spawn a blood orb
			SkeerTheBloodseekerId, 
			// mutates into a scorpion
			RikkalTheDissectorId, 

			// ranged attack with damage taken increase debuff on target.
			// HisekTheSwarmkeeperId, 

			// jumps up to a ledge and fling amber at NPCs creating an explosion. Used to destroy the ember summoned by Korven
			//KarozTheLocustId,

			// summons a pet to fight for the player
			KaztikTheManipulatorId, 

			// leaps forward a fixed distance and 10y aoe damage to enemies at impact location. used to disable Thick Shell buff on a 'Hungry Kunchong'
			//KilrukTheWindReaverId
		};

		private readonly uint[] _KlaxxiNpcWithSpecialAbilities_HEAL =
		{
			// heals casted on players are copied to other members of raid that share same race or class.
			IyyokukTheLucidId, 
			// healed targets get a buff that stores amount healed and heal for the stored amount the next time they take damage
			XarilThePoisonedMindId,
			// summons a pet to fight for the player
			KaztikTheManipulatorId
		};

		private readonly string[] _specialKlaxxiPowers =
		{
			// Mutate Scorpion
			"Gene Splice", 
			// Volatile Poultice
			"Vast Apothecarial Knowledge",
			// Ingenious
			"Calculator",
			"Master of Puppets"
		};


		[LocationHandler(x: 1582.5f, y: -5686.611f, z: -314.639f, radius: 60, locationDisplayName: "Paragons of the Klaxxi Area")]
		public Composite CreateBehavior_ParagonsOfTheKlaxxi()
		{

			AddAvoidObject(
				ctx => true,
				6,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == SonicProjectionSpellId;
				},
				o =>
				{
					WoWPoint objLoc = o.Location;
					return Me.Location.GetNearestPointOnSegment(objLoc, objLoc.RayCast(o.Rotation, 10));
				});

			// poisonous pools on the ground.
			AddAvoidObject(
				ctx => true,
				6,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == ReactionYellowSpellId;
				});

			// avoid standing in front of these NPCs since they do a frontal frenzy attack or cleave
			AddAvoidObject(
				ctx => true,
				7,
				o => (o.Entry == KorvenThePrimeId || o.Entry == KovokId) && o.ToUnit().Combat,
				o => o.Location.RayCast(o.Rotation, 6));

			AddAvoidObject(
				ctx => true,
				6,
				o => o.Entry == KorthikHonorGuardId && o.ToUnit().Combat,
				o => o.Location.RayCast(o.Rotation, 6));


			return new PrioritySelector(
				//  handle the aim ability
				CreateBehavior_HandleAim(),
				// handle mutated scopion ability from Rik'kal the Dissector
				CreateBehavior_MutatedScorpion(),
				// remove the 'Encase in Ember' ability if has 'Strong Legs' buff.
				CreateBehavior_EncaseInArmor(),
				CreateBehavior_HandleReave(),
				CreateBehavior_HandleBloodletting(),
				// healer special power.
				CreateBehavior_VolatilePoultise(),
				new ActionRunCoroutine(ctx => HandleIngenious()),
				CreateBehavior_MasterOfPuppets(),

				// interact with a defeated boss to gain one of it's abilites.
				new PrioritySelector(
					ctx => GetDeadKlaxxiWithPowers(),
					new Decorator<WoWUnit>(npc => npc.DistanceSqr > 4 * 4, new Helpers.Action<WoWUnit>(npc => Navigator.MoveTo(npc.Location))),
					new Decorator<WoWUnit>(ctx => ctx != null && Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
					new Helpers.Action<WoWUnit>(npc => npc.Interact()))
				);
		}

		private WoWUnit GetDeadKlaxxiWithPowers()
		{
			// return null if I already have one of the special klaxxi powers.
			if (Me.GetAllAuras().Select(a => a.Name).ContainsAny(_specialKlaxxiPowers))
				return null;

			bool isHealer = Me.IsHealer();

			return ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
				.Where(
					u =>
						(isHealer
							? _KlaxxiNpcWithSpecialAbilities_HEAL.Contains(u.Entry)
							: _KlaxxiNpcWithSpecialAbilities_DPS.Contains(u.Entry)) && u.HasAura("CLICK ME") && !u.HasAura("Been Clicked"))
				.OrderBy(u => u.DistanceSqr)
				.FirstOrDefault();
		}

		// Hisek does a powerful attack that does massive amounts of damage to a player and it's shared with anyone between him and player
		// So to reduce the damage his target takes we need to move between him and target
		private Composite CreateBehavior_HandleAim()
		{
			const int aimSpellId = 142948;
			return new Decorator(ctx => Me.IsRange(),
						new PrioritySelector(
						ctx =>
						{
							GroupMember groupMemberWithAim = ScriptHelpers.GroupMembers.FirstOrDefault(
								g => g.Player != null && g.Player.HasAura(aimSpellId));
							if (groupMemberWithAim != null)
							{
								var hisek = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == HisekTheSwarmkeeperId) as WoWUnit;
								if (hisek != null)
								{
									return Me.Location.GetNearestPointOnSegment(hisek.Location.RayCast(hisek.Rotation, 7), groupMemberWithAim.Location);
								}
							}
							return WoWPoint.Zero;
						},
						new Decorator<WoWPoint>(
							moveto => moveto != WoWPoint.Zero && moveto.DistanceSqr(Me.Location) > 4 * 4,
							new Helpers.Action<WoWPoint>(moveto => Navigator.MoveTo(moveto)))));
		}

		private Composite CreateBehavior_MutatedScorpion()
		{
			const int madScientistId = 141857;

			// to-do finish this..
			return new Decorator(
				ctx => Me.HasAura("Gene Splice") || Me.HasAura(MutateSpellId),
				new PrioritySelector(
					new Decorator(
						ctx => Me.HasAura(madScientistId) && SpellActionButton.ExtraActionButton.CanUse,
						new Action(ctx => SpellActionButton.ExtraActionButton.Use())),
					new Decorator(
						ctx =>
						{
							SpellActionButton firstButton = SpellActionButton.OverrideActionBarButtons.FirstOrDefault();
							return firstButton != null && firstButton.IsEnabled;
						},
						new PrioritySelector(
							ctx => Me.CurrentTarget,
				// get within melee range
							new Decorator<WoWUnit>(
								target => !WoWMovement.ActiveMover.IsWithinMeleeRangeOf(target),
								new Helpers.Action<WoWUnit>(target => Navigator.MoveTo(target.Location))),
				// face target
							new Decorator<WoWUnit>(
								target => !WoWMovement.ActiveMover.IsSafelyFacing(target),
								new Helpers.Action<WoWUnit>(target => target.Face())),
				// stop moving.
							new Decorator(ctx => WoWMovement.ActiveMover.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
				// use best available ability. behavior should stop here.
							new Action(
								ctx =>
								{
									SpellActionButton ability =
										SpellActionButton.OverrideActionBarButtons.OrderByDescending(b => b.Slot).FirstOrDefault(b => b.CanUse);
									if (ability != null)
									{
										ability.Use();
									}
								})
							))));
		}

		private Composite CreateBehavior_EncaseInArmor()
		{
			const int strongLegsExtrButtonSpellId = 143964;
			WoWUnit target = null;

			// We save the 'Strong Legs' ability for when a Encase in Amber shield needs to be removed.
			return new Decorator(
				ctx => Me.HasAura("Strong Legs"),
				new PrioritySelector(
					ctx => target = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.HasAura("Encase in Amber")),
					new Decorator(
						ctx => ctx != null,
						new PrioritySelector(
							ctx => SpellActionButton.ExtraActionButton,
				// use strong legs to get up to a ledge.
							new Decorator<SpellActionButton>(
								spell => spell.SpellId == strongLegsExtrButtonSpellId && spell.CanUse,
								new Helpers.Action<SpellActionButton>(spell => spell.Use())),
				// throw amber at target
							new Decorator<SpellActionButton>(
								spell => spell.SpellId != strongLegsExtrButtonSpellId && spell.CanUse,
								new Sequence(
									new Helpers.Action<SpellActionButton>(spell => spell.Use()),
									new Sleep(1000),
									new Action(ctx => SpellManager.ClickRemoteLocation(target.Location))))
							))));
		}

		private Composite CreateBehavior_HandleReave()
		{
			bool ranOnce = false;
			// to-do finish this..
			return new Decorator(
				ctx => Me.HasAura("Reave"),
				new PrioritySelector(
				// todo: figure out the ID of the Hungry Kunchong
					ctx => ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.HasAura("Thick Shell")),
				// TODO: remove this once behavior works..
					new Action(
						ctx =>
						{
							if (!ranOnce)
							{
								Logger.Write("[Thick Shell] Dumping spells for debug purposes");
								SpellActionButton extraButton = SpellActionButton.ExtraActionButton;
								if (extraButton != null)
								{
									Logger.Write(
										"[Extra Action button] {0} - Id: {1}, Slot: {1}",
										extraButton.Spell.Name,
										extraButton.SpellId,
										extraButton.Slot);
								}
								ranOnce = true;
							}
							return RunStatus.Failure;
						})
					));
		}

		private Composite CreateBehavior_HandleBloodletting()
		{
			return new Decorator(
				ctx => Me.HasAura("Bloodletting"),
				new Action(
					ctx =>
					{
						SpellActionButton extraActionButton = SpellActionButton.ExtraActionButton;
						if (extraActionButton != null && extraActionButton.CanUse)
						{
							extraActionButton.Use();
						}
						return RunStatus.Failure;
					}));
		}

		private Composite CreateBehavior_VolatilePoultise()
		{
			return new Decorator(
				ctx => Me.HasAura("Vast Apothecarial Knowledge") && SpellActionButton.ExtraActionButton.IsEnabled,
				new Action(
					ctx =>
					{
						SpellActionButton extraActionButton = SpellActionButton.ExtraActionButton;
						if (extraActionButton.CanUse)
						{
							extraActionButton.Use();
							return RunStatus.Success;
						}
						return RunStatus.Failure;
					}));
		}

		private async Task<bool> HandleIngenious()
		{
			var button = SpellActionButton.ExtraActionButton;
			if (button == null || !Me.HasAura("Calculator") || !button.CanUse)
				return false;

			var healTarget = HealTargeting.Instance.FirstUnit;
			if (healTarget == null || healTarget.DistanceSqr >= 40 * 40)
				return false;

			if (Me.CurrentTarget != healTarget)
			{
				healTarget.Target();
				if (!await Coroutine.Wait(4000, () => healTarget.IsValid && Me.CurrentTarget == healTarget))
					return false;
			}
			button.Use();
			return true;
		}

		private Composite CreateBehavior_MasterOfPuppets()
		{
			return new Decorator(
				ctx => Me.HasAura("Master of Puppets"),
				new Action(
					ctx =>
					{
						SpellActionButton extraActionButton = SpellActionButton.ExtraActionButton;
						if (extraActionButton.CanUse)
						{
							extraActionButton.Use();
							return RunStatus.Success;
						}
						return RunStatus.Failure;
					}));
		}

		#endregion

		#region Garrosh Hellscream

		private const int WhirlingCorruptionSpellId = 144985;
		private const int EmpoweredWhirlingCorruptionSpellId = 145037;

		private const uint EmpoweredDesecratedWeaponId = 72198;
		private const uint DesecratedWeaponSpellVisualId = 32785;
		private const uint DesecratedWeaponId = 72154;
		private const uint DesecrateMissileId = 144748;
		private const uint KokronIronStarId = 71985;
		private const uint GarroshHellscreamId = 71865;
		private const uint AnnihilateId = 73625;

		private readonly ScriptHelpers.AngleSpan _annihilateAvoidAngleSpan =
			new ScriptHelpers.AngleSpan((int)WoWMathHelper.RadiansToDegrees(0), 90);

		private readonly int[] _empoweredWhirlingCorruptionMissileIds = { 145023, 144994 };

		private readonly WaitTimer _updateRangedGroupCenterPos = new WaitTimer(TimeSpan.FromSeconds(1));
		private WoWPoint _rangedGroupCenter;

		[EncounterHandler(71865, "Garrosh Hellscream")]
		public Composite CreateBehavior_GarroshHellscream()
		{
			WaitTimer phase2TransitionTimer = WaitTimer.FiveSeconds;
			// the avoid radius goes down as health is reduced.
			AddAvoidObject(
				ctx => Me.IsRange(),
				o =>
				{
					double hp = o.ToUnit().HealthPercent;
					return hp <= 1 ? 3 : (14f * ((float)hp) / 100f) + 11;
				},
				o => o.Entry == DesecratedWeaponId || o.Entry == EmpoweredDesecratedWeaponId);

			AddAvoidLocation(
				ctx => Me.IsRange(),
				25,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == DesecrateMissileId));

			// sidestep the Iron Star.
			AddAvoidObject(
				ctx => true,
				30,
				o => o.Entry == KokronIronStarId && o.ToUnit().HasAura("Iron Star Impact"),
				o =>
				{
					WoWPoint objLoc = o.Location;
					return Me.Location.GetNearestPointOnSegment(objLoc, objLoc.RayCast(o.Rotation, 200));
				});

			AddAvoidObject(
				ctx => Me.IsRange(),
				15,
				o =>
				{
					if (o.Entry == GarroshHellscreamId)
					{
						int castingSpellId = o.ToUnit().CastingSpellId;
						return castingSpellId == WhirlingCorruptionSpellId || castingSpellId == EmpoweredWhirlingCorruptionSpellId;
					}
					return false;
				});

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Phase 1 and 3
				new Decorator(
					ctx => !boss.HasAura("Realm of Y'Shaarj"),
					new PrioritySelector(
				// ranged classes should no wander too far from raid. (not a problem for melee)
						new Decorator(
							ctx => Me.IsRange() && phase2TransitionTimer.IsFinished,
							new PrioritySelector(
								ScriptHelpers.CreateWaitAtLocationWhile(
									ctx => ShouldStackUp(boss),
									ctx => GetRangedGroupCenter(),
									"Range Stack Location",
									7,
									ScriptHelpers.GroupRoleFlags.Ranged))))),
				// Phase 2
				new Decorator(
					ctx => boss.HasAura("Realm of Y'Shaarj") && !boss.HasAura("Y'Shaarj's Protection"),
					new PrioritySelector(
						ctx => GetMoveOutOfAnnihilatePoint(boss),
						new Action(
							ctx =>
							{
								phase2TransitionTimer.Reset();
								return RunStatus.Failure;
							}),
				// move close enough to the boss in phase 2 so annihilate can be easily avoided.
						new Decorator(
							ctx => boss.DistanceSqr > 20 * 20,
							new Action(ctx => Navigator.MoveTo(boss.Location))),
				// avoid Garrosh's frontal area in phase 2..
						new Decorator<WoWPoint>(
							moveto => moveto != WoWPoint.Zero && !Navigator.AtLocation(moveto),
							new Helpers.Action<WoWPoint>(moveto => Navigator.MoveTo(moveto))))));
		}

		bool ShouldStackUp(WoWUnit boss)
		{
			if (boss == null || !boss.IsValid)
				return false;

			var rangedGroupLoc = GetRangedGroupCenter();
			return !boss.HasAura("Realm of Y'Shaarj") && !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(rangedGroupLoc));
		}


		// returns a point outside of Garrash's Annihilate ability cone area if it's being used.
		// We don't want to move unless necessary and Garash slowly turns when chain casting annihilate 
		// so it's best not to avoid his current 'facing' angle but to avoid the angle from his location to the 'annihilate' dummy npc
		private WoWPoint GetMoveOutOfAnnihilatePoint(WoWUnit garrash)
		{
			WoWUnit annhilate = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == AnnihilateId);
			if (annhilate == null)
				return WoWPoint.Zero;
			WoWPoint garrashLoc = garrash.Location;

			WoWPoint garrashToAnnhilateLoc = annhilate.Location - garrashLoc;
			var rotation = (float)Math.Atan2(garrashToAnnhilateLoc.Y, garrashToAnnhilateLoc.X);


			WoWPoint myLoc = Me.Location;

			if (!ScriptHelpers.IsLocationInAngleSpans(myLoc, garrashLoc, rotation, _annihilateAvoidAngleSpan))
				return WoWPoint.Zero;

			WoWPoint pointOutisideAngles;
			return ScriptHelpers.GetPointOutsideAngleSpans(
				myLoc,
				garrashLoc,
				rotation,
				35,
				out pointOutisideAngles,
				_annihilateAvoidAngleSpan)
				? pointOutisideAngles
				: WoWPoint.Zero;
		}

		private WoWPoint GetRangedGroupCenter()
		{
			if (_rangedGroupCenter == WoWPoint.Zero || _updateRangedGroupCenterPos.IsFinished)
			{
				var loc = ScriptHelpers.GetGroupCenterLocation(g => g.IsRange, 20);
				if (loc.DistanceSqr(_rangedGroupCenter) > 3*3)
					_rangedGroupCenter = loc;
				_updateRangedGroupCenterPos.Reset();
			}
			return _rangedGroupCenter;
		}

		#endregion
	}
}
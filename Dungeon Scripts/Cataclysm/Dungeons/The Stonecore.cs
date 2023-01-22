using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Enums;
using Bots.DungeonBuddy.Profiles.Handlers;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot.Routines;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Styx.CommonBot.Coroutines;

namespace Bots.DungeonBuddy.Dungeons.Cataclysm
{
	public class Stonecore : Dungeon
	{
		#region Overrides of Dungeon

		private readonly CircularQueue<WoWPoint> _corpseRun = new CircularQueue<WoWPoint>
															  {
																  new WoWPoint(934.2088, 638.4651, 171.1208),
																  new WoWPoint(1024.311, 643.19, 162.269),
																  new WoWPoint(1032.611f, 607.3205f, 160.9259f)
															  };

		/// <summary>
		///     The Map Id of this dungeon. This is the unique id for dungeons thats used to determine which dungeon, the script
		///     belongs to
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId
		{
			get { return 307; }
		}


		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1032.611f, 607.3205f, 160.9259f); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(823.1542, 985.9727, 318.7188); }
		}

		public override bool IsFlyingCorpseRun
		{
			get { return true; }
		}

		public override CircularQueue<WoWPoint> CorpseRunBreadCrumb
		{
			get { return _corpseRun; }
		}

		public override void OnEnter()
		{
			_slabhideTimer = null;
		}

		/// <summary>
		///     IncludeTargetsFilter is used to add units to the targeting list. If you want to include a mob thats usually removed
		///     by the default filters, you shall use that.
		/// </summary>
		/// <param name="incomingunits"> Units passed into the method </param>
		/// <param name="outgoingunits"> Units passed to the targeting class </param>
		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (WoWObject obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit == null)
					continue;
				if (unit.Entry == MillhouseManastormId && Me.IsTank() && !Me.Combat && unit.Distance <= 60)
					outgoingunits.Add(unit);

				if (unit.Entry == StonecoreSentryId && unit.DistanceSqr <= 25 * 25) // Stonecore Sentry
					outgoingunits.Add(unit);

				if (unit.Entry == 42428 && Me.IsTank() && (unit.DistanceSqr <= 40 * 40 || unit.Combat)) // devout followers
					outgoingunits.Add(unit);
			}
		}

		/// <summary>
		///     RemoveTargetsFilter is used to remove units thats not wanted in target list. Like immune mobs etc.
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

					// for Corborus when he submerges
					if (unit.Entry == CorborusId && unit.HasAura("Submerge"))
						return true;

					if (unit.Entry == RockBorerId)
					{
						if (_rockBorerIgnoreTimer.IsFinished)
						{
							_rockBorerIgnoreTimer.Reset();
							_rockBorerKillTimer.Reset();
						}
						if (!_rockBorerKillTimer.IsFinished && Me.IsTank())
						{
							return true;
						}
					}

					// For Ozruk fight. Ranged dps should stop dpsing if the boss has spell reflect buff
					//if (unit.Entry == OzrukId && unit.HasAura("Elementium Bulwark") && Me.IsRange() && Me.IsDps())
					//    return true;

					// For Priestess fight. It becomes immune with this shield on
					if (unit.Entry == HighPriestessAzilId && unit.HasAura("Energy Shield"))
						return true;

					if (unit.Entry == 49857 && !unit.Aggro) // Emerald Shale Hatchling - level 1 (criter?)
						return true;

					return false;
				});
		}

		/// <summary>
		///     WeighTargetsFilter is used to weight units in the targeting list. If you want to give priority to a certain npc,
		///     you should use this method.
		/// </summary>
		/// <param name="units"> </param>
		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var t in units)
			{
				var prioObject = t.Object;
				var unit = prioObject as WoWUnit;
				if (unit == null)
					continue;
				// Stonecore Sentry. kill it before it runs for help.
				if (unit.Entry == StonecoreSentryId)
					t.Score += 5000;

				// Devout Follower. try to pick up aggro.
				if (unit.Entry == DevoutfollowerId && StyxWoW.Me.IsTank() && unit.Combat && unit.IsTargetingMyPartyMember)
					t.Score += 5000;

				if (unit.Entry == RiftConjurerId && StyxWoW.Me.IsDps()) // Rift Conjurer.
					t.Score += 4000;

				if (unit.Entry == ImpId && StyxWoW.Me.IsDps()) // Imp
					t.Score += 4100;

				// kill these last.
				if (unit.Entry == RockBorerId)
					t.Score -= 4000;
			}
		}

        public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingObjects)
        {
            foreach (var obj in incomingunits)
            {
                var gObj = obj as WoWGameObject;
                if (gObj != null)
                {
                    if (ScriptHelpers.SupportsQuesting && TwilightDocumentsId == obj.Entry
                        && gObj.DistanceSqr < 35 * 35 && gObj.ZDiff < 10
                        && !ScriptHelpers.WillPullAggroAtLocation(gObj.Location) && gObj.CanUse())
                    {
                        outgoingObjects.Add(obj);
                    }
                }
            }
        }

		#endregion

		[EncounterHandler(50048, "Earthwarden Yrsa", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		#region Encounter Handlers

		private const uint StonecoreBruiserId = 42692;
		private const uint StonecoreSentryId = 42695;
		private const uint RiftConjurerId = 42691;
		private const uint ImpId = 43014;
		private const uint StalactiteId = 204337;
		private const uint StalactiteSpellId = 80654;
		private const uint TwilightDocumentsQuestId = 28815;
		private const uint MillhouseManastormId = 43391;
		private readonly WaitTimer _rockBorerIgnoreTimer = new WaitTimer(TimeSpan.FromSeconds(20));
		private readonly WaitTimer _rockBorerKillTimer = new WaitTimer(TimeSpan.FromSeconds(10));
		private readonly WaitTimer _teleportTimer = new WaitTimer(TimeSpan.FromSeconds(10));
		private WoWUnit _ozruk;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "RootBehavior")]
		public Composite RootBehavior()
		{
			AddAvoidObject(ctx => true, 2, u => (u.Entry == StalactiteId || u.Entry == StalactiteSpellId) && u.DistanceSqr <= 100);
			return
				new PrioritySelector(
					new Decorator(
						ctx => Me.QuestLog.ContainsQuest(TwilightDocumentsQuestId) && Me.QuestLog.GetQuestById(TwilightDocumentsQuestId).IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(TwilightDocumentsQuestId)),
					
						// move tank towards current boss to lead group away from fast spawning Rock Borers
						new Decorator(ctx => !_rockBorerKillTimer.IsFinished && Me.IsTank() && Me.Combat && Targeting.Instance.IsEmpty(),
							new PrioritySelector(ctx => BossManager.CurrentBoss,
								new Helpers.Action<Boss>(boss => Navigator.MoveTo(boss.Location))))
						);
		}

		[EncounterHandler(42808, "Stonecore Flayer")]
		public Composite StonecoreFlayerEncounter()
		{
			return
				new PrioritySelector(
					ScriptHelpers.CreateAvoidUnitAnglesBehavior(
						ctx => ((WoWUnit)ctx).HasAura("Flay") && ((WoWUnit)ctx).Distance < 9,
						ctx => (WoWUnit)ctx,
						new ScriptHelpers.AngleSpan(0, 120)));
		}

	    private const uint TwilightDocumentsId = 207415;

		[EncounterHandler(42692, " Stonecore Bruiser")]
		public Composite StonecoreBruiserEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && unit.CurrentTargetGuid != Me.Guid && !unit.IsMoving && unit.Distance < 20,
					ctx => unit,
					new ScriptHelpers.AngleSpan(0, 100)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(20));
		}

		public override async Task<bool> HandleMovement(WoWPoint location)
		{
			// wait 2 seconds after porting and clear navigation path.
			if (!_teleportTimer.IsFinished && _teleportTimer.TimeLeft > TimeSpan.FromSeconds(8))
			{
				Navigator.NavigationProvider.Clear();
				return true;
			}
			// use entrance portal.
			if ((StyxWoW.Me.X < 900 && location.X > 1200 || StyxWoW.Me.Y > 1200 && location.Y < 900) && _teleportTimer.IsFinished)
			{
				var teleporter = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(o => (o.Entry == 51396 || o.Entry == 51397) && o.HasAura("Teleporter Active Visual"));
				if (teleporter != null)
				{
					if (!teleporter.WithinInteractRange)
						return (await CommonCoroutines.MoveTo(teleporter.Location, teleporter.SafeName)).IsSuccessful();
					teleporter.Interact();
					await CommonCoroutines.SleepForLagDuration();
					_teleportTimer.Reset();
					return true;
				}
			}
			return false;
		}

		#region Slabhide

		private const uint RockBorerId = 42845;
		private WaitTimer _slabhideTimer;

		[EncounterHandler(43214, "Slabhide", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite SlabhideWaitForLandBehavior()
		{
			WoWUnit boss = null;
			var landingLoc = new WoWPoint(1280.065, 1227.754, 247.0767);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => StyxWoW.Me.IsTank() && Targeting.Instance.IsEmpty() && StyxWoW.Me.Location.Distance(landingLoc) < 35,
					new PrioritySelector(
						new Decorator(
							ctx => _slabhideTimer == null,
							new Action(
								ctx =>
								{
									_slabhideTimer = new WaitTimer(TimeSpan.FromSeconds(14));
									_slabhideTimer.Reset();
								})),
				// wait for Slabhide to land his fat ass.
						new Decorator(ctx => boss != null && !boss.Combat && boss.Z > 255 || !_slabhideTimer.IsFinished, 
							new PrioritySelector(
								new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							new ActionAlwaysSucceed())))));
		}

		[EncounterHandler(43214, "Slabhide")]
		public Composite SlabhideFight()
		{
			WoWUnit boss = null;
			const uint lavaFissureId = 43242;

			AddAvoidObject(ctx => true, 5, lavaFissureId);

			return new PrioritySelector(
				ctx =>
				{
					boss = (WoWUnit)ctx;
					return boss;
				},
				new Decorator(
					ctx => Math.Abs(boss.Z - StyxWoW.Me.Z) < 7,
					new PrioritySelector(
				// only tank should be in front when boss is not flying to avoid the Sand Blast abiliy
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(
							ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && boss.Distance < 15 && !boss.IsMoving,
							ctx => boss,
							new ScriptHelpers.AngleSpan(0, 70)),
						ScriptHelpers.CreateTankFaceAwayGroupUnit(15),
						ScriptHelpers.GetBehindUnit(ctx => !StyxWoW.Me.IsTank() && StyxWoW.Me.IsMelee() && boss.IsSafelyFacing(StyxWoW.Me, 65), ctx => boss))));
		}

		#endregion

		#region Ozruk

		private const uint OzrukId = 42188;

		[LocationHandler(1532.957, 1163.717, 217.889, 50, "West Side")]
		[LocationHandler(1538.305, 1122.397, 216.3325, 50, "East Side")]

		public Func<WoWPoint, Task<bool>> CamberOfFanaticsHandler()
		{
			return async point =>
			{
				var trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(point, 50).FirstOrDefault();
				return await ScriptHelpers.PullNpcToLocation(() => trash != null && ScriptHelpers.IsViable(trash), trash, Me.Location,0);
			};
		}

		[EncounterHandler(42188, "Ozruk", Mode = CallBehaviorMode.Proximity)]
		public Composite OzrukFight()
		{
			var ozrukArea = new WoWPoint(1507.859,1073.26,217.2513);
			AddAvoidObject(ctx => true, 15, o => o.Entry == OzrukId && IsCastingShatter(o.ToUnit()));
			// run away from ground slam impact location.
			AddAvoidObject(
				ctx => true,
				6,
				o => o.Entry == OzrukId && IsCastingGroundSlam(o.ToUnit()),
				o => WoWMathHelper.GetPointAt(o.Location, 6, WoWMathHelper.NormalizeRadian(o.Rotation), 0));

			return new PrioritySelector(
				ctx => _ozruk = ctx as WoWUnit,
				ScriptHelpers.CreateClearArea(() => ozrukArea, 60, u => u != _ozruk),
				ScriptHelpers.CreateDispelEnemy("Elementium Bulward", ScriptHelpers.EnemyDispelType.Magic, ctx => _ozruk));
		}

		private bool IsCastingGroundSlam(WoWUnit unit)
		{
			return unit != null && (unit.CastingSpellId == 78903 || unit.CastingSpellId == 92410);
		}

		private bool IsCastingShatter(WoWUnit unit)
		{
			return unit != null && (unit.CastingSpellId == 78807 || unit.CastingSpellId == 92662);
		}

		#endregion

		#region High Priestess Azil

		private const uint DevoutfollowerId = 42428;
		private const uint HighPriestessAzilId = 42333;

		[EncounterHandler(42333, "High Priestess Azil", Mode = CallBehaviorMode.Proximity, BossRange = 75)]
		public Composite HighPriestessAzilFight()
		{
			List<WoWUnit> trash = null;
			List<WoWUnit> gravityWells = null, aggro = null;
			WoWUnit boss = null;

			var trashLoc = new WoWPoint(1327.616, 990.7188, 207.9859);
			var trashTankLoc = new WoWPoint(1292.835, 990.944, 207.9722);
			var trashWaitLoc = new WoWPoint(1292.202, 1013.337, 209.1433);
			bool hasAggroInMelee = false;

			const uint seismicShardTargetingId = 80511;
			const uint gravityWellId = 42499;

			AddAvoidObject(ctx => true, 5, o => o.Entry == HighPriestessAzilId && o.ToUnit().HasAura("Energy Shield"));
			AddAvoidObject(ctx => true, 11, gravityWellId);
			AddAvoidObject(ctx => true, 3, seismicShardTargetingId);

			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					// Devout Follower
					trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashTankLoc, 80, u => u.Entry == DevoutfollowerId);
					aggro = trash.Where(u => u.DistanceSqr <= 50 * 50 && u.CurrentTargetGuid == Me.Guid).ToList();
					hasAggroInMelee = aggro.Any(a => a.DistanceSqr <= 7 * 7);
					if (aggro.Any())
					{
						gravityWells =
							ObjectManager.GetObjectsOfType<WoWUnit>()
								.Where(
									u =>
										u.Entry == gravityWellId && u.DistanceSqr <= 30 * 30 &&
										Navigator.CanNavigateFully(StyxWoW.Me.Location, WoWMathHelper.CalculatePointFrom(aggro[0].Location, u.Location, -12)))
								.OrderBy(u => u.DistanceSqr)
								.ToList();
					}
					else gravityWells = null;
					return boss;
				},
				// boss combat behavior
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => StyxWoW.Me.IsHealer && hasAggroInMelee,
							new PrioritySelector(
								new Decorator(ctx => SpellManager.CanCast("Fade"), new Action(ctx => SpellManager.Cast("Fade"))),
								new Decorator(ctx => SpellManager.CanCast("Hand of Salvation"), new Action(ctx => SpellManager.Cast("Hand of Salvation", StyxWoW.Me))))),
				// kite adds in to gravity well
						ScriptHelpers.CreateLosLocation(
							ctx => !Me.IsTank() && aggro.Any() && gravityWells != null && gravityWells.Any(),
							ctx => aggro[0].Location,
							ctx => gravityWells[0].Location,
							ctx => 12),
				// run to tank

						new Decorator(
							ctx => aggro.Any() && StyxWoW.Me.IsFollower() && (gravityWells == null || !gravityWells.Any()) && ScriptHelpers.Tank.Distance > 10,
							new Action(ctx => Navigator.MoveTo(ScriptHelpers.Tank.Location))))),
				// trash pull before boss encounter is started.
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
				// give healer vigilane if warrior
						new Decorator(
							ctx => StyxWoW.Me.Class == WoWClass.Warrior && SpellManager.CanCast("Vigilance"),
							new Action(ctx => SpellManager.Cast("Vigilance", ScriptHelpers.Healer))),
						ScriptHelpers.CreatePullNpcToLocation(ctx => trash.Any(), ctx => !Me.IsActuallyInCombat, ctx => trash[0], ctx => trashTankLoc, 5),
				// pull the boss
						new Decorator(
							ctx => StyxWoW.Me.IsTank(),
							ScriptHelpers.CreatePullNpcToLocation(
								ctx => !trash.Any() && StyxWoW.Me.PartyMembers.All(p => p.IsAlive && p.HealthPercent > 80),
								ctx => true,
								ctx => boss,
								ctx => trashLoc,
								null,
								0)))));
		}

		#endregion

		#region Corborus

		private const uint CorborusId = 43438;

		private const uint ThrashingChargeId = 43743;
		private WaitTimer _corborusTimer = new WaitTimer(TimeSpan.FromSeconds(180));

		[EncounterHandler(43438, "Corborus", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite CorborusWaitForSpawnBehavior()
		{
			return
				new PrioritySelector(new Decorator(ctx => ctx == null && !_corborusTimer.IsFinished && Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(43438, "Corborus")]
		public Composite CorborusFight()
		{
			WoWUnit boss = null;
			var crystalBarrageIds = new uint[] { 86881, 92648 };
			var roomCenterLoc = new WoWPoint(1155.49, 881.957, 284.963);

			AddAvoidObject(ctx => true, () => roomCenterLoc, 25, 5, crystalBarrageIds);

			// avoid the thrashing charge..
			AddAvoidObject(
				ctx => boss != null && boss.IsValid,
				() => roomCenterLoc,
				25,
				8,
				o => o.Entry == ThrashingChargeId,
				o => Me.Location.GetNearestPointOnLine(boss.Location, o.Location));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Action(
					ctx =>
					{
						_corborusTimer.Reset();
						return RunStatus.Failure;
					}),
				new Decorator(ctx => Me.Location.Distance(roomCenterLoc) > 25, new Action(ctx => Navigator.MoveTo(roomCenterLoc))));
		}

		#endregion

		#endregion
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	internal class ForgottenDepths : Dungeon
	{
		#region Overrides of Dungeon


		private readonly WoWPoint _entrance = new WoWPoint(7278.526, 5002.926, 76.17107);
		private int _diedOnTrashCount;
		private bool _runningToBoss;

		public override uint DungeonId
		{
			get { return 611; }
		}

		public override WoWPoint Entrance
		{
			get
			{
				if (_runningToBoss)
				{
					_runningToBoss = false;
					_diedOnTrashCount++;
					Logger.Write(
						"You have died {0} times on trash while trying to run to group. {1} more attempts before leaving group.",
						_diedOnTrashCount,
						3 - _diedOnTrashCount);
				}
				return _entrance;
			}
		}

		public override void OnEnter()
		{
			_diedOnTrashCount = 0;

			_trashMobsToAvoid = GetDynamicBlackspots().ToList();

			DynamicBlackspotManager.AddBlackspots(_trashMobsToAvoid);

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

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspots(_trashMobsToAvoid);
			_trashMobsToAvoid = null;
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			var tanks = ScriptHelpers.GroupMembers.Where(g => g.IsTank && g.Player != null).Select(g => g.Player).ToList();
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;
					if (unit == null) return false;
					// ignore NPCs that has no tanks near.
					if (unit.Combat && !tanks.Any(t => t.Location.DistanceSqr(unit.Location) <= 40 * 40))
						return true;
					if (unit.Entry == FrozenHeadId)
						return true;

					if (unit.Entry == WhirlTurleID && unit.HasAura("Shell Block"))
						return true;
					return false;
				});
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit == null) continue;
				if (unit.Entry == WhirlTurleID && Me.IsMelee())
					priority.Score -= 10000;
			}
		}


		public override MoveResult MoveTo(WoWPoint location)
		{
			// prevent CR from moving out of stackup point at Megaera
			if (StackupAtMegaera && location.DistanceSqr(_megaeraRaidLocation) >= 9 * 9)
				return MoveResult.Moved;

			if (location.DistanceSqr(_flamingHeadLoc) < 10 * 10)
				return Navigator.MoveTo(_flamingHeadMovetoLoc);
			if (location.DistanceSqr(_venomousHeadLoc) < 10 * 10)
				return Navigator.MoveTo(_venomousHeadMovetoLoc);
			if (location.DistanceSqr(_frozenHeadLoc) < 10 * 10)
				return Navigator.MoveTo(_venomousHeadMovetoLoc);

			if (!Me.IsGhost && Me.Location.DistanceSqr(location) > 100 * 100)
				_runningToBoss = true;
			else
				_runningToBoss = false;
			return MoveResult.Failed;
		}

		#endregion

		private const uint GastropodId = 68220;

		#region Root

	    private readonly WaitTimer _isNotMovingTimer = WaitTimer.OneSecond;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

        [EncounterHandler(0)]
        public Func<WoWUnit, Task<bool>> RootHandler()
        {
            AddAvoidObject(ctx => !_isNotMovingTimer.IsFinished, o => !_isNotMovingTimer.IsFinished ? 20 : 12, u => u.Entry == GastropodId && u.ToUnit().Combat);

            return async npc =>
            {
                if (_diedOnTrashCount >= 3)
                {
                    _diedOnTrashCount = 0;
                    Alert.Show(
                        "Unable to travel to group",
                        "Automatically leaving group because bot is unable to travel to group because of getting aggro and dieing on the way. Press 'Continue' to stay",
                        30,
                        true,
                        true,
                        null,
                        () => Lua.DoString("LeaveParty()"),
                        "Continue",
                        "Leave");
                }

                // reset the isMovingTimer whenever bot is not moving. This timer is used to avoid some stuff while not moving.
                // Because we need to move when running out we use a timer to avoid 'stop n go' type behavior.
                if (Me.IsMoving)
                    _isNotMovingTimer.Reset();

                return false;
            };
        }

		#endregion

		#region Tortos

		private const uint RockfallID = 68219;
		private const uint WhirlTurleID = 67966;
		private const uint TortosId = 69712;

		[EncounterHandler(69712, "Tortos", Mode = CallBehaviorMode.Proximity)]
		public Composite TortosEncounter()
		{
			AddAvoidObject(ctx => true, 10, u => u.Entry == WhirlTurleID && ((WoWUnit)u).HasAura("Spinning Shell"));
			AddAvoidObject(ctx => true, 5, RockfallID);
			// Don't stand directly under him during encounter.
			AddAvoidObject(ctx => true, 6, o => o.Entry == TortosId && o.ToUnit().IsAlive);

			// stay away from the spray target.
			return new PrioritySelector();
		}

		#endregion

		#region Trash Avoidance
		private List<DynamicBlackspot> _trashMobsToAvoid;

		private readonly WoWPoint _trashBelowRampAfterTortosLoc = new WoWPoint(6210.283, 4753.755, -172.2116);

		private readonly WaitTimer _trashBelowRampAfterTortosRefreshTimer = new WaitTimer(TimeSpan.FromSeconds(5));
		private bool _shouldAvoidTrashBelowRampAfterTortos;

		IEnumerable<DynamicBlackspot> GetDynamicBlackspots()
		{
			yield return new DynamicBlackspot(
				() =>
				{
					if (!_trashBelowRampAfterTortosRefreshTimer.IsFinished) return _shouldAvoidTrashBelowRampAfterTortos;
					_trashBelowRampAfterTortosRefreshTimer.Reset();
					_shouldAvoidTrashBelowRampAfterTortos = !Me.Combat && Me.Location.Distance(_trashBelowRampAfterTortosLoc) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_trashBelowRampAfterTortosLoc, 25).Any(u => !u.Combat);
					return _shouldAvoidTrashBelowRampAfterTortos;
				},
				() => _trashBelowRampAfterTortosLoc,
				LfgDungeon.MapId,
				30.86862f,
				name: "Mobs at bottom of ramp after Tortos");
		}

		#endregion


		#region Madera

		//verified by erenion
		private readonly WoWPoint _flamingHeadLoc = new WoWPoint(6439.94, 4534.12, -209.692);
		private readonly WoWPoint _flamingHeadMovetoLoc = new WoWPoint(6424.047, 4533.906, -209.1807);
		private readonly WoWPoint _frozenHeadLoc = new WoWPoint(6419.33, 4504.384, -209.6087);
		private readonly WoWPoint _venomousHeadLoc = new WoWPoint(6395.116, 4494.938, -209.6087);
		private readonly WoWPoint _venomousHeadMovetoLoc = new WoWPoint(6390.613, 4507.01, -209.2759);

		private const uint CindersId = 70432;
		private const uint FrozenHeadId = 70235;
		private const uint VenomousHeadId = 70247;
		private const uint FlamingHeadId = 70212;

		private readonly WoWPoint _cindersRunLocation = new WoWPoint(6371.334, 4548.301, -209.1768);
		private WoWUnit _megaera;

		private readonly uint[] _magaeraHeadIds = { VenomousHeadId, FlamingHeadId, FrozenHeadId };

		private bool _stackupAtMegaera;
		private WoWPoint _megaeraRaidLocation;

		[LocationHandler(6435.174, 4546.373, -209.2975, 150, "Megaera")]
		public Func<WoWPoint, Task<bool>> MegaeraEncounter()
		{
			bool isSwimming = false;
			var updateRaidLocTimer = new WaitTimer(TimeSpan.FromSeconds(2));
			AddAvoidObject(ctx => true, 7, CindersId);

			return async location =>
			{
				if (updateRaidLocTimer.IsFinished)
				{
					_megaeraRaidLocation = ScriptHelpers.GetGroupCenterLocation(null, 20);
					updateRaidLocTimer.Reset();
				}

				if (Targeting.Instance.IsEmpty()
					|| !_magaeraHeadIds.Contains(Targeting.Instance.FirstUnit.Entry))
				{
					_megaera = null;
					return false;
				}
				
				if (Me.Debuffs.ContainsKey("Cinders"))
				{
					await CommonCoroutines.MoveTo(_cindersRunLocation);
					_stackupAtMegaera = false;
					return true;
				}

				_megaera = Targeting.Instance.FirstUnit;

				var myLoc = Me.Location;
				var maxDpsRange = Me.IsMelee() ? _megaera.MeleeRange() : 36 + _megaera.CombatReach;

				var bossLoc = _megaera.Location;
				if (_megaeraRaidLocation.Distance(bossLoc) > maxDpsRange)
				{	
					var maxPoint = WoWMathHelper.CalculatePointFrom(_megaeraRaidLocation, bossLoc, maxDpsRange);
					_megaeraRaidLocation = myLoc.GetNearestPointOnLine(maxPoint, bossLoc);
				}

				var raidDist = myLoc.Distance(_megaeraRaidLocation);
				_stackupAtMegaera = _megaeraRaidLocation.Distance(_megaera.Location) <= maxDpsRange;

				if (_stackupAtMegaera && raidDist > 9)
				{
					await CommonCoroutines.MoveTo(_megaeraRaidLocation);
					return true;
				}

				if (Me.IsSwimming && !isSwimming)
					isSwimming = true;

				// reset current navigation path after getting ported out of water when taking the water shortcut to prevent bot from running to next node in path which is usually in the water..
				if (!Me.IsSwimming && isSwimming && !Me.IsFalling && !Me.MovementInfo.IsAscending)
				{
					Navigator.NavigationProvider.Clear();
					isSwimming = false;
				}
				return false;
			};
		}

		bool StackupAtMegaera
		{
			get { return _megaera != null && _megaera.IsValid && _megaera.Combat && _stackupAtMegaera; }
		}

		#endregion

		#region Ji-Kun

		private const uint FeedPoolId = 68188;
		private const int DownDraft = 134370; //pushes you off the edge
		private const uint DemonicGatewayId = 59262;
		private const uint JiKunId = 69712;
		private readonly WoWPoint _jiKunPlatformLocation = new WoWPoint(6145.938, 4318.527, -31.86974);

		[EncounterHandler(69712, "Ji-Kun", BossRange = 150)]
		public Composite JiKunEncounter()
		{
			WoWUnit boss = null;
			// stay away from the spray target.
			AddAvoidObject(ctx => Me.HealthPercent < 60 && boss.CastingSpellId != DownDraft, 7, FeedPoolId);
			AddAvoidObject(ctx => true, 33, u => u.Entry == JiKunId && !u.ToUnit().Combat && u.ToUnit().IsAlive && u.Distance <= 50);

			var isFallingOrWhirling = new Func<bool>(() => Me.IsFalling || Me.HasAura("Safety Net Trigger "));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.HasAura(DownDraft) || boss.CastingSpellId == DownDraft,
					new PrioritySelector(
						ctx => GetGateway(boss.Location),
						new Decorator(ctx => !Me.IsSafelyFacing(boss, 10), new Action(ctx => boss.Face())),
						new Decorator<WoWUnit>(gateway => gateway.Distance <= 6, new Helpers.Action<WoWUnit>(gateway => gateway.Interact())),
						new Decorator(ctx => !Me.MovementInfo.MovingForward, new Action(ctx => WoWMovement.Move(WoWMovement.MovementDirection.Forward))),
				// prevent CR from getting called because it will stop moving.
						new ActionAlwaysSucceed())),
				// disable movement while falling or flying up to platform, prevents failed navigation.
				new Decorator(ctx => isFallingOrWhirling() && ScriptHelpers.MovementEnabled, new ActionAlwaysSucceed()),
				new Decorator(ctx => boss.Distance2D > 25 && boss.Combat, new Action(ctx => Navigator.MoveTo(boss.Location))));
		}

		private WoWUnit GetGateway(WoWPoint location)
		{
			return
				(WoWUnit)
					ObjectManager.ObjectList.Where(o => o.Entry == DemonicGatewayId && o.Location.Distance2D(location) > 15).OrderBy(u => u.Distance2DSqr).FirstOrDefault();
		}

		#endregion
	}
}
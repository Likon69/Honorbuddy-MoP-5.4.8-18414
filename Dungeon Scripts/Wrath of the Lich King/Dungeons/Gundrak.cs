using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class Gundrak : Dungeon
	{
		#region Overrides of Dungeon

		private const float BridgeZ = 118;
		private const float SouthBridgeX = 1748;
		private const float NorthBridgeX = 1797;
		private const float BridgeMinY = 736;
		private const float BridgeMaxY = 751;
		private readonly WoWPoint _northBridgeLocation = new WoWPoint(1800.753, 743.4679, 119.1964);
		private readonly WoWPoint _nwEntrance = new WoWPoint(6975.151, -4397.363, 441.5771);
		private readonly WoWPoint _seEntrance = new WoWPoint(6699.581, -4662.947, 441.5676);
		private readonly WoWPoint _southBridgeLocation = new WoWPoint(1751.669, 744.2233, 118.9558);
		private WaitTimer _deathTimer;

		public override uint DungeonId
		{
			get { return 216; }
		}

		public override WoWPoint Entrance
		{
			get
			{
				if (_deathTimer == null)
				{
					_deathTimer = new WaitTimer(TimeSpan.FromMinutes(2));
					_deathTimer.Reset();
				}
				var corsePoint = Me.CorpsePoint;
				if (corsePoint != WoWPoint.Zero)
					return corsePoint.DistanceSqr(_nwEntrance) < corsePoint.DistanceSqr(_seEntrance) ? _nwEntrance : _seEntrance;
				return Me.Location.DistanceSqr(_nwEntrance) < Me.Location.DistanceSqr(_seEntrance) ? _nwEntrance : _seEntrance;
			}
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(1898.504, 657.7404, 176.6373); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					if (ret.Entry == DrakkariFrenzyId && (!ret.ToUnit().Combat || Me.IsMoving))
						return true;
					if (ret.Entry == GaldarahId && ret.ToUnit().HasAura("Whirling Slash") && Me.IsMelee() && Me.IsDps())
						return true;
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
					if (unit.Entry == SnakeWrapId)
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
					if (unit.Entry == SnakeWrapId && StyxWoW.Me.IsDps())
						priority.Score += 500;
				}
			}
		}

		#endregion

		private const uint SnakeWrapId = 29742;
		private const uint SladranId = 29304;
		private const uint DrakkariFrenzyId = 29834;
		private const uint GaldarahId = 29306;
		private readonly int[] _frostNovaIds = new[] { 59842, 55081 };

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootBehavior()
		{
			return new PrioritySelector(
				// make sure we're at the right entrance when porting in after dieing.
				// fixed by zoning out and back in.
				new Decorator(
					ctx => _deathTimer != null,
					new Sequence(
						new DecoratorContinue(
							ctx => !_deathTimer.IsFinished && Me.Location.Distance(ExitLocation) > 50,
							new Sequence(
								new Action(ctx => Logger.Write("Porting outside because I'm not at right entrance.")),
								new Action(ctx => ScriptHelpers.TelportOutsideLfgInstance()),
								new WaitContinue(2, ctx => !StyxWoW.IsInWorld, new ActionAlwaysSucceed()),
								new WaitContinue(40, ctx => StyxWoW.IsInWorld, new ActionAlwaysSucceed()),
								new Action(ctx => Lua.DoString("LFGTeleport(false)")))),
						new Action(ctx => _deathTimer = null))),
				new PrioritySelector(
					ctx => ScriptHelpers.Tank,
					new Decorator<WoWPlayer>(
						tank => !Me.IsTank() && Me.IsSwimming && tank != null && !tank.IsSwimming && Navigator.CanNavigateFully(Me.Location, tank.Location),
						new Helpers.Action<WoWPlayer>(tank => Navigator.MoveTo(tank.Location)))));
		}

		[EncounterHandler(55738, "Tol'mar", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			const int galdarahMustPayQuestId = 29834;

			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					new PrioritySelector(
						new Decorator(
							ctx => !Me.QuestLog.ContainsQuest(galdarahMustPayQuestId) && !Me.QuestLog.GetCompletedQuests().Contains(galdarahMustPayQuestId),
							ScriptHelpers.CreatePickupQuest(ctx => unit, galdarahMustPayQuestId)))),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(29304, "Slad'ran")]
		public Composite SladranEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(ctx => true, 15, o => o.Entry == SladranId && _frostNovaIds.Contains(o.ToUnit().CastingSpellId));

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[ObjectHandler(192518, "Altar of Slad'ran", ObjectRange = 2000)]
		[ObjectHandler(192520, "Altar of the Drakkari Colossus", ObjectRange = 2000)]
		[ObjectHandler(192519, "Altar of Moorabi", ObjectRange = 2000)]
		public async Task<bool> AltarHandler (WoWGameObject altar)
		{
			if (altar.CanUse() && Me.IsLeader() )
				ScriptHelpers.SetInteractPoi(altar);
			return false;
		}

		[EncounterHandler(29307, "Drakkari Colossus")]
		public Composite DrakkariColossusEncounter()
		{
			const uint mojoPuddleId = 55627;
			AddAvoidObject(ctx => true, 3, mojoPuddleId);

			return new PrioritySelector();
		}

		[EncounterHandler(29306, "Gal'darah")]
		public Composite GaldarahEncounter()
		{
			WoWUnit _galdarah = null;
			AddAvoidObject(ctx => true, 15, o => o.Entry == GaldarahId && (o.ToUnit().HasAura("Whirling Slash") || Me.IsRange()));
			return new PrioritySelector(ctx => _galdarah = ctx as WoWUnit);
		}
	}
}
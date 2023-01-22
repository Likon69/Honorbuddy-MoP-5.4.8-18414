

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class DireMaulWarpwoodQuarter : Dungeon
	{
		#region Overrides of Dungeon

		private readonly JumpLink[] _poolExitJumpLinks = new[]
		{
			// north-east side
			new JumpLink (new  WoWPoint (-3.108631, -441.9224, -59.9572), new WoWPoint (-2.984955, -443.9251, -58.62362)),
			// south-east side
			new JumpLink (new WoWPoint (-32.26128, -442.0283, -59.95), new WoWPoint (-32.61896, -444.2151, -58.61603)),
			// south-west side
			new JumpLink (new WoWPoint (-37.27712, -411.3893, -59.94997), new WoWPoint (-37.79222, -408.4651, -58.61409)),
			// noeth-west side
			new JumpLink (new WoWPoint (-8.181879, -411.3037, -59.94997),new WoWPoint (-7.396617, -409.3429, -58.61412))
		};

		private readonly WoWPoint _poolLoc = new WoWPoint(-14.4789, -427.714, -59.95045);

		public override uint DungeonId
		{
			get { return 34; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-3764.563, 935.0845, 161.0271); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(41.75133, -155.5493, -2.714351); }
		}


		public override async Task<bool> HandleMovement(WoWPoint location)
		{
			// jump out of the pool.
			if (StyxWoW.Me.Location.DistanceSqr(_poolLoc) < 40 * 40)
			{
				if (StyxWoW.Me.Z < -59f && location.Z > -59 && !StyxWoW.Me.MovementInfo.IsAscending)
				{
					var bestExit = _poolExitJumpLinks.OrderBy(jl => jl.From.DistanceSqr(location)).FirstOrDefault();

					if (bestExit.From.Distance(StyxWoW.Me.Location) > Navigator.PathPrecision)
						Navigator.MoveTo(bestExit.From);

					else
					{
						Navigator.PlayerMover.MoveTowards(bestExit.To);
						WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
						WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
					}
					return true;
				}
			}
			return false;
		}

		struct JumpLink
		{
			public JumpLink(WoWPoint from, WoWPoint to)
			{
				From = from;
				To = to;
			}

			public readonly WoWPoint From;
			public readonly WoWPoint To;
		}
		#endregion

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(44971, "Ambassador Dagg'thol", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(44969, "Furgus Warpwood", Mode = CallBehaviorMode.Proximity)]
		public Composite AmbassadorDaggtholEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		const uint PimgibId = 14349;
		[EncounterHandler(14327, "Lethtendris")]
		public Composite LethtendrisEncounter()
		{
			const int shadowBoltVolleyId = 14887;
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Curse of Thorns", ScriptHelpers.PartyDispelType.Curse),
				ScriptHelpers.CreateDispelGroup("Curse of Tongues", ScriptHelpers.PartyDispelType.Curse),
				ScriptHelpers.CreateDispelGroup("Immolate", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateInterruptCast(ctx => boss, shadowBoltVolleyId)
				);
		}

		[EncounterHandler(14349, "Pimgib")]
		public Composite PimgibEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelEnemy("Enlarge", ScriptHelpers.EnemyDispelType.Magic, ctx => boss));
		}

		[EncounterHandler(14354, "Pusillin", Mode = CallBehaviorMode.Proximity)]
		public Composite PusillinEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit, new Decorator(ctx => !boss.IsHostile && Me.IsTank() && Targeting.Instance.IsEmpty(), ScriptHelpers.CreateTalkToNpc(ctx => boss)));
		}

		[EncounterHandler(11490, "Zevrim Thornhoof")]
		public Composite ZevrimThornhoofEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(11492, "Alzzin the Wildshaper", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite OpenPathTo_AlzzintheWildshaperBehavior()
		{
			// This tree's Entry is changed after you kill the boss before it. However the entry is somehow changed without a new tree spawning, so HB keeps the old cached entry for it.
			// Need to handle both IDs
			var ironbarkTheRedeemedIds = new HashSet<uint> { 11491, 14241 };
			const uint conservatoryDoorId = 176907;
			var ronbarkTheRedeemedLoc = new WoWPoint(-72.17187, -283.2524, -57.83297);

			WoWGameObject door = null;
			WoWUnit ironbarkTheRedeemed = null;

			return new Decorator(
				ctx => Me.IsTank() && Targeting.Instance.IsEmpty(),
				new PrioritySelector(
					ctx => door = ObjectManager.GetObjectsOfTypeFast<WoWGameObject>().FirstOrDefault(g => g.Entry == conservatoryDoorId),
					// talk to Ironbark the Redeemed if door is not open
					new Decorator(
						ctx => door == null || door.State == WoWGameObjectState.Ready,
						new PrioritySelector(
							ctx =>
								ironbarkTheRedeemed =
									ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => ironbarkTheRedeemedIds.Contains(u.Entry)),
							new Decorator(ctx => ironbarkTheRedeemed == null, new Action(ctx => Navigator.MoveTo(ronbarkTheRedeemedLoc))),
							new Decorator(ctx => ironbarkTheRedeemed.CanGossip, ScriptHelpers.CreateTalkToNpc(ctx => ironbarkTheRedeemed)),
							// wait at door for it to open
							new Decorator(
								ctx => door.DistanceSqr < 10 * 10,
								new PrioritySelector(
									new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
									new ActionAlwaysSucceed()))
							))
					));
		}

		[EncounterHandler(11492, "Alzzin the Wildshaper")]
		public Composite AlzzintheWildshaperEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelEnemy("Wild Regneration", ScriptHelpers.EnemyDispelType.Magic, ctx => boss),
				ScriptHelpers.CreateDispelGroup("Enervate", ScriptHelpers.PartyDispelType.Poison),
				ScriptHelpers.CreateDispelGroup("Wither", ScriptHelpers.PartyDispelType.Disease),
				ScriptHelpers.CreateDispelEnemy("Thorns", ScriptHelpers.EnemyDispelType.Magic, ctx => boss));
		}
	}
}
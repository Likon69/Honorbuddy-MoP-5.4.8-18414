using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using Bots.DungeonBuddy.Behaviors;
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
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class NightmareOfShekzeer : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 530; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(165.4734, 4069.003, 255.8758); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null) { }
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
					if (unit is WoWPlayer && !unit.IsMe && unit.HasAura("Reshape Life") && !Me.HasAura("Reshape Life"))
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
					if (unit.Entry == AmberMonstrosityId)
						priority.Score += 5000;
					else if (unit is WoWPlayer && !unit.IsMe && unit.HasAura("Reshape Life") && !Me.HasAura("Reshape Life"))
						priority.Score += 4500;
					else if (unit.Entry == LivingAmberId && !Me.HasAura("Reshape Life"))
						priority.Score += 1000;
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

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector();
		}

		#region Wind Lord Mel'jarak

		private const uint WhirlingBladeId = 63930;
		private const uint WindBombMissileSpellId = 131813;
		private const uint WindBombId = 67053;
		private const uint AmberPrisonId = 62531;
		private const int WhirlingBladeSpellId1 = 121897;
		private const int WhirlingBladeSpellId2 = 122083;
		private const uint CorrosiveResinPoolId = 67046;
		private const uint ZarthikBattleMenderId = 62408;
		private const int MendingId = 122193;

		[EncounterHandler(62397, "Wind Lord Mel'jarak", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite WindLordMeljarakEncounter()
		{
			WoWUnit boss = null;
			WoWUnit amberPrison = null;
			WoWUnit mender = null;
			WoWPoint raidLoc = WoWPoint.Zero;
			// avoid wind bombs.
			AddAvoidObject(ctx => true, 6, WindBombId);
			AddAvoidLocation(ctx => true, 5, o => ((WoWMissile)o).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellId == WindBombMissileSpellId));
			// avoid the whirling blades.
			AddAvoidLocation(
				ctx => true,
				6,
				o =>
				{
					var missile = (WoWMissile)o;
					return Me.Location.GetNearestPointOnSegment(missile.Position, missile.ImpactPosition);
				},
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == WhirlingBladeSpellId1 || m.SpellId == WhirlingBladeSpellId2));
			// run when you have the Corrosive Resin debuf
			AddAvoidObject(ctx => true, 5, u => u.IsMe && u.ToUnit().HasAura("Corrosive Resin"), o => WoWMathHelper.CalculatePointBehind(o.Location, o.Rotation, 2));
			// prevent placing the Corrosive Resin pools in melee...
			AddAvoidObject(ctx => Me.HasAura("Corrosive Resin"), 20, u => u.Guid == Me.CurrentTargetGuid);
			AddAvoidObject(ctx => true, 4, CorrosiveResinPoolId);

			return new PrioritySelector(
				ctx =>
				{
					amberPrison = (from prison in ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == AmberPrisonId)
								   let myLoc = Me.Location
								   let prisonLoc = prison.Location
								   where
									   !Me.RaidMembers.Any(
										   m => m.Guid != Me.Guid && m.Location.Distance(prisonLoc) < myLoc.Distance(prisonLoc) && !m.HasAura("Amber Prison")) ||
									   prisonLoc.Distance(myLoc) <= 8
								   orderby myLoc.DistanceSqr(prisonLoc)
								   select prison).FirstOrDefault();
					mender =
						ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.Entry == ZarthikBattleMenderId && u.CastingSpellId == MendingId)
									 .OrderBy(u => u.DistanceSqr)
									 .FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				// free other rade members that are in amber prisons
				new Decorator(
					ctx => amberPrison != null,
					new PrioritySelector(
						new Decorator(ctx => amberPrison.WithinInteractRange,
							new Sequence(
								new Action(ctx => amberPrison.Interact()),
								new WaitContinue(1, context => false, new ActionAlwaysSucceed())
								)),
						new Decorator(ctx => !amberPrison.WithinInteractRange, new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(amberPrison.Location)))))),
				new Decorator(ctx => mender != null, ScriptHelpers.CreateInterruptCast(ctx => mender, MendingId)),
				// make sure I'm standing with raid incase there's a wipe
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						ctx => raidLoc = ScriptHelpers.GetGroupCenterLocation(),
						new Decorator(ctx => Me.Location.Distance(raidLoc) > 10, new Action(ctx => Navigator.MoveTo(raidLoc))))));
		}

		#endregion

		#region Amber-Shaper Un'sok

		private const uint AmberScalpelId = 62510;
		private const uint LivingAmberId = 62691;
		private const uint AmberMonstrosityId = 62711;
		private const uint MutatedConstructId = 62701;
		private const uint BurningAmberId = 62512;

		[EncounterHandler(63569, "Amber Searsting")]
		public Composite AmberSearstingEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.TransportGuid > 0 && boss.CurrentTarget.Distance > 5,
					new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(boss.CurrentTarget.Location)))));
		}

		// players with the 'Reshape Life' debuf are transformed into a mutated construct

		[EncounterHandler(62511, "Amber-Shaper Un'sok")]
		public Composite AmberShaperUnsokEncounter()
		{
			WoWUnit boss = null;

			AddAvoidObject(ctx => true, 2, AmberScalpelId);
			AddAvoidObject(ctx => true, 4, u => u.Entry == LivingAmberId && u.ToUnit().HasAura("Permanent Feign Death"));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Construct abilites
				CreateConstructBehavior());
		}

		private Composite CreateConstructBehavior()
		{
			var amberStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(6));
			var struggleForControlTimer = new WaitTimer(TimeSpan.FromSeconds(6));
			return new Decorator(
				ctx => Me.HasAura("Reshape Life"),
				new PrioritySelector(
					new Decorator(
						ctx => Me.CurrentHealth < 20, new Sequence(new ActionLogger("Using 'Break Free' ability"), new Action(ctx => Lua.DoString("CastPetAction(4)")))),
					new Decorator(
						ctx => struggleForControlTimer.IsFinished,
						new Sequence(
							new ActionLogger("Using 'Struggle For Control' ability"),
							new Action(ctx => Lua.DoString("CastPetAction(2)")),
							new Action(ctx => struggleForControlTimer.Reset()))),
					new Decorator(
						ctx => !Targeting.Instance.IsEmpty(),
						new PrioritySelector(
							new Decorator(ctx => Targeting.Instance.FirstUnit.Guid != Me.CurrentTargetGuid, new Action(ctx => Targeting.Instance.FirstUnit.Target())),
							new Decorator(
								ctx => Me.CurrentTargetGuid != 0,
								new PrioritySelector(

									new Decorator(
										ctx => !Me.CurrentTarget.IsWithinMeleeRange,
										new Action(ctx => Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, 3)))),
									new Decorator(ctx => !Me.IsSafelyFacing(Me.CurrentTarget), new Action(ctx => Me.CurrentTarget.Face())),
									new Decorator(
										ctx => amberStrikeTimer.IsFinished && Me.CurrentTarget.IsCasting,
										new Sequence(
											new ActionLogger("Using 'Amber Strike' ability"),
											new Action(ctx => Lua.DoString("CastPetAction(1)")),
											new Action(ctx => amberStrikeTimer.Reset()))),
									new Decorator(ctx => !Me.IsAutoAttacking, new Action(ctx => Me.CurrentTarget.Interact())))))),
					new ActionAlwaysSucceed()));
		}

		#endregion

		#region Grand Empress Shek'zeer

		private const uint DissonanceFieldId = 62847;
		// kite when focused by a windblade
		[EncounterHandler(62837, "Grand Empress Shek'zeer")]
		public Composite GrandEmpressShekzeerEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(ctx => true, 8, DissonanceFieldId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion
	}
}
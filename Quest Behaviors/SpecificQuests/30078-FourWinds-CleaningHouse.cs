// Behavior originally contributed by Natfoth.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.CleaningHouse
{
	[CustomBehaviorFileName(@"SpecificQuests\30078-FourWinds-CleaningHouse")]
	public class FourWindsCleaningHouse : CustomForcedBehavior
	{
		private bool _isBehaviorDone;
		private Composite _root;

		public FourWindsCleaningHouse(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30078;
				QuestRequirementComplete =
					GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ??
					QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog =
					GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ??
					QuestInLogRequirement.InLog;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state

		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId
		{
			get { return ("$Id$"); }
		}

		public override string SubversionRevision
		{
			get { return ("$Revision$"); }
		}


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}

        public override void OnFinished()
        {
            Targeting.Instance.IncludeTargetsFilter -= Instance_IncludeTargetsFilter;
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
				Targeting.Instance.IncludeTargetsFilter += Instance_IncludeTargetsFilter;

				this.UpdateGoalText(QuestId);
			}
		}

		private void Instance_IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
		{
			var isOutdoors = Me.IsOutdoors;
			foreach (var unit in incomingUnits.OfType<WoWUnit>())
			{
				if (!isOutdoors &&
					(unit.Entry == EddyId || unit.Entry == JoogaId || unit.Entry == FizzyYellowSpiritId ||
					 unit.Entry == FizzyYellowAlementalId) &&
					!Blacklist.Contains(unit, BlacklistFlags.Combat))
					outgoingUnits.Add(unit);
			}
		}

		#endregion

		#region CreateBehavior_MainCombat

		private const int ChenOutsideID = 56133;

		private const int ChenInsideId = 58029;

		private Composite CreateBehavior_OutdoorsBehavior()
		{
			var chenOutsideLoc = new WoWPoint(-709.4158, 1266.86, 136.0237);
			WoWUnit chenOutside = null;
			// moveto and talk to Chen when outside the dungeon. 
			return new Decorator(
				ctx => Me.IsOutdoors && Targeting.Instance.FirstUnit == null,
				new PrioritySelector(
					ctx =>
						chenOutside =
							ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == ChenOutsideID),
					new Decorator(ctx => chenOutside == null, new Action(ctx => Navigator.MoveTo(chenOutsideLoc))),
					new Decorator(
						ctx => chenOutside != null,
						new PrioritySelector(
							new Decorator(
								ctx => !chenOutside.WithinInteractRange,
								new Action(ctx => Navigator.MoveTo(chenOutside.Location))),
							new Decorator(
								ctx => !GossipFrame.Instance.IsVisible,
								new Sequence(
									new Action(ctx => chenOutside.Interact()),
									new WaitContinue(2, ctx => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()))),
							new Decorator(
								ctx => GossipFrame.Instance.IsVisible,
								new Action(ctx => GossipFrame.Instance.SelectGossipOption(0)))))));

		}

		private Composite CreateBehavior_IndoorsBehavior()
		{
			return new Decorator(
				ctx => !Me.IsOutdoors,
				new PrioritySelector(
					//// temp fix for a caching bug.
					//new Decorator(ctx => Me.CurrentTargetGuid == 0 && Lua.GetReturnVal<ulong>("return UnitGUID('target')", 0) != 0, 
					//    new Action(ctx => Me.ClearTarget())),
					CreateBehavior_TalkToChenInside(),
					CreateBehavior_KillEddy(),
					CreateBehavior_KillJooga(),
					CreateBehavior_KillFizzyYellowAlemental()));
		}

		private Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(
				ctx => !IsDone,
				new PrioritySelector(CreateBehavior_OutdoorsBehavior(), CreateBehavior_IndoorsBehavior())));
		}

		private Composite CreateBehavior_TalkToChenInside()
		{
			WoWUnit chen = null;
			WoWPoint insideLoc = new WoWPoint(-676.3004, 1174.42, 139.1725);
			return
				new PrioritySelector(
					ctx => chen = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(o => o.Entry == ChenInsideId),
					new Decorator(
						ctx => chen != null && chen.DistanceSqr <= 40 * 40 && chen.CanGossip && chen.Location.DistanceSqr(insideLoc) < 1,
						new PrioritySelector(
							new Decorator(ctx => chen.DistanceSqr > 4*4, new Action(ctx => Navigator.MoveTo(chen.Location))),
							new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							new Decorator(ctx => !GossipFrame.Instance.IsVisible, new Action(ctx => chen.Interact())),
							new Action(ctx => GossipFrame.Instance.SelectGossipOption(0)))));
		}

		#region Eddy

		private const int EddyId = 58014;
		private const int TinyBubbleId = 210231;
		private WoWPoint _tinyBubbleMoveTo;

		private Composite CreateBehavior_KillEddy()
		{
			WoWUnit eddy = null;
			WoWPoint tinyBubbleMoveTo = WoWPoint.Zero;
			const int wateryShieldPopSpellId = 110198;
			const int wateryShieldSpellId = 110189;
			return new PrioritySelector(
				ctx => eddy = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == EddyId),
				new Decorator(
					ctx => eddy != null,
					new PrioritySelector(
						ctx => tinyBubbleMoveTo = GetTinyBubbleMoveTo(),
						// pop some bubbles until the watery shield can be removed from Eddy
						new Decorator(
							ctx => eddy.HasAura(wateryShieldSpellId) && tinyBubbleMoveTo != WoWPoint.Zero,
							new Action(ctx => Navigator.MoveTo(tinyBubbleMoveTo))),
						// pop the watery shield on eddy.
						new Decorator(
							ctx => eddy.HasAura(wateryShieldPopSpellId),
							new PrioritySelector(
								new Decorator(
									ctx => !eddy.WithinInteractRange,
									new Action(ctx => Navigator.MoveTo(eddy.Location))),
								new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
								new Action(ctx => eddy.Interact()))))));
		}

		protected override Composite CreateBehavior()
		{
			return new PrioritySelector(
				new Action(ctx => RunStatus.Failure));
		}

		private WoWPoint GetTinyBubbleMoveTo()
		{
			var myLoc = Me.Location;
			if (_tinyBubbleMoveTo != WoWPoint.Empty && _tinyBubbleMoveTo.DistanceSqr(myLoc) > 4*4)
				return _tinyBubbleMoveTo;

			return _tinyBubbleMoveTo =
				(from obj in ObjectManager.ObjectList
					where obj.Entry == TinyBubbleId && !Blacklist.Contains(obj, BlacklistFlags.Interact)
					let loc = obj.Location
					let distanceSqurared = loc.DistanceSqr(myLoc)
					where IsElegibleBubble(obj, distanceSqurared)
					orderby distanceSqurared
					select loc).FirstOrDefault();
		}

		private bool IsElegibleBubble(WoWObject bubble, float bubbleDistanceSquared)
		{
			if (bubbleDistanceSquared > 4*4)
				return true;
			Blacklist.Add(bubble, BlacklistFlags.Interact, TimeSpan.FromSeconds(5));
			return false;
		}

		#endregion

		#region Jooga

		private const uint JoogaId = 58015;
		private const uint EmptyKegId = 210230;

		private Composite CreateBehavior_KillJooga()
		{
			WoWUnit jooga = null;
			WoWGameObject emptyKeg = null;

			return new PrioritySelector(
				ctx => jooga = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == JoogaId),
				new Decorator(
					ctx => jooga != null,
					new PrioritySelector(
						// throw a keg on Jooga's head to make him stop chasing the panda cub.
						new Decorator(
							ctx => Me.Combat && jooga.HasAura("Slippery"),
							new PrioritySelector(
								ctx =>
									emptyKeg =
										ObjectManager.GetObjectsOfTypeFast<WoWGameObject>()
											.Where(g => g.Entry == EmptyKegId)
											.OrderBy(g => g.DistanceSqr)
											.FirstOrDefault(),
								new Decorator(
									ctx => emptyKeg != null,
									new PrioritySelector(
										new Decorator(
											ctx => !emptyKeg.WithinInteractRange,
											new Action(ctx => Navigator.MoveTo(emptyKeg.Location))),
										new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
										new Action(ctx => emptyKeg.Interact()))))))));
		}

		#endregion

		#region Fizzy Yellow Alemental

		private const uint FizzyYellowAlementalId = 58017;
		private const uint FizzyYellowSpiritId = 58253;
		// these are poisonous fumes that move around and are hard to see but do very small damage. will just ignore them.
		private const uint BurpId = 58304;

		private Composite CreateBehavior_KillFizzyYellowAlemental()
		{
			// Tank and spank!
			return new PrioritySelector(               
				// ctx => fizzyYellowAlemental = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == FizzyYellowAlementalId),
				// new Decorator(ctx => fizzyYellowAlemental != null, new PrioritySelector())
				);
		}

		#endregion

		#endregion
	}
}
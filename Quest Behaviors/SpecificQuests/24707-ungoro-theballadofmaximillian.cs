// Behavior originally contributed by mastahg.
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
//    <CustomBehavior File="StandAndKill" QuestId="25553" MobId="40974" X="3772.889" Y="-3233.83" Z="975.3411" /> // originally made for hyjal behavior
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
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.theballadofmaximillian
{

	[CustomBehaviorFileName(@"SpecificQuests\24707-ungoro-theballadofmaximillian")]
	public class theballadofmaximillian : CustomForcedBehavior
	{

		public theballadofmaximillian(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 24707;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

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
		private bool _isBehaviorDone;
		private Composite _root;


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		#region Overrides of CustomForcedBehavior




		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public WoWPoint start = new WoWPoint(-7228.146, -599.6198, -271.2461);

		//<Vendor Name="Devilsaur Queen" Entry="38708" Type="Repair" X="-7933.465" Y="-689.9974" Z="-258.6719" />
		public WoWUnit Dragon
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 38708 && r.IsAlive); }
		}

		public WoWUnit maxi
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 38237 && r.IsAlive); }
		}

		public WoWItem armor
		{
			get { return Me.BagItems.FirstOrDefault(r => r.Entry == 51794); }
		}

		public WoWItem rock
		{
			get { return Me.BagItems.FirstOrDefault(r => r.Entry == 51780); }
		}

		public Composite shoot(int which)
		{
			return new Action(r => Lua.DoString("CastPetAction({0})", which));
		}

		public Composite GoobyPls
		{

			get
			{
				return new PrioritySelector(
					new Decorator(r => !Query.IsInVehicle() && (maxi == null || maxi.Distance > 5), new Action(r => Navigator.MoveTo(start))),
					new Decorator(r => !Query.IsInVehicle() && maxi != null,
						new Action(r =>
						{
							maxi.Interact();
							GossipFrame.Instance.SelectGossipOption(0);
						})),
					 new Decorator(r => Query.IsInVehicle() && Dragon == null, shoot(1)),
					 new Decorator(r => Query.IsInVehicle() && Dragon != null && Me.CurrentTarget != Dragon, new Action(r => Dragon.Target())),
					 new Decorator(r => Query.IsInVehicle() && Dragon != null, new Action(r =>
						 {
							 if (Dragon.Distance <= 12) {Lua.DoString("CastPetAction({0})", 1);}
							 if (rock != null){Lua.DoString("CastPetAction({0})", 2);}
							 if (armor != null) {Lua.DoString("CastPetAction({0})", 3);}


						 }))
					 
						
					
					
					);
			}
		}


		protected override Composite CreateBehavior()
		{

			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GoobyPls, new ActionAlwaysSucceed())));
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
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
				//LevelBot.BehaviorFlags &= ~BehaviorFlags.Combat;

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}
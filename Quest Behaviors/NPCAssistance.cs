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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_MyCTM
//
// Allows you to physically click on the screen so that your bot can get around non meshed locations or off objects. *** There is no navigation with this ****
// ##Syntax##
// QuestId: Id of the quest.
// X,Y,Z: Where you wish to move.
// 
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.NPCAssistance
{
	[CustomBehaviorFileName(@"NPCAssistance")]
	public class NPCAssistance : CustomForcedBehavior
	{
		public enum NpcStateType
		{
			Alive,
			BelowHp,
			Dead,
			DontCare,
		}

		public enum NpcCommand
		{
			Target,
		}
		
		public NPCAssistance(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.     
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] { "NpcId" });
				NpcState = GetAttributeAsNullable<NpcStateType>("MobState", false, null, new[] { "NpcState" }) ?? NpcStateType.Alive;
				CurrentCommand = GetAttributeAsNullable<NpcCommand>("MobCommand", false, null, new[] { "NpcCommand" }) ?? NpcCommand.Target;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;
				WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? false;
				MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
				CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100.0;          
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
		public double CollectionDistance { get; private set; }
		public int[] MobIds { get; private set; }
		public NpcStateType NpcState { get; private set; }
		public NpcCommand CurrentCommand { get; private set; }
		public bool WaitForNpcs { get; private set; }
		public int WaitTime { get; private set; }
		public double MobHpPercentLeft { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private readonly List<ulong> _npcBlacklist = new List<ulong>();

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		private WoWUnit CurrentNPC
		{
			get
			{
				WoWUnit obj = null;

						var baseTargets = ObjectManager.GetObjectsOfType<WoWUnit>()
															   .OrderBy(target => target.Distance)
															   .Where(target => !_npcBlacklist.Contains(target.Guid) && !BehaviorBlacklist.Contains(target.Guid)
																				&& (target.Distance < CollectionDistance)
																				&& MobIds.Contains((int)target.Entry));

						var npcStateQualifiedTargets = baseTargets
															.Where(target => ((NpcState == NpcStateType.DontCare)
																			  || ((NpcState == NpcStateType.Dead) && target.IsDead)
																			  || ((NpcState == NpcStateType.Alive) && target.IsAlive)
																			  || ((NpcState == NpcStateType.BelowHp) && target.IsAlive && (target.HealthPercent < MobHpPercentLeft))));

						obj = npcStateQualifiedTargets.FirstOrDefault();

				if (obj != null)
                { QBCLog.DeveloperInfo(obj.SafeName); }

				return obj;
			}
		}

		class BehaviorBlacklist
		{
			static readonly Dictionary<ulong, BlacklistTime> SpellBlacklistDict = new Dictionary<ulong, BlacklistTime>();
			private BehaviorBlacklist()
			{
			}

			class BlacklistTime
			{
				public BlacklistTime(DateTime time, TimeSpan span)
				{
					TimeStamp = time;
					Duration = span;
				}
				public DateTime TimeStamp { get; private set; }
				public TimeSpan Duration { get; private set; }
			}

			static public bool Contains(ulong id)
			{
				RemoveIfExpired(id);
				return SpellBlacklistDict.ContainsKey(id);
			}

			static public void Add(ulong id, TimeSpan duration)
			{
				SpellBlacklistDict[id] = new BlacklistTime(DateTime.Now, duration);
			}

			static void RemoveIfExpired(ulong id)
			{
				if (SpellBlacklistDict.ContainsKey(id) &&
					SpellBlacklistDict[id].TimeStamp + SpellBlacklistDict[id].Duration <= DateTime.Now)
				{
					SpellBlacklistDict.Remove(id);
				}
			}
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				 new PrioritySelector(

					 new Decorator(ret => CurrentCommand == NpcCommand.Target && CurrentNPC != null,
								new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Targeting Npc: " + CurrentNPC.SafeName + " Distance: " + CurrentNPC.Location.Distance(Me.Location)),
										new Action(ret => CurrentNPC.Target()),
										new Sleep(WaitTime),
										new Action(ret => _isBehaviorDone = true)
									   )),

					 new Action(ret => TreeRoot.StatusText = "Waiting for Npc to spawn")

					));
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
				return (_isBehaviorDone     // normal completion
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
				this.UpdateGoalText(QuestId, "Npc Assistance Started");
			}
		}

		#endregion
	}
}


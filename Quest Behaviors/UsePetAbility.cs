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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
// Allows you to use item on an object or at a location
// ##Syntax##
// [Optional] QuestId: Id of the quest. If specified the QB will run until the quest is completed
// [Optional] MobId1, MobId2, ...MobIdN: Id of the object/npc that the item will be used on
// SpellIndex: Button bar Number starting from 1
// ItemId: Id of the item that will be used
// [Optional]NumOfTimes: Number of times
// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:100 yards
// [Optional]MobHpPercentLeft: What HP % of the mob when it will use the ability
// [Optional]WaitTime: Time to wait after using the item 
// UseType: Current (Current Location)
//          Location  (From X,Y,Z No Target)
//          ToObject  (from range of an object to object's location)
//          Default is Current
// [Optional]MobState: Alive (NPC is still alive at any HP)
//          BelowHp  (Alive but below HP % left)
//          Dead  (Dead as in not alive)
//          DontCare  (Can really be anything even dead)
//          Default is DontCare
// [Optional]X,Y,Z: If the UseType is AtLocation, QB will move to that location before using item. Otherwise it will move towards that point to search for objects
// [Optional]Range: If the UseType is ToObject, QB will move to that range of an object/npc before using item. (default 20)
// [Optional]MinRange: Will backup if too close. (default 4)
// [Optional]IgnoreCombat: Will Ignore All Combat (default False)
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
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.UsePetAbility
{
	[CustomBehaviorFileName(@"UsePetAbility")]
	public class UsePetAbility : CustomForcedBehavior
	{
		public enum QBType
		{
			Current = 0,
			Location = 1,
			ToObject = 2
		}

		public enum NpcStateType
		{
			Alive,
			BelowHp,
			Dead,
			DontCare,
		}


		public UsePetAbility(Dictionary<string, string> args)
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

				AttackButton = GetAttributeAsNullable<int>("AttackButton", true, ConstrainAs.HotbarButton, new[] { "AttackIndex", "SpellIndex" }) ?? 0;
				ClickToLocation = GetAttributeAsNullable<WoWPoint>("ClickTo", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100;
				IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? false;
				MinRange = GetAttributeAsNullable<double>("MinRange", false, ConstrainAs.Range, null) ?? 4.0;
				MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "ObjectId" });
				MoveToLocation = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				NpcState = GetAttributeAsNullable<NpcStateType>("MobState", false, null, new[] { "NpcState" }) ?? NpcStateType.DontCare;
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
				Range = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null) ?? 20.0;
				UseType = GetAttributeAsNullable<QBType>("UseType", false, null, null) ?? QBType.Current;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;

				Counter = 1;
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
		public int AttackButton { get; private set; }
		public WoWPoint ClickToLocation { get; private set; }
		public double CollectionDistance { get; private set; }
		public bool IgnoreCombat { get; private set; }
		public double MinRange { get; private set; }
		public int[] MobIds { get; private set; }
		public double MobHpPercentLeft { get; private set; }
		public WoWPoint MoveToLocation { get; private set; }
		public NpcStateType NpcState { get; private set; }
		public int NumOfTimes { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public double Range { get; private set; }
		public QBType UseType { get; private set; }
		public int WaitTime { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		public int Counter { get; private set; }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private readonly List<ulong> _npcBlacklist = new List<ulong>();

		private WoWUnit UseObject
		{
			get
			{
				var baseTargets = ObjectManager.GetObjectsOfType<WoWUnit>()
															   .OrderBy(target => target.Distance)
															   .Where(target => !_npcBlacklist.Contains(target.Guid) &&
															   target.Distance < CollectionDistance &&
															   !Me.Minions.Contains(target) &&
																MobIds.Contains((int)target.Entry));

						var npcStateQualifiedTargets = baseTargets
															.OrderBy(target => target.Distance)
															.Where(target => ((NpcState == NpcStateType.DontCare)
																			  || ((NpcState == NpcStateType.Dead) && target.IsDead)
																			  || ((NpcState == NpcStateType.Alive) && target.IsAlive)
																			  || ((NpcState == NpcStateType.BelowHp) && target.IsAlive && (target.HealthPercent < MobHpPercentLeft))));


						WoWUnit obj = npcStateQualifiedTargets.FirstOrDefault();


				if (obj != null)
                { QBCLog.DeveloperInfo(obj.SafeName); }

				return obj;
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root =
				new Decorator(ctx => !_isBehaviorDone && (!Me.IsActuallyInCombat || IgnoreCombat),
				new PrioritySelector(

					new Decorator(ret => Counter > NumOfTimes && QuestId == 0,
						new Action(ret => _isBehaviorDone = true)),

					new Decorator(
						ret => UseType == QBType.Current,
						new PrioritySelector(
							new Decorator(  
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Using Pet Ability:" + " " + Counter + " Out of " + NumOfTimes + " Times"),
									new Action(ret => Navigator.PlayerMover.MoveStop()),
                                    new SleepForLagDuration(),
									new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
                                    new SleepForLagDuration(),
									new Action(ret => Counter++),
									new Sleep(WaitTime))
							))),

					new Decorator(
						ret => UseType == QBType.Location,
						new PrioritySelector(
							new Decorator(
								ret => Me.Location.Distance(MoveToLocation) > 3,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Moving To Use Ability at Location, Distance: " + MoveToLocation.Distance(Me.Location)),
									new Action(ret => Navigator.MoveTo(MoveToLocation)))),
							new Sequence(
								new Action(ret => TreeRoot.StatusText = "Using Pet Ability At Location:" + " " + Counter + " Out of " + NumOfTimes + " Times"),
								new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new SleepForLagDuration(),
								new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
                                new SleepForLagDuration(),
								new Action(ret => Counter++),
								new Sleep(WaitTime))
							)),

					new Decorator(
						ret => UseType == QBType.ToObject,
						new PrioritySelector(
							new Decorator(
								ret => UseObject == null && Me.Location.DistanceSqr(MoveToLocation) >= 2 * 2,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Moving To Use Ability around Location. Distance: " + MoveToLocation.Distance(Me.Location)),
									new Action(ret => Navigator.MoveTo(MoveToLocation)))),
							new Decorator(
								ret => UseObject != null,
								new PrioritySelector(
									new Decorator(
										ret => UseObject.DistanceSqr >= Range * Range,
										new Sequence(
											new Action(ret => TreeRoot.StatusText = "Moving closer to the Target, Distance: " + MoveToLocation.Distance(Me.Location)),
											new Action(ret => Navigator.MoveTo(UseObject.Location)))),
									new Decorator(
										ret => UseObject.DistanceSqr < MinRange * MinRange,
										new Sequence(
											new Action(ret => TreeRoot.StatusText = "Too Close, Backing Up"),
											new Action(ret => Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, UseObject.Location, (float)MinRange + 2f)))
											)),
									new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Using Pet Ability On Target : " + UseObject.SafeName + " " + Counter + " Out of " + NumOfTimes + " Times"),
										new Action(ret => UseObject.Target()),
										new Action(ret => Navigator.PlayerMover.MoveStop()),
										new Action(ret => UseObject.Face()),
                                        new SleepForLagDuration(),
										new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
										new Action(ret => Counter++),
                                        new SleepForLagDuration(),
										new Action(ret => _npcBlacklist.Add(UseObject.Guid)),
										new Sleep(WaitTime)))),
							new Action(ret => TreeRoot.StatusText = "No objects around. Waiting")
							))
					)));
		}


        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
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
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}

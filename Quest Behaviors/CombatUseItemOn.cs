// Behavior originally contributed by Raphus.
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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CombatUseItemOn
//
// QUICK DOX:
//      Uses an item on a target while the toon is in combat.   The caller can determine at what point
//      in combat the item will be used:
//          * When the target's health drops below a certain percentage
//          * When the target is casting a particular spell
//          * When the target gains a particular aura
//          * When the toon gains a particular aura
//          * one or more of the above happens
//
//  Parameters (required, then optional--both listed alphabetically):
//      ItemId: Id of the item to use on the targets.
//      MobId1, MobId2, ...MobIdN [Required: 1]: Id of the targets on which to use the item.
//
//      CastingSpellId [Default:none]: waits for the target to be casting this spell before using the item.
//      HasAuraId [Default:none]: waits for the toon to acquire this aura before using the item.
//      MobHasAuraId [Default:none]: waits for the target to acquire this aura before using the item.
//      MobHpPercentLeft [Default:0 percent]: waits for the target's hitpoints to fall below this percentage
//              before using the item.
//
//      NumOfTimes [Default:1]: number of times to use the item on a viable target
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      X, Y, Z [Default:Toon's current location]: world-coordinates of the general location where the targets can be found.
//
//  Notes:
//      * One or more of CastingSpellId, HasAuraId, MobHasAuraId, or MobHpPercentLeft must be specified.
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
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.CombatUseItemOn
{
	[CustomBehaviorFileName(@"CombatUseItemOn")]
	public class CombatUseItemOn : CustomForcedBehavior
	{
		public CombatUseItemOn(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				CastingSpellId = GetAttributeAsNullable<int>("CastingSpellId", false, ConstrainAs.SpellId, null) ?? 0;
				MaxRange = GetAttributeAsNullable<double>("MaxRange", false, ConstrainAs.Range, null) ?? 25;
				HasAuraId = GetAttributeAsNullable<int>("HasAuraId", false, ConstrainAs.AuraId, new[] { "HasAura" }) ?? 0;
				ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
				Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] { "NpcId" });
				MobHasAuraId = GetAttributeAsNullable<int>("MobHasAuraId", false, ConstrainAs.AuraId, new[] { "NpcHasAuraId", "NpcHasAura" }) ?? 0;
				MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "NpcHpLeft", "NpcHPLeft" }) ?? 0;
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				UseOnce = GetAttributeAsNullable<bool>("UseOnce", false, null, null) ?? true;
				BlacklistMob = GetAttributeAsNullable<bool>("BlacklistMob", false, null, null) ?? false;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 500;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				// semantic coherency checks --
				if ((CastingSpellId == 0) && (HasAuraId == 0) && (MobHasAuraId == 0) && (MobHpPercentLeft == 0.0))
				{
					QBCLog.Error("One or more of the following attributes must be specified:\n"
								+ "CastingSpellId, HasAuraId, MobHasAuraId, MobHpPercentLeft");
					IsAttributeProblem = true;
				}

				QuestBehaviorBase.DeprecationWarning_Behavior(this, "CombatUseItemOnV2", BuildReplacementArguments());

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



		private List<Tuple<string, string>> BuildReplacementArguments()
		{
			var replacementArgs = new List<Tuple<string, string>>();

			QuestBehaviorBase.BuildReplacementArgs_QuestSpec(replacementArgs, QuestId, QuestRequirementComplete, QuestRequirementInLog);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, ItemId, "ItemId", 0);
			QuestBehaviorBase.BuildReplacementArgs_Ids(replacementArgs, "MobId", MobIds, true);

			QuestBehaviorBase.BuildReplacementArg(replacementArgs, CastingSpellId, "UseWhenMobCastingSpellId", 0);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, HasAuraId, "UseWhenMeHasAuraId", 0);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, MobHasAuraId, "UseWhenMobHasAuraId", 0);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, MobHpPercentLeft, "UseWhenMobHasHealthPercent", 0);

			var useItemStrategy =
				(UseOnce && !BlacklistMob) ? CombatUseItemOnV2.CombatUseItemOnV2.UseItemStrategyType.UseItemOncePerTarget
				: (UseOnce && BlacklistMob) ? CombatUseItemOnV2.CombatUseItemOnV2.UseItemStrategyType.UseItemOncePerTargetDontDefend
				: (!UseOnce && !BlacklistMob) ? CombatUseItemOnV2.CombatUseItemOnV2.UseItemStrategyType.UseItemContinuouslyOnTarget
				: CombatUseItemOnV2.CombatUseItemOnV2.UseItemStrategyType.UseItemContinuouslyOnTargetDontDefend;
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, useItemStrategy, "UseItemStrategy",
				CombatUseItemOnV2.CombatUseItemOnV2.UseItemStrategyType.UseItemOncePerTarget);

			QuestBehaviorBase.BuildReplacementArg(replacementArgs, NumOfTimes, "NumOfTimesToUseItem", 1);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, MaxRange, "MaxRangeToUseItem", 25.0);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, WaitTime, "WaitTimeAfterItemUse", 0);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, Location, "", Me.Location);

			return replacementArgs;
		}


		// Attributes provided by caller
		public int CastingSpellId { get; private set; }
		public double MaxRange { get; private set; }
		public int HasAuraId { get; private set; }
		public int ItemId { get; private set; }
		public WoWPoint Location { get; private set; }
		public int MobHasAuraId { get; private set; }
		public double MobHpPercentLeft { get; private set; }
		public int[] MobIds { get; private set; }
		public int NumOfTimes { get; private set; }
		public int QuestId { get; private set; }
		public bool UseOnce { get; private set; }
		public bool BlacklistMob { get; private set; }
		public int WaitTime { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private int Counter { get; set; }
		public WoWItem Item { get { return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId && i.Cooldown == 0); } }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		public WoWUnit Mob
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead && !BehaviorBlacklist.Contains(u.Guid))
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		private ulong _lastMobGuid;
		private Composite RootCompositeOverride()
		{
			return
				new PrioritySelector(
					new Decorator(
						ret => !_isBehaviorDone && Me.IsAlive,
						new PrioritySelector(
							new Decorator(ret => (Counter >= NumOfTimes) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Finished!"),
									new WaitContinue(120,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)),

							new Decorator(
								ret => Me.CurrentTarget != null && Item != null && (!UseOnce || Me.CurrentTarget.Guid != _lastMobGuid),
								new PrioritySelector(
									new Sequence(
									new Decorator(
										ret => (CastingSpellId != 0 && Me.CurrentTarget.CastingSpellId == CastingSpellId) ||
											   (MobHasAuraId != 0 && Me.CurrentTarget.Auras.Values.Any(a => a.SpellId == MobHasAuraId)) ||
											   (MobHpPercentLeft != 0 && Me.CurrentTarget.HealthPercent <= MobHpPercentLeft) ||
											   (HasAuraId != 0 && Me.HasAura(WoWSpell.FromId(HasAuraId).Name)),
										new PrioritySelector(
											new Decorator(

											new Sequence(
												new Action(ret => Navigator.PlayerMover.MoveStop()),
												new SleepForLagDuration(),
												new Action(ret => TreeRoot.StatusText = "Using item"),
												new Action(ret => _lastMobGuid = Me.CurrentTarget.Guid),
												new Action(ret => Item.UseContainerItem()),
												new Sleep(WaitTime),
												new DecoratorContinue(
													ret => QuestId == 0,
													new Action(ret => Counter++)),
												new DecoratorContinue(ret => BlacklistMob,
													new Action(ret => BehaviorBlacklist.Add(_lastMobGuid, TimeSpan.FromSeconds(30))))))))))

					))));
		}


		#region Overrides of CustomForcedBehavior

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(
						ret => !Me.Combat && Me.IsAlive,
							new PrioritySelector( ctx => Mob,
								new Decorator(
									ret => ret == null,
									new Sequence(
										new Action(ret => TreeRoot.StatusText = "Moving to location"),
										new Action(ret => Navigator.MoveTo(Location)))),
								new Decorator(
									ret => ret != null && ((WoWUnit)ret).Distance > MaxRange,
									new Action(ret => Navigator.MoveTo(Mob.Location))),
								new Decorator(
									ret => !Me.GotTarget && Mob.Distance <= MaxRange,
									new Action(ret => ((WoWUnit)ret).Target())),
								new Decorator(
									ret => RoutineManager.Current.PullBehavior != null,
									RoutineManager.Current.PullBehavior),
								new Action(ret => RoutineManager.Current.Pull()))),
					RootCompositeOverride()
				));
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
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
}

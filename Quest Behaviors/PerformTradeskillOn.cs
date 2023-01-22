// Behavior originally contributed by Unknown.
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
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.DBC;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.PerformTradeskillOn
{
	[CustomBehaviorFileName(@"PerformTradeskillOn")]
	class PerformTradeskillOn : CustomForcedBehavior
	{
		#region Constructor and Argument Processing
		public PerformTradeskillOn(Dictionary<string, string> args)
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

				CastOnItemId = GetAttributeAsNullable<int>("CastOnItemId", false, ConstrainAs.ItemId, null);
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, new[] { "NumTimes" }) ?? 1;
				TradeSkillId = GetAttributeAsNullable<int>("TradeSkillId", true, ConstrainAs.SpellId, null) ?? 0;
				TradeSkillItemId = GetAttributeAsNullable<int>("TradeSkillItemId", true, ConstrainAs.ItemId, null) ?? 0;
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
		public int? CastOnItemId { get; private set; }  /// If set, an item ID to cast the trade skill on.
		public int NumOfTimes { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public int TradeSkillId { get; private set; }
		public int TradeSkillItemId { get; private set; }  // Identifier for the trade skill item. E.g; the actual 'item' we use from the tradeskill window.
		#endregion

		// Private variables for internal state
		private bool _isBehaviorDone;
		private WoWSpell _recipeSpell;
		private int _numOfCasts;
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		public override void OnFinished()
		{
			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;
		}

		private Composite CreateTradeSkillCast()
		{
			return new PrioritySelector(
					new Decorator(ctx => _numOfCasts >= NumOfTimes, new Action(ctx => _isBehaviorDone = true)),

					new Decorator(ret => StyxWoW.Me.IsCasting,
						new ActionAlwaysSucceed()),

					new Sequence(
						// check we have the material to craft recipe
						new DecoratorContinue(ctx => GetMaxRepeat(_recipeSpell) == 0,
							new Action(
								ctx =>
								{
									_isBehaviorDone = true;
									return RunStatus.Failure;
								})),

						new Action(ctx => _recipeSpell.Cast()),

						// check if we're casting on an item.
						new DecoratorContinue(ctx => CastOnItemId.HasValue,
							new Sequence(ctx => StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == CastOnItemId.Value),
								new DecoratorContinue(ctx => ctx == null,
									new Action(ctx => QBCLog.Fatal("Could not find ItemId({0}).", CastOnItemId.Value))),
								new Action(ctx => ((WoWItem)ctx).Use()),
								new Sleep(Delay.BeforeButtonClick),
								// click the enchant confirmation botton.
								new Action(ctx => Lua.DoString("local _,frame = StaticPopup_Visible('REPLACE_ENCHANT') if frame then StaticPopup_OnClick(frame, 1) end")))),

						new WaitContinue(TimeSpan.FromMilliseconds(2000), ctx => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),
						new Action(ctx => _numOfCasts++),
				// wait for cast to finish.
						new WaitContinue(TimeSpan.FromMilliseconds(6000), ctx => !StyxWoW.Me.IsCasting, new ActionAlwaysSucceed())));
		}

		int GetMaxRepeat(WoWSpell recipe)
		{
			int maxRepeat = int.MaxValue;
			var spellReagents = recipe.InternalInfo.SpellReagents;
			if (spellReagents.Reagent == null)
				return maxRepeat;

			for (int index=0; index < spellReagents.Reagent.Length; index++)
			{
				var reagent = spellReagents.Reagent[index];
				if (reagent == 0)
					continue;
				var required = spellReagents.ReagentCount[index];
				if (required <=0 )
					continue;
				var numInBags = StyxWoW.Me.BagItems.Sum(i => i != null && i.IsValid && i.Entry == reagent ? i.StackCount : 0);
				var repeatNum = (int)(numInBags / required);
				if (repeatNum < maxRepeat)
					maxRepeat = repeatNum;
			}
			return maxRepeat;
		}

		WoWSpell GetRecipeSpell(int itemOrSpellId)
		{
            var skillLineId = TradeSkillId;

            var skillLineIds = StyxWoW.Db[ClientDb.SkillLine]
                .Select(r => r.GetStruct<SkillLineInfo.SkillLineEntry>())
                .Where(s => s.ID == skillLineId || s.ParentSkillLineId == skillLineId)
                .Select(s => (SkillLine) s.ID)
                .ToList();

			var recipes = SkillLineAbility.GetAbilities()
                .Where(a => skillLineIds.Contains(a.SkillLine) && a.NextSpellId == 0 && a.GreySkillLevel > 0 && a.TradeSkillCategoryId > 0)
				.Select(a => WoWSpell.FromId(a.SpellId))
				.Where(s => s.IsValid)
				.ToList();

			return recipes.FirstOrDefault(s => s.CreatesItemId == itemOrSpellId)
				?? recipes.FirstOrDefault(s => s.Id == itemOrSpellId);
		}

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return new PrioritySelector(
				new Decorator(ret => StyxWoW.Me.IsMoving,
					new Action(ret => Navigator.PlayerMover.MoveStop())),

				CreateTradeSkillCast());
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
			var skillLine = (SkillLine)TradeSkillId;
			if (!Enum.GetValues(typeof(SkillLine)).Cast<SkillLine>().Contains(skillLine))
			{
				QBCLog.ProfileError("TradeSkillId {0} is not a valid tradeskill Id.", TradeSkillId);
			}

			// special case for Runeforging since it's not considered a profession.
			_recipeSpell = skillLine == SkillLine.Runeforging 
				? WoWSpell.FromId(TradeSkillItemId) 
				: GetRecipeSpell(TradeSkillItemId);

			if (_recipeSpell == null || !_recipeSpell.IsValid)
			{
				QBCLog.ProfileError("TradeSkillItemId {0} is not a valid Item or Spell Id.", TradeSkillId);
			}
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}

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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FindingYourCenter
{
	[CustomBehaviorFileName(@"SpecificQuests\29890-JadeForest-FindingYourCenter")]
	public class FindingYourCenter : CustomForcedBehavior
	{
		private bool _isBehaviorDone;

		private Composite _root;
		private bool _useMount;

		public FindingYourCenter(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 29890;
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

		public int QuestId { get; set; }


		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}


		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

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


		private int power
		{
			get { return Lua.GetReturnVal<int>("return UnitPower(\"player\", ALTERNATE_POWER_INDEX)", 0); }
		}

		public Composite Balance
		{
			get
			{
				return new Decorator(ctx => Query.IsInVehicle(),
					new PrioritySelector(
						new Decorator(r => power <= 40,
							new Action(r => UsePetAbility("Focus"))),
						new Decorator(r => power >= 60,
							new Action(r => UsePetAbility("Relax")))));
			}
		}

		private Composite DrinkBrew
		{
			get
			{
				var brewLoc = new WoWPoint(-631.5737, -2365.238, 22.87861);
				WoWGameObject brew = null;
				const uint brewId = 213754;

				return
					new PrioritySelector(
						new Decorator(
							ctx => !Query.IsInVehicle(),
							new PrioritySelector(
								ctx => brew = ObjectManager.GetObjectsOfTypeFast<WoWGameObject>().FirstOrDefault(g => g.Entry == brewId),
								new Decorator(ctx => brew != null && !brew.WithinInteractRange, new Action(ctx => Navigator.MoveTo(brew.Location))),
								new Decorator(ctx => brew != null && brew.WithinInteractRange,
									new PrioritySelector(
										new Decorator(ctx => Me.IsMoving,
											new Action(ctx => WoWMovement.MoveStop())),
										new Sequence(new Action(ctx => brew.Interact()),
											new WaitContinue(3, ctx => false, new ActionAlwaysSucceed())))))));
			}
		}


		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				_useMount = CharacterSettings.Instance.UseMount;
				CharacterSettings.Instance.UseMount = false;

				this.UpdateGoalText(QuestId);
			}
		}

        public override void OnFinished()
        {
            CharacterSettings.Instance.UseMount = _useMount;
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public void UsePetAbility(string action)
		{
			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,
				DrinkBrew,
				Balance)));
		}
	}
}
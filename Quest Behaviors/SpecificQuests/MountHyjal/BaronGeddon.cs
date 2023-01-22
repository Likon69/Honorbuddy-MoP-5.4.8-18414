// Behavior originally contributed by Bobby53.
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
// Allows safely completing the http://www.wowhead.com/quest=25464 .  Can also be used
// on similar quest if one discovered.
// 
// Moves to XYZ
// Locates MobId
// If MobId has AuraId, run to XYZ
// Otherwise run to MobId and use ItemId
// At end, waits for Living Bomb before continuing
// 
// Note: to minimize damage, it will cast ItemId for a max of 5 seconds 
// then run to xyz and wait even if no aura is present.  the duration betwen
// aoe casts (aura present) varies and waiting for it to appear before
// running out results in a very weak toon (and possible death from living bomb)
// 
// ##Syntax##
// QuestId: The id of the quest.
// [Optional] MobId: The id of the object.
// [Optional] ItemId: The id of the item to use.
// [Optional] AuraId: Spell id of the aura on MobId that signals we should run
// [Optional] CollectionDistance: distance at xyz to search for MobId
// [Optional] Range: Distance to use item at
// X,Y,Z: safe point (location we run to when target has auraid) must be in LoS of MobId
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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.MountHyjal.BaronGeddon
{
	[CustomBehaviorFileName(@"SpecificQuests\MountHyjal\BaronGeddon")]
	public class BaronGeddon : CustomForcedBehavior
	{
		public BaronGeddon(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 25464;
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;

		private Composite _root;
		
		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}


		public WoWUnit Barron
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 40147);
			}
		}

		public WoWItem Rod
		{
			get { return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 54463); }
		}

		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId) && safe.Distance(Me.Location) < 3 && !Me.Combat,
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}

		//Safe
		//<Vendor Name="dd" Entry="0" Type="Repair" X="" />
		WoWPoint safe = new WoWPoint(5410.753,-2771.448,1516.072);
		//Attack
		//<Vendor Name="dd" Entry="0" Type="Repair" X="" />
		WoWPoint attack = new WoWPoint(5417.539,-2792.542,1515.283);
		public Composite DpsHim
		{
			get
			{
				return new Decorator(r => !Barron.HasAura("Inferno"), new PrioritySelector(
					
					new Decorator(r=>attack.Distance(Me.Location) > 3, new Action(r=>Navigator.MoveTo(attack))),
					//new Decorator(r=>!Me.GotTarget || Me.CurrentTarget != Barron, new Action(r=>Barron.Target())),
					new Decorator(r=> Me.IsCasting || Me.IsChanneling, new ActionAlwaysSucceed()),
					new Decorator(r=> Rod != null && Rod.Cooldown <= 0, new Action(r=>Rod.Use(Barron.Guid)))
					));
			}
		}


		public Composite RunAway
		{
			get
			{
				return new Decorator(r => Barron == null || Barron.HasAura("Inferno") || Me.IsQuestComplete(QuestId),
					new Decorator(r => safe.Distance(Me.Location) > 3,
						new Action(r => Navigator.MoveTo(safe))));
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,RunAway,DpsHim, new ActionAlwaysSucceed())));
		}


		#region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion

	}
}

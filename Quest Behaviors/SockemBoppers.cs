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
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SockemBoppers
{
	[CustomBehaviorFileName(@"SockemBoppers")]
	public class SockemBoppers : CustomForcedBehavior
	{
		public SockemBoppers(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = GetAttributeAsNullable<int>("QuestId", true, null,null) ?? 0;
			MobIds = GetAttributeAsArray<uint>("MobIds", false, null, new[] { "NpcIds" }, null);
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;

		private Composite _root;

		private uint[] MobIds { get; set; }
		
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



		public static WoWItem Bopper
		{
			get
			{
				return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63390);
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
				this.UpdateGoalText(QuestId);
			}
		}

		private WoWUnit Gooby
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(r => MobIds.Contains(r.Entry) && r.IsAlive).OrderBy(r=>r.Distance2D)
					.FirstOrDefault();
			}
		}


		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new PrioritySelector(
				
				DoneYet,
				new Decorator(r=> (!Me.GotTarget || !Me.CurrentTarget.IsAlive) && Gooby != null, new Action(r=>Gooby.Target())),
				new Decorator(r=> Me.GotTarget &&  Me.CurrentTarget.Distance > 3, new Action(r=>Navigator.MoveTo(Me.CurrentTarget.Location))),
				new Decorator(r => Me.GotTarget && Me.CurrentTarget.Distance <= 3, new Action(r=>Bopper.Use(true)))
				
				));
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
	}
}

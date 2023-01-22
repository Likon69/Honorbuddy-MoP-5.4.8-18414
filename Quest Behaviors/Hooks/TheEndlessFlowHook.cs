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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Buddy.Coroutines;
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


namespace Honorbuddy.Quest_Behaviors.Hooks
{
	[CustomBehaviorFileName(@"Hooks\TheEndlessFlowHook")]
	public class TheEndlessFlowHook : CustomForcedBehavior
	{
		public TheEndlessFlowHook(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}

		public override bool IsDone { get { return true; } }

		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		private Composite _myHook;
		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			
			if (_myHook == null)
				InstallHook();
			else
				RemoveHook();
		}

		void InstallHook()
		{
			if (_myHook != null)
				return;
			_myHook = new ActionRunCoroutine(ctx => MainLogic());
			TreeHooks.Instance.InsertHook("Questbot_Main", 0, _myHook);
			BotEvents.OnBotStopped += BotEvents_OnBotStopped;
			BotEvents.Profile.OnNewProfileLoaded += Profile_OnNewProfileLoaded;
			QBCLog.Debug(this, "Installed hook");
		}

		void RemoveHook()
		{
			if (_myHook == null)
				return;
			TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook);
			_myHook = null;
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			BotEvents.Profile.OnNewProfileLoaded -= Profile_OnNewProfileLoaded;
			QBCLog.Debug(this, "Removed hook");
		}

		void Profile_OnNewProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
		{
			RemoveHook();
		}

		void BotEvents_OnBotStopped(System.EventArgs args)
		{
			RemoveHook();
		}

		private async Task<bool> MainLogic()
		{
			if (!Me.IsAlive)
				return false;

			var bomb = Me.BagItems.FirstOrDefault(i => Query.IsViable(i) && i.Entry == 60849 || i.Entry == 60678);
			var bunny = ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(r => r.Entry == 44360)
				.OrderBy(r=>r.Distance2D)
				.FirstOrDefault();

			//Remove hook once were done with the quest
			if (bomb == null)
			{
				RemoveHook();
				return true;
			}

			if (bunny != null)
			{
				var bunnyDist = bunny.DistanceSqr;
				if (bunnyDist < 5*5)
				{
					bomb.Use();
					SpellManager.ClickRemoteLocation(bunny.Location);
					await Coroutine.Sleep(Delay.AfterItemUse.Milliseconds);
					return true;
				}
				if (bunnyDist < 35*35)
				{
					return await UtilityCoroutine.MoveTo(bunny.Location, "Bunny", MovementByType.NavigatorOnly);
				}
			}
								
			var stickbone = ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(r => r.Entry == 44329 && r.IsAlive)
				.OrderBy(r => r.Distance2D)
				.FirstOrDefault();
			if (stickbone != null && stickbone.DistanceSqr < 5*5)
			{
				bomb.Use();
				SpellManager.ClickRemoteLocation(stickbone.Location);
				await Coroutine.Sleep(Delay.AfterItemUse.Milliseconds);
				return true;
			}
			return false;
		}
	}
}

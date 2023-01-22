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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheGreatBankHeist
{
	[CustomBehaviorFileName(@"SpecificQuests\14122-Kezan-TheGreatBankHeist")]
	public class _14122 : CustomForcedBehavior
	{
		public _14122(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 14122;
		}

		private int QuestId { get; set; }

		private bool IsBehaviorDone;
		private WoWPoint wp = new WoWPoint(-8361.689, 1726.248, 39.94792);
		private int _petAbilityIndex;

		private readonly string[] _bossWhisperIcons =
		{
			// Amazing G-Ray
			"INV_Misc_EngGizmos_20.blp",
			// Blastcrackers
			"INV_Misc_Bomb_07.blp",
			// Ear-O-Scope!
			"INV_Misc_Ear_NightElf_02.blp",
			// Infinifold Lockpick
			"INV_Misc_EngGizmos_swissArmy.blp",
			// Kaja'mite Drill
			"INV_Weapon_ShortBlade_21.blp"
		};


		public WoWGameObject Bank
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWGameObject>()
					.Where(ctx => ctx.Entry == 195449)
					.OrderBy(ctx => ctx.DistanceSqr).FirstOrDefault();
			}
		}
		private Composite _root;

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

		async Task<bool> MainCoroutine()
		{
			if (IsBehaviorDone)
			{
				return false;
			}

			var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
			if (quest.IsCompleted)
			{
				if (StyxWoW.Me.HasAura("Vault Cracking Toolset"))
					Lua.DoString("VehicleExit()");
				IsBehaviorDone = true;
				return true;
			}

			if (StyxWoW.Me.Location.DistanceSqr(wp) > 5 * 5)
			{
				TreeRoot.StatusText = "Moving to location";
				Navigator.MoveTo(wp);
				return true;
			}

			if (!StyxWoW.Me.HasAura("Vault Cracking Toolset"))
			{
				Bank.Interact();
				await Coroutine.Sleep((int)Delay.LagDuration.TotalMilliseconds);
				return true;
			}

			if (_petAbilityIndex > 0)
			{
				await Coroutine.Sleep((int)Delay.BeforeButtonClick.TotalMilliseconds);
				Lua.DoString("CastPetAction({0})", _petAbilityIndex);
				_petAbilityIndex = 0;
				return true;
			}

			return false;
		}

		public override bool IsDone
		{
			get { return (IsBehaviorDone); }
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				Lua.Events.AttachEvent("CHAT_MSG_RAID_BOSS_WHISPER", BossWhisperHandler);
				this.UpdateGoalText(QuestId);
			}
		}

		public override void OnFinished()
		{
			Lua.Events.DetachEvent("CHAT_MSG_RAID_BOSS_WHISPER", BossWhisperHandler);
			base.OnFinished();
		}

		public void BossWhisperHandler(object sender, LuaEventArgs arg)
		{
			var msg = arg.Args[0].ToString();
			var match = _bossWhisperIcons.FirstOrDefault(msg.Contains);
			_petAbilityIndex = match != null ? (_bossWhisperIcons.IndexOf(match) + 1) : 0;
		}
	}
}

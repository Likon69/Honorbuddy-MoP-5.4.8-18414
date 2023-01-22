//This BotBase was created by Apoc, I take no credit for anything within this code
//I just changed "!StyxWoW.Me.CurrentTarget.IsFriendly" to "!StyxWoW.Me.CurrentTarget.IsHostile"
//For the purpose of allowing RaidBot to work within Arenas

using System.Windows.Forms;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.WoWInternals;
using Styx.TreeSharp;

namespace RaidBot
{
	public class RaidBot : BotBase
	{
		private Composite _root;
		public override string Name { get { return "Raid Bot"; } }
		public override Composite Root { get { return _root ?? (_root = new PrioritySelector(CreateRootBehavior())); } }
		public override PulseFlags PulseFlags { get { return PulseFlags.All & ~(PulseFlags.Targeting | PulseFlags.Looting); } }
		public bool IsPaused { get; set; }
		public override void Start()
		{
			TreeRoot.TicksPerSecond = 30;
			//if (ProfileManager.CurrentProfile == null)
				ProfileManager.LoadEmpty();

			HotkeysManager.Register("RaidBot Pause",
				Keys.X,
				ModifierKeys.Alt,
				hk =>
					{
						IsPaused = !IsPaused;
						if (IsPaused)
						{
							Lua.DoString("print('RaidBot Paused!')");
							// Make the bot use less resources while paused.
							TreeRoot.TicksPerSecond = 5;
						}
						else
						{
							Lua.DoString("print('RaidBot Resumed!')");
							// Kick it back into overdrive!
							TreeRoot.TicksPerSecond = 30;
						}
					});
		}

		private Composite CreateRootBehavior()
		{
			return
				new PrioritySelector(
					new Decorator(ret => IsPaused,
						new Action(ret => RunStatus.Success)),
					new Decorator(ret => !StyxWoW.Me.Combat,
						new PrioritySelector(
							RoutineManager.Current.PreCombatBuffBehavior)),
					new Decorator(ret => StyxWoW.Me.Combat,
						new LockSelector(
							RoutineManager.Current.HealBehavior,
							new Decorator(ret => StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.IsFriendly && !StyxWoW.Me.CurrentTarget.IsDead,
								new PrioritySelector(
									RoutineManager.Current.CombatBuffBehavior,
									RoutineManager.Current.CombatBehavior)))));
		}

		public override void Stop()
		{
			TreeRoot.ResetTicksPerSecond();
			HotkeysManager.Unregister("RaidBot Pause");
		}

		#region Nested type: LockSelector

		private class LockSelector : PrioritySelector
		{
			public LockSelector(params Composite[] children) : base(children)
			{
			}

			public override RunStatus Tick(object context)
			{
				using (StyxWoW.Memory.AcquireFrame())
				{
					return base.Tick(context);
				}
			}
		}

		#endregion
	}
}
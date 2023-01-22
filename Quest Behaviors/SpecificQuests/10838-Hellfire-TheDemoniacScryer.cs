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
using System.Diagnostics;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheDemoniacScryer
{
	[CustomBehaviorFileName(@"SpecificQuests\10838-Hellfire-TheDemoniacScryer")]
	public class _10838 : CustomForcedBehavior
	{
		public _10838(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ??0;
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
		public WoWPoint Location = new WoWPoint(-145.9611, 3192.408, -65.09953);
		public int ItemId = 31606;
		public int MobId = 22258;
		public Stopwatch TimeOut = new Stopwatch();
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		private ConfigMemento _configMemento;
		private bool _isBehaviorDone;
		private Composite _root;
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private List<WoWUnit> MobList
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
										.Where(u => u.Entry == MobId && !u.IsDead)
										.OrderBy(u => u.Distance).ToList());
			}
		}
		private List<WoWUnit> Mobs
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry != MobId && !u.IsDead && u.IsHostile && u.Distance < 15).OrderBy(u => u.Distance).ToList();
			}
		}

		WoWSpell RangeSpell
		{
			get
			{
				switch (Me.Class)
				{
					case Styx.WoWClass.Druid:
						return SpellManager.Spells["Starfire"];
					case Styx.WoWClass.Hunter:
						return SpellManager.Spells["Arcane Shot"];
					case Styx.WoWClass.Mage:
						return SpellManager.Spells["Frost Bolt"];
					case Styx.WoWClass.Priest:
						return SpellManager.Spells["Shoot"];
					case Styx.WoWClass.Shaman:
						return SpellManager.Spells["Lightning Bolt"];
					case Styx.WoWClass.Warlock:
						return SpellManager.Spells["Curse of Agony"];
					default: // should never get to here but adding this since the compiler complains
						return SpellManager.Spells["Auto Attack"]; ;
				}
			}
		}

        public override void OnFinished()
        {
            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone);
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
				_configMemento = new ConfigMemento();

				// Disable any settings that may interfere with the escort --
				// When we escort, we don't want to be distracted by other things.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				ProfileManager.CurrentProfile.LootMobs = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;

				var mob = ObjectManager.GetObjectsOfType<WoWUnit>()
									  .FirstOrDefault(unit => unit.Entry == MobId);

                this.UpdateGoalText(QuestId, "Escorting " + ((mob != null) ? mob.SafeName : ("Mob(" + MobId + ")")));
			}
		}

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

							new Decorator(ret => Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Finished!"),
									new WaitContinue(120,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)),

						   new Decorator(ret => MobList.Count == 0 && Me.Location.Distance(Location) > 5,
								new Sequence(
										new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
										new Action(ret => Flightor.MoveTo(Location)),
										new Sleep(300)
									)
								),
						   new Decorator(ret => MobList.Count == 0 && Me.Location.Distance(Location) <= 5,
							   new Sequence(
										new Action(ret => Lua.DoString("UseItemByName({0})", ItemId)),
										new Action(ret => TimeOut.Start())
										)
							   ),
						   new Decorator(ret => TimeOut.ElapsedMilliseconds >= 300000,
							   new Sequence(
								   new Action(ret => MobList[0].Interact()),
								   new Sleep(500),
								   new Action(ret => Lua.DoString("SelectGossipOption(1)"))
								   )
							  ),

						   new Decorator(ret => Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly,
							   new Action(ret => Me.ClearTarget())),

						   new Decorator(
							   ret => Mobs.Count > 0 && Mobs[0].IsHostile,
							   new PrioritySelector(
								   new Decorator(
									   ret => Me.CurrentTarget != Mobs[0],
                                       new Sequence(
                                           new Action(ctx => Mobs[0].Target()),
                                           new SleepForLagDuration())),
								   new Decorator(
									   ret => !Me.Combat,
									   new PrioritySelector(
											new Decorator(
												ret => RoutineManager.Current.PullBehavior != null,
												RoutineManager.Current.PullBehavior),
											new Action(ret => RoutineManager.Current.Pull()))))),

						   new Decorator(ret => Mobs.Count > 0 && (Me.Combat || Mobs[0].Combat),
								new PrioritySelector(
									new Decorator(
										ret => Me.CurrentTarget == null && Mobs[0].CurrentTarget != null,
										new Sequence(
										new Action(ret => MobList[0].Target()),
										new SleepForLagDuration())),
									new Decorator(
										ret => !Me.Combat,
										new PrioritySelector(
											new Decorator(
												ret => RoutineManager.Current.PullBehavior != null,
												RoutineManager.Current.PullBehavior),
											new Action(ret => RoutineManager.Current.Pull())))))

						)
					);
		}
		#endregion
	}
}

// Behavior originally contributed by mastahg.
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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FireFromTheSky
{
	[CustomBehaviorFileName(@"SpecificQuests\28497-28736-Uldum-FireFromTheSky")]
	public class FireFromSky : CustomForcedBehavior
	{

		public FireFromSky(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		#region Overrides of CustomForcedBehavior

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


		public int UnfriendlyUnitsNearTarget(float distance, WoWUnit who)
		{
			var dist = distance * distance;
			var curTarLocation = who.Location;
			return ObjectManager.GetObjectsOfType<WoWUnit>().Count(p =>  p.IsAlive && (p.Entry == 48713) && p.Location.DistanceSqr(curTarLocation) <= dist);
			// (p.Entry == 48720 || p.Entry == 48713) changed to (p.Entry == 48720)
		}
 
		public List<WoWUnit> Enemies
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsAlive && (u.Entry == 48713)).OrderByDescending(z => UnfriendlyUnitsNearTarget(20, z)).ToList();
					// (u.Entry == 48720 || u.Entry == 48713) changed to (u.Entry == 48720)

			}
		}


		public Composite ShootStuff
		{
			get
			{
				return new Decorator(ret => Me.PetSpells[0].Cooldown == false, Fire);
			}
		}

		/*
		protected WoWPoint getEstimatedPosition(WoWUnit who,double time)
		{
			var targetVelocity = 1.50;
			var targetStartingPoint = who.MovementInfo.Position;
			double x = targetStartingPoint.X +
			   targetVelocity * time * who.MovementInfo.DirectionSinX;//Math.Sin(who.Rotation);
			double y = targetStartingPoint.Y +
			   targetVelocity * time * who.MovementInfo.DirectionCosY;//Math.Cos(who.Rotation);
			return new WoWPoint(x, y,who.Z);
		}
		*/

		public Composite Fire
		{
			get
			{
				return new Action(delegate
									  {
										  ObjectManager.Update();
										  Lua.DoString("CastPetAction(1);");
										  //SpellManager.ClickRemoteLocation(getEstimatedPosition(Enemies[0],7));

										   if (Enemies[0].Z <= 285)
										 {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 15));
										  }
										  /*
										  // bottom left
										  if ((Enemies[0].Z >= 200) && (Enemies[0].Z <= 213))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 11));
										  }
										  
										  // middle left
										  if ((Enemies[0].Z >= 219) && (Enemies[0].Z <= 228))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 11));
										  }
										  // middle flat - i should change this to 2x x/y's for vector changes
										  else if ((Enemies[0].Z >= 235) && (Enemies[0].Z <= 240))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 16));
										  }
										  //
										  /* this doesn't quite work the way i want it to
										  // Middle Flat - Left
										  else if (((Enemies[0].Z >= 236) && (Enemies[0].Z <= 240)) && ((Enemies[0].Y >= -57) && (Enemies[0].Y <= -44)) && ((Enemies[0].X >= -8534) && (Enemies[0].X <= -8515)))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 16));
										  }
										  // Middle Flat - Right
										  else if (((Enemies[0].Z >= 236) && (Enemies[0].Z <= 240)) && ((Enemies[0].Y >= -40) && (Enemies[0].Y <= -15)) && ((Enemies[0].X >= -8509) && (Enemies[0].X <= -8507)))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 16));
										  }
										  /
										  // middle right
										  else if ((Enemies[0].Z >= 244) && (Enemies[0].Z <= 253))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 12));
										  }
										  // middle top right
										  else if ((Enemies[0].Z >= 262) && (Enemies[0].Z <= 272))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 11));
										  }
										  // top right
										  else if ((Enemies[0].Z >= 282) && (Enemies[0].Z <= 292))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 16));
										  }
										  /* top
										  else if ((Enemies[0].Z >= 292) && (Enemies[0].Z <= 307))
										  {
											  SpellManager.ClickRemoteLocation(Enemies[0].Location.RayCast(Enemies[0].Rotation, 22));
										  }
										  */
										  
										  QBCLog.Info(UnfriendlyUnitsNearTarget(20,Enemies[0]).ToString());
									  });
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,ShootStuff)));
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
				return (_isBehaviorDone // normal completion
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
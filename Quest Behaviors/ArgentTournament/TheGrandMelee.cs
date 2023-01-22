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

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
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


namespace Styx.Bot.Quest_Behaviors
{
[CustomBehaviorFileName(@"ArgentTournament\TheGrandMelee")]
	public class TheGrandMelee : CustomForcedBehavior
	{
		public TheGrandMelee(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??MountSpot;
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;

				Enemy = GetAttributeAsArray<uint>("Enemys", false, new ConstrainTo.Domain<uint>(0, 100000), new[] { "Enemy" }, null);
				EnemyDebuff = GetAttributeAsArray<uint>("EnemysDebuff", false, new ConstrainTo.Domain<uint>(0, 100000), new[] { "EnemyDebuff" }, null);
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

		uint[] Mounts = new uint[]{33798,33796,33799,33842,33791};
		private uint[] Enemy;// = new uint[] { 33384, 33306,33285,33382,33383};
		private uint[] EnemyDebuff;// = new uint[] { 64816, 64811, 64812, 64813, 64815 };

		WoWItem HordeLance()
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(x => x.Entry == 46070);
		}
		WoWItem ArgentLance()
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(x => x.Entry == 46106);
		}

		WoWItem BestLance()
		{
			return HordeLance() ?? ArgentLance();
		}

		// Attributes provided by caller
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public WoWPoint Location { get; private set; }

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
				return
					new Decorator(ret => Me.IsQuestComplete(QuestId) && !Me.Combat, new Action(delegate
					{
						Lua.DoString("RunMacroText(\"/leavevehicle\")");

						mainhand.UseContainerItem();
						if (offhand != null)
						{
							offhand.UseContainerItem();
						}

						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}
		

		public void UsePetSkill(string action)
		{

			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;
			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
		}

		WoWUnit Mount
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(
						x => Mounts.Contains(x.Entry) && x.NpcFlags == 16777216);
			}
		}

		//WoWPoint endspot = new WoWPoint(1076.7,455.7638,-44.20478);
		// WoWPoint spot = new WoWPoint(1109.848,462.9017,-45.03053);
		WoWPoint MountSpot = new WoWPoint(8426.872,711.7554,547.294);
		
		Composite GetNearMounts
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => Me.Location.Distance(Location) > 15, new Action(r => Navigator.MoveTo(Location))),
					 new Decorator(r => Me.Location.Distance(Location) < 15, new Action(r => Mount.Interact()))   
						
						
						);
			}
		}


		Composite MountUp
		{
			get
			{
				return new Decorator(r=>!Me.IsOnTransport,GetNearMounts);
			}
		}

		WoWUnit MyMount
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.CreatedByUnitGuid == Me.Guid); }
		}



		WoWUnit WhichNPC
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => Debuffs.ContainsKey(x.Entry) && !Me.HasAura((int)Debuffs[x.Entry])).OrderBy(u=>u.Distance).FirstOrDefault();
			}
		}

		Composite BuffUp
		{
			get
			{
				return new Decorator(r =>!Me.Combat && (!MyMount.ActiveAuras.ContainsKey("Defend") || ( MyMount.ActiveAuras.ContainsKey("Defend") && MyMount.ActiveAuras["Defend"].StackCount < 3)), new Action(r=>UsePetSkill("Defend")));
			}
		}


		private Composite BarkNpc
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => !Me.Combat && WhichNPC == null, 
						new Action(r => Navigator.MoveTo(Location))),
					new Decorator(r=>!Me.Combat && Me.Location.Distance(WhichNPC.Location)> 5, 
						new Action(r=>Navigator.MoveTo(WhichNPC.Location))),
					new Decorator(r=>!Me.Combat && Me.Location.Distance(WhichNPC.Location)<= 5, 
						new Sequence(
							new Action(r=>
							{
								WhichNPC.Target();
								WhichNPC.Interact();
							}),
							new Sleep(1000),
							new Action(ret => Lua.DoString("SelectGossipOption(1)"))
						)));
			}
		}




		private Composite Fight
		{
			get
			{
				return
					new Decorator(r => Me.Combat, new Action(r =>
																 {


																	 //Me.CurrentTarget.Face();
																	 //if (Me.CurrentTarget.Distance > 10)
																	 //   Navigator.MoveTo(Me.CurrentTarget.Location);

																	 //var moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, -15f);
																	 //var moveTo = WoWMathHelper.CalculatePointBehind(StyxWoW.Me.CurrentTarget.Location,Me.CurrentTarget.Rotation, -15f);
																	 var moveTo = WoWMathHelper.CalculatePointAtSide(StyxWoW.Me.CurrentTarget.Location, Me.CurrentTarget.Rotation, 20f,false);
																	 //var moveTo = WoWMathHelper.GetPointAt(Me.Location, 20,Me.Rotation,0);
																	 if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
																	 {
																		
																		 Navigator.MoveTo(moveTo);
																	 }
																	/* if (Me.CurrentTarget.Distance < 10)
																	 {
																		 Navigator.PlayerMover.Move(
																			 WoWMovement.MovementDirection.Backwards);
																	 }
																	 else
																	 {
																		 Navigator.PlayerMover.MoveStop();
																	 }*/


																	 if (!MyMount.ActiveAuras.ContainsKey("Defend") || (MyMount.ActiveAuras.ContainsKey("Defend") && MyMount.ActiveAuras["Defend"].StackCount < 3))
																	 {
																		 //Me.CurrentTarget.Face();
																		 UsePetSkill("Defend");
																		 UsePetSkill("Charge");
																	 }
																	 else
																	 {
																		 if (Me.CurrentTarget.Distance > 8)
																				Me.CurrentTarget.Face();
																		 using (StyxWoW.Memory.AcquireFrame())
																		 {
																			 UsePetSkill("Thrust");
																			 UsePetSkill("Charge");
																			 UsePetSkill("Shield-Breaker");
																		 }
																	 }
																 }
						))



					;
			}
		}

		Dictionary<uint,uint> Debuffs = new Dictionary<uint, uint>();
		 
		Composite LanceUp
		{
			get
			{
				return new Decorator(r => Me.Inventory.Equipped.MainHand.ItemInfo.Id != 46106 && Me.Inventory.Equipped.MainHand.ItemInfo.Id != 46070, new Action(r => BestLance().UseContainerItem()));
			}
		}

		Composite HealUp
		{
			get
			{
				return new Decorator(r => !Me.Combat && MyMount.HealthPercent < 50, new Action(r => UsePetSkill("Refresh Mount")));
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ??
				   (_root =
					new Decorator(ret => !_isBehaviorDone,
								  new PrioritySelector(DoneYet, LanceUp, MountUp, BuffUp, HealUp,BarkNpc,Fight, new ActionAlwaysSucceed())));
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
		private WoWItem mainhand;
		private WoWItem offhand;

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
				mainhand = Me.Inventory.Equipped.MainHand;

				if (mainhand.ItemInfo.EquipSlot != InventoryType.TwoHandWeapon)
				{
					offhand = Me.Inventory.Equipped.OffHand;
				}


				for (int i = 0; i < Enemy.Count(); i++)
				{
					Debuffs.Add(Enemy[i], EnemyDebuff[i]);
				}
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}
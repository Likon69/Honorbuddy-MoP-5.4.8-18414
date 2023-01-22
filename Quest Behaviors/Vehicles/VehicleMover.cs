// Behavior originally contributed by HighVoltz / revamp by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Summary and Documentation
// VEHICLEMOVER performs the following functions:
// * Looks for an identified vehicle in the nearby area and mounts it
//      The behavior seeks vehicles that have no players nearby.
// * Drives the vehicle to the given destination location or destination NPC
// * Optionally, casts a spell upon arrival at the destination.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      VehicleIdN [REQUIRED Count: 1]
//          Specifies the vehicle that should be mounted and driven back
//          This is the minimum distance the AvoidMobId must be from our safe spot.
//          On occasion, the 'same' vehicle can have multiple IDs.  These numbers
//          identify all the vehicles that can fulfill the need.
//      AuraId_ProxyVehicle [optional; Default: none]
//          If this value is specified, then then VehicleIdN uses a "Eye of Acherus"-like
//          mechanic (http://wowhead.com/npc=28511), instead of the normal WoWclient
//          "vehicle" mechanic.  For a "proxy vehicle like the Eye, there will be
//          no 'eject' button as you find in a normal WoWclient vehicle.
//          A proxy vehicle has the following characteristics:
//              + You are not 'mounted' or 'in a vehicle'--instead you have a particular aura
//              + The vehicle's location is calculated differently
//          The behavior will not find and enter proxy vehicles for you.  You must
//          arrange to use another behavior (e.g., InteractWith).
//      MobId [optional; Default: none]
//          Specifies an NPC to which the vehicle should be delivered.
//      SpellId [optional; Default: none]
//          This is the SpellId of the spell that should be cast when the vehicle
//          has been delivered to the destination.  The spell will be located
//          on the action bar provided by the vehicle.  But please note that
//          this is the _SpellId_, not the _ActionBarIndex_.
//      X/Y/Z [REQUIRED]
//          Specifies the destination location where the vehicle should be delivered.
//          If the vehicle is to be delivered to an NPC instead, this should specify
//          a location within 50 yards or so of where the NPC can be found.
//
// Quest binding:
//      QuestId [optional; Default: none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      CastNum [optional; Default: 1]
//          Behavior is considered 'done' when the spell has been cast this
//          number of times.
//          If there is an associated Quest or Quest Objectve, and it completes
//          this will also terminate the behavior.
//      CastTime [optional; Default: 1500ms]
//          The number of milliseconds we should wait after casting SpellId,
//          before any other actions are taken.
//      Hop [optional; Default: false]
//          This value serves as an 'unstuck' mechanic.  It forces the bot to jump
//          in the vehicle on its way to the destination.
//      IgnoreCombat [optional; Default: true]
//          If set to true, if we get in combat on the way to acquiring or delivering
//          the vehicle, it will be ignored, and we will doggedly pursue vehicle
//          acquisition and delivery.
//      NonCompeteDistance [optional; Default: 25.0]
//          When we acquire vehicles, we look for vehicles with no competing players
//          nearby.  This value determines the range at which a player will be considered
//          to be in competition for the vehicle of interest to us.
//      Precision [optional; Default 4.0]
//          As we move from waypoint to waypoint in our travel to the destination,
//          this value determines when we've reached the current waypoint, and can
//          proceed to the next.
//      WaitForVehicle [optional; Default: true]
//          This value affects what happens if there are no Vehicles in the immediate area.
//          If true, the behavior will stand and wait for VehicleIdN to respawn.
//          If false, and the behavior cannot locate VehicleIdN in the immediate area, the behavior
//          considers itself complete.
//
// THINGS TO KNOW:
// * The vehicle may provide an action bar with spells on it.
//      The SpellId is the Id of the spell (as you would look it up on WoWHead). 
//      The SpellId is _not_ the ActionBarIndex value (1-12).
// * An X/Y/Z must always be provided, even if the destination is an NPC (i.e. MobId).
//      We cannot "see" mobs if they are located too far away.  If the destination
//      is ultimately a mob, the X/Y/Z should be in an area within 50 yards or so
//      of the destination Mob.
// * All looting and harvesting is turned off while the event is in progress.
// * The PullDistance is set to 1 while this behavior is in progress.
// * This behavior consults the Quest Behaviors/Data/AuraIds_OccupiedVehicle.xml
//      file for a list of auras that identified which vehicles are occupied
//      and which are available for taking.
#endregion


#region FAQs
// * Do I need a separate InteractWith behavior to mount the vehicle?
//      No, VehicleMover is smart enough to pick and mount an appropriate vehicle
//      for return to the destination.
//
#endregion


#region Examples
// "Death Comes from On High" (http://wowhead.com/quest=12641)
// Drive the Eye of Acherus (http://wowhead.com/npc=28511) to each of four locations,
// and use the Siphon of Acherus spell (http://wowhead.com/spell=51859) upon arriving
// at the location.
// Since the Eye is a "proxy" vehicle, we must use the InteractWith behavior
// on the Eye of Acherus control mechanism (http://wowhead.com/object=191609).
//      <If Condition="!Me.HasAura(51852)"> <!-- HB start/stop protection -->
//          <CustomBehavior File="InteractWith" MobId = "191609" QuestId="12641"
//              ObjectType="GameObject" NumOfTimes = "1" CollectionDistance = "4"
//              X="2345.848" Y="-5696.338" Z="426.0303" />
//          <CustomBehavior File="WaitTimer" WaitTime="25000" />
//      </If>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="3"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1654.104" Y="-5996.521" Z="183.0229"/>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="1"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1799.286" Y="-6003.341" Z="170.4593"/>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="2"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1592.047" Y="-5735.208" Z="196.1772"/>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="4"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1384.774" Y="-5701.124" Z="199.2797"/>
//
// "The Hungry Ettin": Worgen starter quest (http://wowhead.com/quest=14416)
// Steal Mountain Horses (http://wowhead.com/npc=36540) and return them back
// to Lorna Crowley (http://wowhead.com/npc=36457).
//
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="14416"
//          VehicleID="36540" Precision="2" MobId="36457" X="-2093.622" Y="2259.525" Z="20.98417" />
//
// "Grand Theft Palomino": Death Knight starter quest (http://www.wowhead.com/quest=12680)
// Steal a Havenshire Stallion (http://wowhead.com/npc=28605), Havenshire Mare (http://wowhead.com/npc=28606),
// or Havenshire Colt (http://wowhead.com/npc=28607), and return it to Salanar the Horseman (http://wowhead.com/npc=28653).
// To complete the quest, we have to summon Salanar the Horseman by casting a spell when we arrive at
// the destination.  The spell is provided by the horse vehicle.
//
//      <CustomBehavior File="Vehicles\VehicleMover" VehicleId="28605" VehicleId2="28606" VehicleId3="28607"
//          MobId="28653" SpellId="52264" X="2347.104" Y="-5695.789" Z="155.9568" />
// 
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
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Vehicles.VehicleMover
{
	[CustomBehaviorFileName(@"Vehicles\VehicleMover")]
	public class VehicleMover : QuestBehaviorBase
	{
		#region Consructor and Argument Processing
		public VehicleMover(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Primary attributes...
				AuraId_ProxyVehicle = GetAttributeAsNullable<int>("AuraId_ProxyVehicle", false, ConstrainAs.SpellId, null) ?? 0;
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "MobID", "NpcId" });
				SpellId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, new[] { "SpellID" }) ?? 0;
				VehicleIds = GetNumberedAttributesAsArray<int>("VehicleId", 1, ConstrainAs.VehicleId, new[] { "VehicleID" });
				Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

				// Tunables...
				NumOfTimes = GetAttributeAsNullable<int>("CastNum", false, ConstrainAs.RepeatCount, null) ?? 1;
				CastTime = GetAttributeAsNullable<int>("CastTime", false, new ConstrainTo.Domain<int>(0, 30000), null) ?? 1500;
				Hop = GetAttributeAsNullable<bool>("Hop", false, null, null) ?? false;
				IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? true;
				/*unused*/ GetAttributeAsNullable<double>("Precision", false, new ConstrainTo.Domain<double>(2.0, 100.0), null);

				var useNavigator = GetAttributeAsNullable<bool>("UseNavigator", false, null, null);
				if (useNavigator.HasValue)
					{ MovementBy = useNavigator.Value ? MovementByType.NavigatorPreferred : MovementByType.ClickToMoveOnly; }

				WaitForVehicle = GetAttributeAsNullable<bool>("WaitForVehicle", false, null, null) ?? true;

				// For backward compatibility, we do not error off on an invalid SpellId, but merely warn the user...
				if ((1 <= SpellId) && (SpellId <= 12))
				{
					QBCLog.Error("SpellId of {0} is not valid--did you accidently provde an ActionBarIndex instead?", SpellId);
				}
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


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			UsageCheck_DeprecatedAttribute(xElement,
				Args.Keys.Contains("UseNavigator"),
				"UseNavigator",
				context => string.Format("Automatically converted UseNavigator=\"{0}\" attribute into MovementBy=\"{1}\"."
										+ "  Please update profile to use MovementBy, instead.",
										Args["UseNavigator"], MovementBy));
		}


		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// empty, for now...
		}


		// Attributes provided by caller
		public int AuraId_ProxyVehicle { get; private set; }
		public int CastTime { get; private set; }
		public WoWPoint Destination { get; private set; }
		public bool Hop { get; private set; }
		public bool IgnoreCombat { get; private set; }
		public int[] MobIds { get; private set; }
		public int NumOfTimes { get; private set; }
		public int SpellId { get; private set; }
		public int[] VehicleIds { get; private set; }
		private bool WaitForVehicle { get; set; }
		#endregion


		#region Private and Convenience variables
		private IEnumerable<int> AuraIds_OccupiedVehicle { get; set; }
		private int CastCounter { get; set; }
		private WoWPoint FinalDestination { get; set; }
		private string FinalDestinationName { get; set; }
		private bool DidSuccessfullyMount { get; set; }
		private WoWUnit VehicleUnoccupied { get; set; }

		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{       
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun =
				OnStart_QuestBehaviorCore(
					string.Format("Returning {0} to {1}",
						string.Join(", ", VehicleIds.Select(o => Utility.GetObjectNameFromId(o)).Distinct()),
						Destination));

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Disable any settings that may interfere with the escort --
				// When we escort, we don't want to be distracted by other things.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;
				CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

				AuraIds_OccupiedVehicle = GetOccupiedVehicleAuraIds();
			}
		}
		#endregion

		
		#region Main Behaviors
		protected override Composite CreateBehavior_CombatMain()
		{
			return new Decorator(context => !IsDone,
				new PrioritySelector(

					// Update values for this BT node visit...
					new Action(context =>
					{
						VehicleUnoccupied = FindUnoccupiedVehicle();

						// Figure out our final destination (i.e., a location or a mob)...
						// NB: this can change as we travel.  If our destination is a mob,
						// We can't "see" distant mobs until we get within 100 yards or so of them.
						// Until we close that distance, we'll head towards the provided location.
						// As soon as we "see" the mob, we'll switch to the mob as the destination.
						FinalDestination = Destination;
						FinalDestinationName = "destination";
					
						if (MobIds.Count() > 0)
						{
							// If we can see our destination mob, calculate a path to it...
							var nearestMob = Query.FindMobsAndFactions(MobIds).OrderBy(u => u.Distance).FirstOrDefault() as WoWUnit;
							if (nearestMob != null)
							{
								// Target destination mob as feedback to the user...
								Utility.Target(nearestMob);

								FinalDestination = nearestMob.Location;
                                FinalDestinationName = nearestMob.SafeName;
							}
						}

						return RunStatus.Failure;   // fall thru
					}),


					// Proceed if we're not in combat, or are ignoring it...
					new Decorator(context => !Me.Combat || IgnoreCombat,
						new PrioritySelector(
							// If we were successfully mounted...
							// and within a few yards of our destination when we were dismounted, we must
							// assume we were auto-dismounted, and the behavior is complete...
							new Decorator(context => DidSuccessfullyMount && !IsInVehicle()
														&& (WoWMovement.ActiveMover.Location.Distance(FinalDestination) < 15.0),
								new Action(context => { BehaviorDone(); })),

							// If we're not in a vehicle, go fetch one...
							new Decorator(context => !IsInVehicle() && Query.IsViable(VehicleUnoccupied),
								new Sequence(
									new CompositeThrottleContinue(
										Throttle.UserUpdate,
										new Action(context =>
										{
											TreeRoot.StatusText = string.Format("Moving to {0} {1}",
                                                                                VehicleUnoccupied.SafeName,
																				Me.Combat ? "(ignoring combat)" : "");
										})),
									new DecoratorContinue(context => VehicleUnoccupied.WithinInteractRange,
										new Action(context => { VehicleUnoccupied.Interact(); })),
									new ActionRunCoroutine(
									    interactUnitContext => UtilityCoroutine.MoveTo(
									        VehicleUnoccupied.Location,
									        VehicleUnoccupied.SafeName,
									        MovementBy))
								)),

							// If we can't find a vehicle, terminate if requested...
							new CompositeThrottle(
								context => !IsInVehicle() && !Query.IsViable(VehicleUnoccupied),
								Throttle.UserUpdate,
								new Action(context =>
								{
									if (!WaitForVehicle)
										{ BehaviorDone(string.Format("No Vehicle, and WaitForVehicle=\"{0}\"", WaitForVehicle)); }
									else
										{ TreeRoot.StatusText = "No vehicles in area--waiting for vehicle to become available."; }
								})),


							// Move vehicle to destination...
							new Decorator(context => IsInVehicle(),
								new PrioritySelector(
								   // If we successfully mounted the vehicle, record the fact...
									 new Decorator(context => !DidSuccessfullyMount,
										new Action(context => { DidSuccessfullyMount = true; })),

                                    new ActionRunCoroutine(
									    interactUnitContext => UtilityCoroutine.MoveTo(
									        FinalDestination,
									        FinalDestinationName,
									        MovementBy)),

									new Decorator(context => WoWMovement.ActiveMover.IsMoving,
										new Action(context => { WoWMovement.MoveStop(); })),

									// Arrived at destination, use spell if necessary...
									// NB: We want to make certain movement is settled before we attempt
									// to cast spell, so we won't be interrupted.
									new SleepForLagDuration(),
									CreateSpellBehavior()
								))
						)),

					// Squelch combat, if requested...
					new Decorator(context => IgnoreCombat,
						new ActionAlwaysSucceed())
				));
		}


		protected override Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateMainBehavior()
		{
			return new PrioritySelector(
				// If quest is done, behavior is done...
				new Decorator(context => IsDone,
					new Action(context => { BehaviorDone(); })),

				// If we've cast the spell the expected number of times, we're done...
				new Decorator(context => CastCounter >= NumOfTimes,
					new Action(context => { BehaviorDone(); }))
			);
		}
		#endregion

		
		#region Helpers

		private Composite CreateSpellBehavior()
		{
			string luaCastSpellCommand = string.Format("CastSpellByID({0})", SpellId);
			string luaCooldownCommand = string.Format("return GetSpellCooldown({0})", SpellId);
			string luaRetrieveSpellInfoCommand = string.Format("return GetSpellInfo({0})", SpellId);

			// If we have a spell to cast, one or more times...
			// NB: Since the spell we want to cast is associated with the vehicle, if we get auto-ejected
			// from the vehicle after we arrive at our destination, then there is no way to cast the spell.
			// If we get auto-ejected, we don't try to cast.
			return new Decorator(context => IsInVehicle() && (SpellId > 0),
				new PrioritySelector(
					// Stop moving so we can cast...
					new Decorator(context => WoWMovement.ActiveMover.IsMoving,
						new Action(context => { WoWMovement.MoveStop(); })),
						
					// If we cannot retrieve the spell info, its a bad SpellId...
					new Decorator(context => string.IsNullOrEmpty(Lua.GetReturnVal<string>(luaRetrieveSpellInfoCommand, 0)),
						new Action(context =>
						{
							QBCLog.Warning("SpellId({0}) is not known--ignoring the cast", SpellId);
							CastCounter = NumOfTimes +1; // force 'done'
						})),

					// If the spell is on cooldown, we need to wait...
					new Decorator(context => Lua.GetReturnVal<double>(luaCooldownCommand, 1) > 0.0,
						new Action(context => { TreeRoot.StatusText = "Waiting for cooldown"; } )),

					// Cast the required spell...
					new Sequence(
						new Action(context =>
						{
							WoWSpell wowSpell = WoWSpell.FromId(SpellId);
							TreeRoot.StatusText = string.Format("Casting {0}", (wowSpell != null) ? wowSpell.Name : string.Format("SpellId({0})", SpellId));

							// NB: we use LUA to cast the spell.  As some vehicle abilities cause
							// a "Spell not learned" error.  Apparently, HB only keeps up with
							// permanent spells known by the toon, and not transient spells that become
							// available in vehicles.
							Lua.DoString(luaCastSpellCommand);
							++CastCounter;

							// If we're objective bound, the objective needs to complete regardless of the counter...
							if ((QuestObjectiveIndex <= 0) && (CastCounter >= NumOfTimes))
								{ BehaviorDone(); }
						}),
						new WaitContinue(TimeSpan.FromMilliseconds(CastTime), context => false, new ActionAlwaysSucceed())
					)
				));
		}


		private WoWUnit FindUnoccupiedVehicle()
		{
			return
				(from wowObject in Query.FindMobsAndFactions(VehicleIds)
				 let wowUnit = wowObject as WoWUnit
				 where
					Query.IsViable(wowUnit)
					&& !wowUnit.Auras.Values.Any(aura => AuraIds_OccupiedVehicle.Contains(aura.SpellId))
					&& !Query.IsInCompetition(wowUnit, NonCompeteDistance)
					&& wowUnit.IsUntagged()
				 orderby wowUnit.Distance
				 select wowUnit)
				 .FirstOrDefault();
		}


		private bool IsInVehicle()
		{
			return
				Query.IsInVehicle()
				|| Me.HasAura(AuraId_ProxyVehicle);
		}
		#endregion
	}
}

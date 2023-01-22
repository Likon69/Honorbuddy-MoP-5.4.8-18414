// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation

// QUICK DOX:
//		This behavior is designed for NPCs that have an immunity/shield 
//		ability that needs to be broken by killing other NPCs nearby
//
// BEHAVIOR ATTRIBUTES:
//      MobIdN [REQUIRED] 
//			Ids of the mobs that have an immunity/shield ability
//      ImmunityBreakingMobIdN [REQUIRED] 
//          Ids of the mobs that need to be killed close to the mob with immunity/shield in order to break the shield
//      ImmunityAuraId [REQUIRED] 
//          The aura id of the shield
//		MaxRange [Optional; Default: 8]
//			The maximum range from the shielded mob that the immunity breaking mob need to be killed within
//		X, Y, Z [REQUIRED]
//			The encounter location.
// THINGS TO KNOW:
//

#endregion

#region Examples

// EXAMPLE:
//     <CustomBehavior File="Misc\BreakImmunityByKillingMobsInCloseProximity" MobId="2120" 
//			ImmunityAuraId="87251" ImmunityBreakingNpcIds="45766" X="-135.444" Y="1071.23" Z="66.07568" />

#endregion

#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy.Helpers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.Plugins;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion

namespace Honorbuddy.Quest_Behaviors.BreakImmunityByKillingMobsInCloseProximity
{
	[CustomBehaviorFileName(@"Misc\BreakImmunityByKillingMobsInCloseProximity")]
	public class BreakImmunityByKillingMobsInCloseProximity : QuestBehaviorBase
	{
		#region Constructor and Argument Processing

		public BreakImmunityByKillingMobsInCloseProximity(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				// Primary attributes...
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] {"NpcId"});
				if (MobIds != null && !MobIds.Any())
					MobIds = GetAttributeAsArray<int>("MobIds", true, ConstrainAs.MobId, new[] {"NpcIds"}, null);

				ImmunityBreakingMobIds = GetNumberedAttributesAsArray<int>(
					"ImmunityBreakingMobId",
					0,
					ConstrainAs.MobId,
					new[] {"ImmunityBreakingNpcId"});
				if (ImmunityBreakingMobIds != null && !ImmunityBreakingMobIds.Any())
					ImmunityBreakingMobIds = GetAttributeAsArray<int>(
						"ImmunityBreakingMobIds",
						true,
						ConstrainAs.MobId,
						new[] {"ImmunityBreakingNpcIds"},
						null);

				SearchLocation = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;

				ImmunityAuraId = GetAttributeAsNullable<int>("ImmunityAuraId", true, ConstrainAs.AuraId, null) ?? 0;
				MaxRange = GetAttributeAsNullable<double>("MaxRange", false, new ConstrainTo.Domain<double>(0, 40), null) ?? 8;
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


		// Variables for Attributes provided by caller

		private int[] MobIds { get; set; }

		private int[] ImmunityBreakingMobIds { get; set; }

		private double MaxRange { get; set; }

		private WoWPoint SearchLocation { get; set; }

		private int ImmunityAuraId { get; set; }

		#endregion

		#region Private and Convenience variables

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		WoWUnit SelectedNpc { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId
		{
			get { return "$Id$"; }
		}

		public override string SubversionRevision
		{
			get { return "$Rev$"; }
		}

		#endregion

		#region Overrides of QuestBehaviorBase

		private Composite _root;

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//  
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin));
		}


		protected override Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

		private async Task<bool> MainCoroutine()
		{
			// break if we are done or we are not in combat and targting is not empty, we want the botbase to clear path for us.
			if (IsDone || (!Me.Combat && Targeting.Instance.FirstUnit != null) || !Me.IsAlive)
				return false;

			if (!Query.IsViable(SelectedNpc))
				SelectedNpc = GetNpc();

			if (!Query.IsViable(SelectedNpc) || !Me.IsActuallyInCombat && Targeting.Instance.FirstUnit == null)
			{
				// move to search area
				if (SearchLocation != WoWPoint.Zero && !Navigator.AtLocation(SearchLocation))
				{
					await UtilityCoroutine.MoveTo(SearchLocation, "Search Area", MovementBy);
				}
				// Dismount after reaching search location.
				else if ((SearchLocation == WoWPoint.Zero || Navigator.AtLocation(SearchLocation)) && Me.Mounted)
				{
					await UtilityCoroutine.ExecuteMountStrategy(MountStrategyType.DismountOrCancelShapeshift);
				}
				else
				{
					TreeRoot.StatusText = "Waiting for NPC to spawn";
				}
				return true;
			}

			if (SelectedNpc.IsDead && SelectedNpc.TaggedByMe && QuestId == 0)
			{
				BehaviorDone();
				return true;
			}

			if (SelectedNpc.HasAura(ImmunityAuraId))
			{
				if (BotPoi.Current.AsObject == SelectedNpc)
					BotPoi.Clear("Mob is immune");

				var targetedMob = Targeting.Instance.FirstUnit;
				if (targetedMob != null && ImmunityBreakingMobIds.Contains((int)targetedMob.Entry) )
				{

					if (targetedMob.IsTargetingMeOrPet)
					{
						// move close enough to shielded NPC so that the exploding mobs will hit it when killed.
						var myMinDistance = Math.Max(2, MaxRange - targetedMob.MeleeRange());
						if (SelectedNpc.DistanceSqr > myMinDistance*myMinDistance)
						{
                            TreeRoot.StatusText = string.Format("Moving closer to {0} before killing {1}", SelectedNpc.SafeName, targetedMob.SafeName);
							Navigator.MoveTo(SelectedNpc.Location);
							return true;
						}
						// wait for exploding mob to get within range of shielded mob.
						if (targetedMob.Location.DistanceSqr(SelectedNpc.Location) > MaxRange*MaxRange)
						{
							TreeRoot.StatusText = string.Format(
								"Waiting for {0} to move withing range of {1}",
                                targetedMob.SafeName,
                                SelectedNpc.SafeName);
							return true;
						}
					}
				}
			}
			return false;
		}

		protected override void TargetFilter_IncludeTargets(
			List<WoWObject> incomingWowObjects,
			HashSet<WoWObject> outgoingWowObjects)
		{
			// don't add targets while flying because core will dismount before reaching search area.
			if (Me.IsFlying && !Me.IsActuallyInCombat || !Query.IsViable(SelectedNpc))
				return;

			if (!SelectedNpc.HasAura(ImmunityAuraId) )
			{
				outgoingWowObjects.Add(SelectedNpc);
				return;
			}

			if (!Me.IsActuallyInCombat)
				return;

			foreach (var unit in incomingWowObjects.OfType<WoWUnit>())
			{
				if (ImmunityBreakingMobIds.Contains((int) unit.Entry))
					outgoingWowObjects.Add(unit);
			}
		}

		protected override void TargetFilter_RemoveTargets(List<WoWObject> wowObjects)
		{
			var npcIsImmune = Query.IsViable(SelectedNpc) && SelectedNpc.HasAura(ImmunityAuraId);
			
			if (!npcIsImmune)
				return;
			
			wowObjects.RemoveAll(
				o =>
				{
					// remove selected npc from targeting if it's immune.
					var unit = (WoWUnit) o;
					if (unit == SelectedNpc)
						return true;

					return false;
				});
		}

		#endregion

		#region Utility

		private WoWUnit GetNpc()
		{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
						.Where(
							u => u.IsAlive && MobIds.Contains(((int) u.Entry))
								&& (u.ThreatInfo.ThreatStatus > ThreatStatus.UnitNotInThreatTable || !Query.IsInCompetition(u, NonCompeteDistance)))
						.OrderBy(u => u.DistanceSqr)
						.FirstOrDefault();
		}

		#endregion
	}
}
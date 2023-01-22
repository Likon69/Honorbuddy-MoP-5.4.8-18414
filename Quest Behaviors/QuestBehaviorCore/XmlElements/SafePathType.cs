// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Styx;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
	public class SafePathType : QuestBehaviorXmlBase
	{
		public enum StrategyType
		{
			WaitForAvoidDistance,
			StalkMobAtAvoidDistance
		}

		public SafePathType(XElement xElement, double defaultEgressDistance)
			: base(xElement)
		{
			try
			{
				DismissPet = GetAttributeAsNullable<bool>("DismissPet", false, null, null) ?? false;
				EgressDistance = GetAttributeAsNullable<double>("EgressDistance", false, null, null)
									?? defaultEgressDistance;
				Strategy = GetAttributeAsNullable<StrategyType>("Strategy", false, null, null)
							?? StrategyType.StalkMobAtAvoidDistance;

				Waypoints = new List<WaypointType>();
				foreach (XElement childElement in xElement.Elements().Where(elem => (elem.Name == "Hotspot")))
				{
					var waypoint = new WaypointType(childElement);

					if (!waypoint.IsAttributeProblem)
						Waypoints.Add(waypoint);

					IsAttributeProblem |= waypoint.IsAttributeProblem;
				}

				HandleAttributeProblem();
			}

			catch (Exception except)
			{
				if (Query.IsExceptionReportingNeeded(except))
					QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
				IsAttributeProblem = true;
			}
		}

		public bool DismissPet { get; private set; }
		public double EgressDistance { get; private set; }
		public StrategyType Strategy { get; private set; }
		public IList<WaypointType> Waypoints { get; private set; }


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "SafePath";

			var root = new XElement(elementName,
			                        new XAttribute("DismissPet", DismissPet),
			                        new XAttribute("EgressDistance", EgressDistance),
			                        new XAttribute("Strategy", Strategy));

			foreach (var waypoint in Waypoints)
				root.Add(waypoint.ToXml("Hotspot"));

			return root;
		}
		#endregion


		public void DismissPetIfNeeded()
		{
			if (DismissPet && StyxWoW.Me.GotAlivePet)
				PetControl.PetDismiss();
		}


		/// <summary>
		/// <para>Returns an egress path, which is simply a reversal of the path we used to ingress.
		/// If MOBTOAVOID is provided, we use the mob's location to determine our starting point
		/// in the egress path.</para>
		/// <para>This method never returns null, but may return an empty queue if there is no
		/// egress path.</para>
		/// </summary>
		/// <param name="mobToAvoid"></param>
		/// <returns></returns>
		public Queue<WaypointType> FindPath_Egress(WoWUnit mobToAvoid)
		{
			var theWayOut = new List<WaypointType>(Waypoints);

			theWayOut.Reverse();

			var egressStartPoint =
				(from waypoint in theWayOut
				 let mobDistanceToPoint = (mobToAvoid != null)
											  ? waypoint.Location.Distance(mobToAvoid.Location)
											  : float.MaxValue
				 let myDistanceToPoint = waypoint.Location.Distance(StyxWoW.Me.Location)
				 where
					 myDistanceToPoint < mobDistanceToPoint
				 orderby
					 myDistanceToPoint
				 select waypoint)
				.FirstOrDefault();

			while (theWayOut[0] != egressStartPoint)
				theWayOut.RemoveAt(0);

			return new Queue<WaypointType>(theWayOut);
		}


		/// <summary>
		/// <para>Returns an ingress path to the destination.</para>
		/// <para>This method never returns null, but may return an empty queue if there is no
		/// egress path.</para>
		/// </summary>
		/// <returns></returns>
		public Queue<WaypointType> FindPath_Ingress()
		{
			var theWayIn = new List<WaypointType>(Waypoints);

			var ingressStartPoint =
			   (from waypoint in theWayIn
				let myDistanceToPoint = waypoint.Location.Distance(StyxWoW.Me.Location)
				orderby
					myDistanceToPoint
				select waypoint)
				.FirstOrDefault();

			while (theWayIn[0] != ingressStartPoint)
				theWayIn.RemoveAt(0);

			return new Queue<WaypointType>(theWayIn);
		}


		// 11Apr2013-04:42UTC chinajade
		public static SafePathType GetOrCreate(XElement parentElement, string elementName, double defaultEgressDistance, WoWPoint? safespotLocation = null)
		{
			if (safespotLocation.HasValue
				&& ((safespotLocation.Value == WoWPoint.Empty) || safespotLocation.Value == WoWPoint.Zero))
			{
				safespotLocation = null;
			}

			var safePath = new SafePathType(parentElement
												.Elements()
												.DefaultIfEmpty(new XElement(elementName))
												.FirstOrDefault(elem => (elem.Name == elementName)),
											  defaultEgressDistance);

			if (!safePath.IsAttributeProblem)
			{
				// If user didn't provide a HuntingGrounds, and he provided a default center point, add it...
				if (!safePath.Waypoints.Any() && safespotLocation.HasValue)
					safePath.Waypoints.Add(new WaypointType(safespotLocation.Value, "safe spot", 7.0));

				if (!safePath.Waypoints.Any())
				{
					QBCLog.Error("Neither the X/Y/Z attributes nor the <{0}> sub-element has been specified.",
								elementName);
					safePath.IsAttributeProblem = true;
				}
			}

			return safePath;
		}
	}
}

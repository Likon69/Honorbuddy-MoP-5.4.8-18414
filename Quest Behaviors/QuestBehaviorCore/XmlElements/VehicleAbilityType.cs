// Originally contributed by HightVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

using System;
using System.Linq;
using System.Xml.Linq;

using Styx;
using Styx.CommonBot.Bars;
using Styx.CommonBot.Profiles;
using Styx.Helpers;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{

    public class VehicleAbilityType : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing
        public VehicleAbilityType(XElement xElement)
            : base(xElement)
        {
            try
            {
				ButtonIndex = GetAttributeAsNullable<int>("ButtonIndex", true, new ConstrainTo.Domain<int>(1, 12), null) ?? 1;
				TargetingType = GetAttributeAsNullable<AbilityTargetingType>("TargetingType", false, null, null) ?? AbilityTargetingType.Vehicle;
				IgnoreLoSToTarget = GetAttributeAsNullable<bool>("IgnoreLoSToTarget", false, null, null) ?? false;
				UseWhenExpression = GetAttributeAs<string>("UseWhen", false, ConstrainAs.StringNonEmpty, null) ?? "true";
	            UseWhenPredicateFunc = QuestBehaviorBase.CompileAttributePredicateExpression("UseWhen", UseWhenExpression);
				if (UseWhenPredicateFunc == null)
					IsAttributeProblem = true; 

                HandleAttributeProblem();
            }
            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

	    public VehicleAbilityType(
		    int abilityIndex,
			AbilityTargetingType targetingType = AbilityTargetingType.Vehicle,
			bool ignoreLosToTarget = false,
			string useWhenExpression = "true")
		{
				ButtonIndex = abilityIndex;
				TargetingType = targetingType;
				IgnoreLoSToTarget = ignoreLosToTarget;
				UseWhenExpression = useWhenExpression ?? "true";
				UseWhenPredicateFunc = QuestBehaviorBase.CompileAttributePredicateExpression("UseWhen", UseWhenExpression);
		}


        #endregion


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "VehicleAbility";

			return new XElement(elementName,
							 new XAttribute("ButtonIndex", ButtonIndex),
							 new XAttribute("TargetingType", TargetingType),
							 new XAttribute("UseWhen", UseWhenExpression));
		}

		public int ButtonIndex { get; private set; }
		public AbilityTargetingType TargetingType { get; set; }
		public Func<bool> UseWhenPredicateFunc { get; private set; }
		public bool IgnoreLoSToTarget { get; private set; }
		private string UseWhenExpression { get; set; }

	    private PerFrameCachedValue<SpellActionButton> _ability;
	    public SpellActionButton Ability
	    {
			get
			{
				return _ability ?? (_ability = new PerFrameCachedValue<SpellActionButton>(
					() =>
					{
						if (!Query.IsVehicleActionBarShowing())
							return null;
						return ActionBar.Active.Buttons.FirstOrDefault(b => b.Index == ButtonIndex) as SpellActionButton;
					}));
			}
	    }

		#endregion


		#region Private and Convenience variables

        #endregion
		
    }
}

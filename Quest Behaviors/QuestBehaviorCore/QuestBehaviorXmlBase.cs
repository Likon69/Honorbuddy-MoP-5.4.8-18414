// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Styx;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public abstract class QuestBehaviorXmlBase
    {
        protected QuestBehaviorXmlBase(XElement xElement)
        {
            Element = xElement;
            Attributes =
                (xElement == null)
                ? new Dictionary<string, string>()
                : xElement
                    .Attributes()
                    .ToDictionary(attribute => attribute.Name.ToString(), attribute => attribute.Value);
        }


        protected QuestBehaviorXmlBase()
        {
            Element = null;
            Attributes = new Dictionary<string, string>();
        }

        public Dictionary<string, string> Attributes { get; private set; }
        public XElement Element { get; private set; }


		public override string ToString()
		{
			return ToXml().ToString();
		}


		#region Concrete class required implementations...
		public virtual string SubversionId { get { return "$Id$"; } }
		public virtual string SubversionRevision { get { return "$Rev$"; } }

		public abstract XElement ToXml(string elementName = null);
		#endregion


		public static class ConstrainAs
        {
            public static readonly IConstraintChecker<int>       AuraId              = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<int>       CollectionCount     = new ConstrainTo.Domain<int>(1, 1000);
            public static readonly IConstraintChecker<int>       FactionId           = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<int>       HotbarButton        = new ConstrainTo.Domain<int>(1, 12);
            public static readonly IConstraintChecker<int>       ItemId              = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<int>       Milliseconds        = new ConstrainTo.Domain<int>(0, int.MaxValue);
            public static readonly IConstraintChecker<int>       MobId               = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<int>       ObjectId            = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<double>    Percent             = new ConstrainTo.Domain<double>(0.0, 100.0);
            public static readonly IConstraintChecker<int>       QuestId             = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<double>    Range               = new ConstrainTo.Domain<double>(1.0, 10000.0);
            public static readonly IConstraintChecker<int>       RepeatCount         = new ConstrainTo.Domain<int>(1, 1000);
            public static readonly IConstraintChecker<int>       SpellId             = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<string>    StringNonEmpty      = new ConstrainTo.NonEmptyString<string>();
            public static readonly IConstraintChecker<int>       VehicleId           = new ConstrainTo.Domain<int>(1, int.MaxValue);
            public static readonly IConstraintChecker<WoWPoint>  WoWPointNonEmpty    = new ConstrainTo.NonEmptyWoWPoint<WoWPoint>();
        }


        public abstract class   IConstraintChecker<T>
        {
            public virtual string   Check(string attributeName,  T value)
            {
                return null;
            }
        }


        public static class ConstrainTo
        {
            public class        Anything<T>     : IConstraintChecker<T>
            {
                public override string      Check(string attributeName,  T value)
                {
                    return (null);
                }
            }

            public class        Domain<T>       : IConstraintChecker<T>
            {
                public Domain(T minValue,  T maxValue)
                {
                    _maxValue = maxValue;
                    _minValue = minValue;
                }

                private readonly T  _maxValue;
                private readonly T  _minValue;

                public override string      Check(string attributeName,  T value)
                {
                    bool    isOverRange     = (Comparer<T>.Default.Compare(value, _maxValue) > 0);
                    bool    isUnderRange    = (Comparer<T>.Default.Compare(value, _minValue) < 0);

                    if (isUnderRange || isOverRange)
                    {
                        return (string.Format("The '{0}' attribute's value (saw '{1}') is not "
                                              + "on the closed interval [{2}..{3}].",
                                              attributeName, value, _minValue, _maxValue));
                    }

                    return (null);
                }
            }

            public class        NonEmptyString<T>   : IConstraintChecker<string>
            {
                public override string      Check(string attributeName,  string value)
                {
                    if (!string.IsNullOrEmpty(value))
                        { return (null); }

                    return (string.Format("The '{0}' attribute's value may not be an empty string (\"\").",
                                            attributeName));
                }
            }

            public class        NonEmptyWoWPoint<T>  : IConstraintChecker<WoWPoint>
            {
                public override string      Check(string attributeName,  WoWPoint value)
                {
                    if (value != WoWPoint.Empty)
                        { return (null); }

                    return (string.Format("The '{0}' attribute's value may not be the empty WoWPoint (e.g., {1}).",
                                            attributeName, WoWPoint.Empty));
                }
            }


            public class        SpecificValues<T>   : IConstraintChecker<T>
            {
                public SpecificValues(T[] allowedValues)
                {
                    _allowedValues = allowedValues;
                }

                private readonly T[]    _allowedValues;

                public override string      Check(string attributeName,  T value)
                {
                    if (_allowedValues.Contains(value))
                        { return (null); }

                    Array.Sort(_allowedValues);

                    string[]    allowedValuesAsString   =  Array.ConvertAll(_allowedValues, t => t.ToString());

                    return (string.Format("The '{1}' attribute's value (saw '{2}') is not "
                                          + "one of the allowed values...{0}    [{3}].",
                                          Environment.NewLine, attributeName, value, ("'" + string.Join("', '", allowedValuesAsString) + "'")));
                }
            }
        }


        public T GetAttributeAs<T>( string attributeName,  bool isAttributeRequired,  IConstraintChecker<T> constraints,  string[] attributeNameAliases)
            where T: class
        {
            return ((T)UtilGetAttributeAs<T>(attributeName, isAttributeRequired, constraints, attributeNameAliases));
        }


        public T[] GetAttributeAsArray<T>( string attributeName,  bool isAttributeRequired,  IConstraintChecker<T> constraints,  string[] attributeNameAliases,
            char[] separatorCharacters)
        {
            // WoWPoint are triples, so requires special handling...
            if (typeof(T) == typeof(WoWPoint))
                { return ((T[])UtilGetAttributeAsWoWPoints(attributeName, isAttributeRequired, attributeNameAliases)); }


            constraints         = constraints ?? new ConstrainTo.Anything<T>();
            separatorCharacters = separatorCharacters ?? new [] { ' ', ',', ';' };

            bool        isError         = false;
            string      keyName         = UtilLocateKey(isAttributeRequired, attributeName, attributeNameAliases);
            var         resultList      = new List<T>();

            if ((keyName == null) || !Attributes.ContainsKey(keyName))
            {
                resultList.Clear();
                return (resultList.ToArray());
            }

            // We 'continue' even if problems are encountered...
            // By doing this, the profile writer can see all his mistakes at once, rather than being
            // nickel-and-dimed to death with error messages.
            foreach (string listEntry in Attributes[keyName].Split(separatorCharacters, StringSplitOptions.RemoveEmptyEntries))
            {
                T           tmpResult;

                try
                    { tmpResult = UtilTo<T>(keyName, listEntry); }

                catch (Exception)
                {
                    isError = true;
                    continue;
                }

                string  constraintViolationMessage  = constraints.Check(keyName, tmpResult);
                if (constraintViolationMessage != null)
                {
                    QBCLog.Error(constraintViolationMessage);
                    isError = true;
                    continue;
                }

                resultList.Add(tmpResult);
            }

            if (isError)
            {
                resultList.Clear();
                IsAttributeProblem = true;
            }

            return (resultList.ToArray());
        }


        public T? GetAttributeAsNullable<T>( string attributeName,  bool isAttributeRequired,  IConstraintChecker<T> constraints,  string[] attributeNameAliases)
            where T: struct
        {
            return ((T?)UtilGetAttributeAs<T>(attributeName, isAttributeRequired, constraints, attributeNameAliases));
        }


        public T[] GetNumberedAttributesAsArray<T>( string baseName,  int countRequired,  IConstraintChecker<T> constraints,  IEnumerable<string> aliasBaseNames)
        {
            bool    isError         = false;
            bool    isWoWPoint      = (typeof(T) == typeof(WoWPoint));
            var     resultList      = new List<T>();

            // Search for primary names first --
            // We 'continue' even if problems are encountered.  By doing this, the profile writer can see
            // all his mistakes at once, rather than being nickel-and-dimed to death with error messages.
            var primaryAttributeNames = from attributeName in Attributes.Keys
                                        where UtilIsNumberedAttribute(baseName, attributeName, isWoWPoint)
                                        select attributeName;
            
            foreach (var numberedAttributeName in primaryAttributeNames)
            {
                isError |= UtilAddToNumberedAttributeToArray<T>(numberedAttributeName, constraints, resultList);
            }

            // Search using alias names --
            // We 'continue' even if problems are encountered.  By doing this, the profile writer can see
            // all his mistakes at once, rather than being nickel-and-dimed to death with error messages.
            if (aliasBaseNames != null)
            {
                var aliasAttributeNames = from aliasBaseName in aliasBaseNames
                                          from attributeName in Attributes.Keys
                                          where UtilIsNumberedAttribute(aliasBaseName, attributeName, isWoWPoint)
                                          select attributeName;

                foreach (var numberedAttributeName in aliasAttributeNames)
                {
                    isError |= UtilAddToNumberedAttributeToArray<T>(numberedAttributeName, constraints, resultList);
                }
            }


            if (resultList.Count < countRequired)
            {
                QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                    "The attribute '{1}N' must be provided at least {2} times (saw it '{3}' times).{0}"
                    + "(E.g., ButtonText1, ButtonText2, ButtonText3, ...){0}"
                    + "Please modify to supply {2} attributes with a base name of '{1}'.",
                    Environment.NewLine,
                    baseName,
                    countRequired,
                    resultList.Count));
                isError = true;
            }


            if (isError)
            {
                resultList.Clear();
                IsAttributeProblem = true;
            }

            return (resultList.ToArray());
        }


        // Returns true if error, false if success
        private bool        UtilAddToNumberedAttributeToArray<T>(string nameFound,
                                                                 IConstraintChecker<T> constraints,
                                                                 List<T> resultList)
        {
            string  baseName    = nameFound;

            // WoWPoints require special handling because there are three attributes comprising the value...
            if (typeof(T) == typeof(WoWPoint))
            {
                // Adjust basename to remove X/Y/Z suffix...
                if ((nameFound.EndsWith("X") || nameFound.EndsWith("Y") || nameFound.EndsWith("Z")))
                    { baseName = nameFound.Substring(0, nameFound.Length -1); }

                // If this is not the "X" key, and the "X" key exists, then skip processing...
                // If we don't, then the WoWPoint will be placed into the list three times.
                if (!nameFound.EndsWith("X") && Attributes.ContainsKey(baseName + "X"))
                    { return (false); }
            }

            object  tmpResult = UtilGetAttributeAs<T>(baseName, false, constraints, null);

            if (tmpResult == null)
                { return (true); }

            resultList.Add((T)tmpResult);
            return (false);
        }


        private object UtilGetAttributeAs<T>(string attributeName,  bool isAttributeRequired,  IConstraintChecker<T> constraints,  string[] attributeNameAliases)
        {
            Type        concreteType    = typeof(T);

            // WoWPoint are a triple of attributes, so requires special handling...
            if (concreteType == typeof(WoWPoint))
                { return (UtilGetXYZAttributesAsWoWPoint(attributeName, isAttributeRequired, attributeNameAliases)); }


            constraints = constraints ?? new ConstrainTo.Anything<T>();

            string keyName = UtilLocateKey(isAttributeRequired, attributeName, attributeNameAliases);

            if ((keyName == null) || !Attributes.ContainsKey(keyName))
                { return (null); }

            T           tmpResult;
            string      valueAsString   = Attributes[keyName];

            try
                { tmpResult = UtilTo<T>(keyName, valueAsString); }
            catch (Exception)
            {
                IsAttributeProblem = true;
                return (null);
            }

            string  constraintViolationMessage  = constraints.Check(keyName, tmpResult);
            if (constraintViolationMessage != null)
            {
                QBCLog.Error(constraintViolationMessage);
                IsAttributeProblem = true;
                return (null);
            }

            return (tmpResult);
        }


        private object UtilGetAttributeAsWoWPoints(string attributeName, bool isAttributeRequired, string[] attributeNameAliases)
        {
            bool            isError                 = false;
            string          keyName                 = UtilLocateKey(isAttributeRequired, attributeName, attributeNameAliases);
            List<WoWPoint>  pointList               = new List<WoWPoint>();
            char[]          separatorCoordinate     = { ' ', ',' };
            char[]          separatorTriplet        = { '|', ';' };


            if ((keyName == null) || !Attributes.ContainsKey(keyName))
            {
                pointList.Clear();
                return (pointList.ToArray());
            }


            foreach (string tripletAsString in Attributes[keyName].Split(separatorTriplet, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] coordinatesAsString = tripletAsString.Split(separatorCoordinate, StringSplitOptions.RemoveEmptyEntries);

                if (coordinatesAsString.Length != 3)
                {
                    QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                        "The '{1}' attribute's value contribution (saw '{2}')"
                        + " doesn't have three coordinates (counted {3}).{0}"
                        + "Expect entries of the form \"x1,y1,z1 | x2,y2,z2 | x3,...\", or \"x1,y1,z1; x2,y2,z2; x3,...\"",
                        Environment.NewLine,
                        keyName,
                        tripletAsString,
                        coordinatesAsString.Length));
                    isError = true;
                    continue;
                }

                double? tmpValueX = null;
                try { tmpValueX = UtilTo<double>(keyName, coordinatesAsString[0]); }
                catch(Exception) { isError = true; }

                double? tmpValueY  = null;
                try { tmpValueY = UtilTo<double>(keyName, coordinatesAsString[1]); }
		        catch(Exception) { isError = true; }
                    
                double? tmpValueZ = null;
                try { tmpValueZ = UtilTo<double>(keyName, coordinatesAsString[2]); }
                catch(Exception) { isError = true; }

                if (tmpValueX.HasValue && tmpValueY.HasValue && tmpValueZ.HasValue)
                    { pointList.Add(new WoWPoint(tmpValueX.Value, tmpValueY.Value, tmpValueZ.Value)); }
            }

            if (isError)
            {
                pointList.Clear();
                IsAttributeProblem = true;
            }

            return (pointList.ToArray());
        }


        private object UtilGetXYZAttributesAsWoWPoint(string attributeBaseName, bool isAttributeRequired, string[] attributeBaseNameAliases)
        {
            if (attributeBaseName == null)
                { attributeBaseName = ""; }

            // Note, don't use the IsAttributeProblem property for any decision making in this method--
            // Recall that the property could already be set on entry to this method.

            // Build the aliases from the provided alias base names --
            string[] attributeXNameAliases = null;
            string[] attributeYNameAliases = null;
            string[] attributeZNameAliases = null;

            if (attributeBaseNameAliases != null)
            {
                attributeXNameAliases = (from aliasBaseName in attributeBaseNameAliases
                                         select (aliasBaseName + "X")).ToArray();
                attributeYNameAliases = (from aliasBaseName in attributeBaseNameAliases
                                         select (aliasBaseName + "Y")).ToArray();
                attributeZNameAliases = (from aliasBaseName in attributeBaseNameAliases
                                         select (aliasBaseName + "Z")).ToArray();
            }

            // We search for the keys explictly such that UtilLocateKey will auto-discover all the possible keys
            // We have to search for all three components, in case the profile writer omitted one or more
            // contributions.
            // Note that we don't handle "is required" here, we defer it until later.
            string keyNameX = UtilLocateKey(false, attributeBaseName + "X", attributeXNameAliases);
            string keyNameY = UtilLocateKey(false, attributeBaseName + "Y", attributeYNameAliases);
            string keyNameZ = UtilLocateKey(false, attributeBaseName + "Z", attributeZNameAliases);

            string keyBase = keyNameX ?? keyNameY ?? keyNameZ;

            if (keyBase != null)
            {
                keyBase = keyBase.Substring(0, keyBase.Length - 1);   // strip off the "X"-"Y"-"Z"

                // Since one of the keys was found, that makes all of them 'required' as a set--
                isAttributeRequired = true;
            }
            else
            {
                // None of the keys were found--
                // Since we suppressed the 'is required' error messages in the initial hunt for the keys,
                // we build what we expect, and allow the error messages to be generated this time.
                keyBase = attributeBaseName;
            }

            // Attribute found, process it...
            var x = (double?)UtilGetAttributeAs<double>(keyBase + "X", isAttributeRequired, null, null);
            var y = (double?)UtilGetAttributeAs<double>(keyBase + "Y", isAttributeRequired, null, null);
            var z = (double?)UtilGetAttributeAs<double>(keyBase + "Z", isAttributeRequired, null, null);

            if (x.HasValue && y.HasValue && z.HasValue)
                { return (new WoWPoint(x.Value, y.Value, z.Value)); }

            return (null);
        }


        // Return true if attibuteName is composed of baseName plus an integer, or consists just of baseName.
        //              (baseName)   (attributeName)     (return value)
        // Examples (allowWoWPointSuffixes = false):
        //              MobId           MobId               true
        //              MobId           MobId27             true
        //              MobId           MobId27n            false
        //              MobId           MobIds              false
        // WoWPoint Examples (allowWoWPointSuffixes = true):
        //              (empty)         X                   true
        //              (empty)         X27                 false
        //              (empty)         27X                 true
        //              (empty)         X27n                false
        //              (empty)         Xs                  false
        //              Stand           StandX              true
        //              Stand           StandY27            false
        //              Stand           Stand27Y            true
        //              Stand           StandY27n           false
        //              Stand           StandZs             false        
        private bool UtilIsNumberedAttribute(string baseName, string attributeName, bool   allowWoWPointSuffixes)
        {
            if (!attributeName.StartsWith(baseName))
                { return (false); }

            string suffix = attributeName.Substring(baseName.Length);

            // Try to convert the suffix to an integral number.  If we fail, no match...
            if (allowWoWPointSuffixes)
            {
                if (suffix.EndsWith("X") || suffix.EndsWith("Y") || suffix.EndsWith("Z"))
                    { suffix = suffix.Substring(0, suffix.Length -1); }
            }

            // If the attributeName exactly matches the baseName, we consider it a 'match'...
            if (suffix.Length == 0)
                { return (true); }

            int tmpResult;

            if (int.TryParse(suffix, out tmpResult))
                { return (true); }

            return (false);
        }


        private T UtilTo<T>(string  attributeName, string  attributeValueAsString)
        {
            Type    concreteType    = typeof(T);

            // Booleans require special handling...
            if (concreteType == typeof(bool))
            {
                int tmpInt;

                if (int.TryParse(attributeValueAsString, out tmpInt))
                {
                    attributeValueAsString = (tmpInt != 0) ? "true" : "false";

                    QBCLog.Warning(QBCLog.BuildMessageWithContext(Element,
                        "Attribute's '{1}' value was provided as an integer (saw '{2}')--a boolean was expected.{0}"
                        + "The integral value '{2}' was converted to Boolean({3}).{0}"
                        + "Please update to provide '{3}' for this value.",
                        Environment.NewLine,
                        attributeName,
                        tmpInt,
                        attributeValueAsString));
                }

                // Fall through for normal boolean conversion
            }


            // Enums require special handling...
            else if (concreteType.IsEnum)
            {
                T tmpValue = default(T);

                try
                {
                    tmpValue = (T)Enum.Parse(concreteType, attributeValueAsString);

                    if (!Enum.IsDefined(concreteType, tmpValue))
                        { throw new ArgumentException(); }

                    // If the provided value is a number instead of Enum name, ask the profile writer to fix it...
                    // This is not fatal, so we let it go without flagging IsAttributeProblem.
                    int tmpInt;
                    if (int.TryParse(attributeValueAsString, out tmpInt))
                    {
                        QBCLog.Warning(QBCLog.BuildMessageWithContext(Element,
                            "The '{1}' attribute's value '{2}' has been implicitly converted"
                            + " to the corresponding enumeration '{3}'.{0}"
                            + "Please use the enumeration name '{3}' instead of a number.",
                            Environment.NewLine,
                            attributeName,
                            tmpInt,
                            tmpValue.ToString()));
                    }
                }
                catch (Exception)
                {
                    QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                        "The value '{1}' is not a member of the {2} enumeration."
                        + "  Allowed values: {3}",
                        Environment.NewLine,
                        attributeValueAsString,
                        concreteType.Name,
                        string.Join(", ", Enum.GetNames(concreteType))));
                    throw;
                }

                return (tmpValue);
            }



            try
                { return ((T)Convert.ChangeType(attributeValueAsString, concreteType)); }
            catch (Exception except)
            {
                QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                    "The '{1}' attribute's value (saw '{2}') is malformed. ({3})",
                    Environment.NewLine,
                    attributeName,
                    attributeValueAsString,
                    except.GetType().Name));
                throw;
            }
        }


        public bool IsAttributeProblem
        {
            get { return (_isAttributeProblem); }
            protected set { if (value) { _isAttributeProblem = true; } }
        }
        private bool _isAttributeProblem;


        public void HandleAttributeProblem()
        {
            UtilReportUnrecognizedAttributes();
        }


        // Data Members
        private readonly List<string> _recognizedAttributes = new List<string>();


        private int UtilCountKeyNames(string primaryName, IEnumerable<string> aliasNames)
        {
            int keyCount = 0;

            if (!string.IsNullOrEmpty(primaryName))
            { keyCount += (Attributes.ContainsKey(primaryName) ? 1 : 0); }

            if (aliasNames != null)
            { keyCount += ((from keyName in aliasNames where Attributes.ContainsKey(keyName) select keyName).Count()); }

            return (keyCount);
        }


        private string UtilLocateKey(bool isAttributeRequired, string primaryName, string[] aliasNames)
        {
            // Register keys as recognized
            UtilRecognizeAttributeNames(primaryName, aliasNames);

            // Make sure the key was only specified once --
            // The 'dictionary' nature of Args assures that a key name will only be in the dictionary once.
            // However, if the key has been renamed, and an alias maintained for backward-compatibility,
            // then the user could specify the primary key name and one or more aliases as attributes.
            // If all the aliases provided the same value, then it is harmless, but we don't make the
            // distinction.  Instead, we encourage the user to use the preferred name of the key.  This
            // eliminates any possibility of the user specifying conflicting values for the 'same' attribute.
            if (UtilCountKeyNames(primaryName, aliasNames) > 1)
            {
                var keyNames = new List<string> { primaryName };

                keyNames.AddRange(aliasNames);
                keyNames.Sort();

                QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                    "The attributes [{1}] are aliases for each other, and thus mutually exclusive.{0}"
                    + "Please specify the attribute by its preferred name '{2}'.",
                    Environment.NewLine,
                    ("'" + string.Join("', '", keyNames.ToArray()) + "'"),
                    primaryName));
                IsAttributeProblem = true;
                return (null);
            }


            // Prefer the primary name...
            if (!string.IsNullOrEmpty(primaryName) && Attributes.ContainsKey(primaryName))
                { return (primaryName); }

            if (aliasNames != null)
            {
                string keyName = (from aliasName in aliasNames
                                  where !string.IsNullOrEmpty(aliasName) && Attributes.ContainsKey(aliasName)
                                  select aliasName).FirstOrDefault();

                if (!string.IsNullOrEmpty(keyName))
                {
                    QBCLog.Warning(QBCLog.BuildMessageWithContext(Element,
                        "Found attribute via its alias name '{1}'.{0}"
                        + "Please update to use its primary name '{2}', instead.",
                        Environment.NewLine,
                        keyName,
                        primaryName));
                    return (keyName);
                }
            }


            // Attribute is required, but cannot be located...
            if (isAttributeRequired)
            {
                QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                    "Attribute '{1}' is required, but was not provided.",
                    Environment.NewLine,
                    primaryName));
                IsAttributeProblem = true;
            }

            return (null);
        }


        private void UtilRecognizeAttributeNames(string primaryName, IEnumerable<string> attributeAliases)
        {
            if (!string.IsNullOrEmpty(primaryName) && !_recognizedAttributes.Contains(primaryName))
            { _recognizedAttributes.Add(primaryName); }

            if (attributeAliases != null)
            {
                foreach (string aliasName in attributeAliases)
                {
                    if (!_recognizedAttributes.Contains(aliasName))
                    { _recognizedAttributes.Add(aliasName); }
                }
            }
        }


        private void UtilReportUnrecognizedAttributes()
        {
            var unrecognizedAttributes = (from attributeName in Attributes.Keys
                                          where !_recognizedAttributes.Contains(attributeName)
                                          orderby attributeName
                                          select attributeName);

            foreach (string attributeName in unrecognizedAttributes)
            {
                QBCLog.Warning(QBCLog.BuildMessageWithContext(Element,
                    "Attribute '{1}' is not a recognized attribute--ignoring it.",
                    Environment.NewLine,
                    attributeName));
            }
        }
    }
}
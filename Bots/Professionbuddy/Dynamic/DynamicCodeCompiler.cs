using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.ComponentBase;
using Microsoft.CSharp;
using System.Threading.Tasks;
using Styx.CommonBot.Coroutines;

namespace HighVoltz.Professionbuddy.Dynamic
{
	public class DynamicCodeCompiler
	{
		private static readonly bool _usingHBLite = Assembly.GetEntryAssembly().GetType("Bots.Gatherbuddy.GatherbuddyBot") == null;

		private static readonly Dictionary<string, IDynamicallyCompiledCode> CsharpCodeDict = new Dictionary<string, IDynamicallyCompiledCode>();

		private static readonly IEnumerable<KeyValuePair<string, IDynamicallyCompiledCode>> Declarations = from dec in CsharpCodeDict
			where
				dec.Value.CodeType ==
				CsharpCodeType.
					Declaration
			select dec;

		private static readonly IEnumerable<KeyValuePair<string, IDynamicallyCompiledCode>> NoneDeclarations =
			from dec in CsharpCodeDict
			where dec.Value.CodeType != CsharpCodeType.Declaration
			select dec;

		private static object _codeDriverInstance;
		public static bool CodeIsModified = true;

		private static string _tempFolder;

		static DynamicCodeCompiler()
		{
			if (!_usingHBLite)
				Prefix = String.Format("using Bots.Gatherbuddy;\r\n{0}", Prefix);
		}

		public static string TempFolder
		{
			get { return _tempFolder ?? (_tempFolder = Path.Combine(ProfessionbuddyBot.BotPath, "Temp")); }
		}

		public static StringBuilder CsharpStringBuilder { get; private set; }

		public static void WipeTempFolder()
		{
			if (!Directory.Exists(TempFolder))
			{
				Directory.CreateDirectory(TempFolder);
			}
			foreach (string file in Directory.GetFiles(TempFolder, "*.*", SearchOption.AllDirectories))
			{
				try
				{
					File.Delete(file);
				}
					// ReSharper disable EmptyGeneralCatchClause
				catch
					// ReSharper restore EmptyGeneralCatchClause
				{}
			}
			foreach (string dir in Directory.GetDirectories(TempFolder))
			{
				try
				{
					Directory.Delete(dir);
				}
					// ReSharper disable EmptyGeneralCatchClause
				catch
					// ReSharper restore EmptyGeneralCatchClause
				{}
			}
		}

		public static void GenorateDynamicCode()
		{
			CsharpCodeDict.Clear();
			StoreMethodName(ProfessionbuddyBot.Instance.Branch);
			// check if theres anything to compile
			if (CsharpCodeDict.Count == 0)
				return;
			Type dynamicType = CompileAndLoad();
			if (dynamicType != null)
			{
				_codeDriverInstance = Activator.CreateInstance(dynamicType);

				foreach (MethodInfo method in dynamicType.GetMethods())
				{
					if (CsharpCodeDict.ContainsKey(method.Name))
					{
						switch (CsharpCodeDict[method.Name].CodeType)
						{
							case CsharpCodeType.BoolExpression:
								CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(
									typeof (Func<object, bool>),
									_codeDriverInstance,
									method.Name);
								break;

							case CsharpCodeType.Statements:
								CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(
									typeof(System.Action<object>),
									_codeDriverInstance,
									method.Name);
								break;

							case CsharpCodeType.Coroutine:
								CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(
									typeof(Func<object, Task>),
									_codeDriverInstance,
									method.Name);
								break;

							default:
								Type gType =
									typeof (Func<,>).MakeGenericType(
										new[]
										{
											typeof (object),
											((IDynamicProperty) CsharpCodeDict[method.Name]).
												ReturnType
										});

								CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(
									gType,
									_codeDriverInstance,
									method.Name);
								break;
						}
					}
				}
			}
		}

		private static void StoreMethodName(Component comp)
		{
			var cSharpCode = comp as IDynamicallyCompiledCode;
			if (cSharpCode != null)
			{
				CsharpCodeDict["Code" + Util.Rng.Next(int.MaxValue)
					.ToString(CultureInfo.InvariantCulture)] = cSharpCode;
			}
			// check for DynamicExpression proprerties
			List<IDynamicProperty> dynProps = (from prop in comp.GetType().GetProperties()
				where typeof (IDynamicProperty).IsAssignableFrom(prop.PropertyType)
				select (IDynamicProperty) prop.GetValue(comp, null)).ToList();
			foreach (IDynamicProperty dynProp in dynProps)

			{
				CsharpCodeDict["Code" + Util.Rng.Next(int.MaxValue).ToString(CultureInfo.InvariantCulture)] = dynProp;
			}

			var composite = comp as Composite;
			if (composite != null)
			{
				foreach (var child in composite.Children)
					StoreMethodName(child);
			}
		}

		public static Type CompileAndLoad()
		{
			CompilerResults results;
			using (var provider = new CSharpCodeProvider(
				new Dictionary<string, string>
				{
					{"CompilerVersion", "v4.0"},
				}))
			{
				var options = new CompilerParameters();
				foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (!asm.GetName().Name.Contains(ProfessionbuddyBot.Instance.Name) && !asm.IsDynamic)
						options.ReferencedAssemblies.Add(asm.Location);
				}

				options.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);

				// disabled due to a bug in 2.0.0.3956;
				//options.GenerateInMemory = true; 
				options.GenerateExecutable = false;
				options.TempFiles = new TempFileCollection(TempFolder, false);
				options.IncludeDebugInformation = false;
				options.OutputAssembly = string.Format("{0}\\CodeAssembly{1:N}.dll", TempFolder, Guid.NewGuid());
				options.CompilerOptions = "/optimize /nowarn:1998";
				CsharpStringBuilder = new StringBuilder();
				CsharpStringBuilder.Append(Prefix);
				// Line numbers are used to identify actions that genorated compile errors.
				int currentLine = CsharpStringBuilder.ToString().Count(c => c == '\n') + 1;
				// genorate CanRun Methods
				foreach (var met in Declarations)
				{
					CsharpStringBuilder.AppendFormat("{0}\n", met.Value.Code.Replace(Environment.NewLine, ""));
					met.Value.CodeLineNumber = currentLine++;
				}
				foreach (var met in NoneDeclarations)
				{
					switch (met.Value.CodeType)
					{
						case CsharpCodeType.BoolExpression:
							CsharpStringBuilder.AppendFormat(
								"public bool {0} (object instance){{return {1};}}\n",
								met.Key,
								met.Value.Code.Replace(Environment.NewLine, ""));
							break;

						case CsharpCodeType.Statements:
							CsharpStringBuilder.AppendFormat(
								"public void {0} (object instance){{{1}}}\n",
								met.Key,
								met.Value.Code.Replace(Environment.NewLine, ""));
							break;

						case CsharpCodeType.Coroutine:
							CsharpStringBuilder.AppendFormat(
								"public async Task {0} (object instance){{{1}}}\n",
								met.Key,
								met.Value.Code.Replace(Environment.NewLine, ""));
							break;

						default:
							Type retType = ((IDynamicProperty) met.Value).ReturnType;
							CsharpStringBuilder.AppendFormat(
								"public {0} {1} (object instance){{return ({0}){2};}}\n",
								retType.Name,
								met.Key,
								met.Value.Code.Replace(Environment.NewLine, ""));
							break;
					}
					met.Value.CodeLineNumber = currentLine++;
				}
				CsharpStringBuilder.Append(Postfix);

				results = provider.CompileAssemblyFromSource(
					options,
					CsharpStringBuilder.ToString());
			}

			List<IPBComponent> compositesToRefreshList =
				CsharpCodeDict.Where(c => !string.IsNullOrEmpty(c.Value.CompileError))
					.Select(c => c.Value.AttachedComponent)
					.Distinct()
					.ToList();

			// clear all previous compile errors
			foreach (var code in CsharpCodeDict.Values)
			{
				code.CompileError = string.Empty;
				var dynamicProperty = code as IDynamicProperty;
				if (dynamicProperty == null) continue;
				dynamicProperty.AttachedComposite.HasErrors = false;
			}

			if (results.Errors.HasErrors)
			{
				if (results.Errors.Count > 0)
				{
					foreach (CompilerError error in results.Errors)
					{
						IDynamicallyCompiledCode icsc = CsharpCodeDict.Values.FirstOrDefault(c => c.CodeLineNumber == error.Line);
						var dynamicProperty = icsc as IDynamicProperty;

						if (icsc != null)
						{
							if (dynamicProperty != null)
							{
								PBLog.Warn(
									"{0}->{1}\nCompile Error : {2}\n",
									icsc.AttachedComponent.Name,
									dynamicProperty.Name,
									error.ErrorText);
								dynamicProperty.AttachedComposite.HasErrors = true;
							}
							else
							{
								PBLog.Warn("{0}\nCompile Error : {1}\n", icsc.AttachedComponent.Title, error.ErrorText);
							}
							if (!compositesToRefreshList.Contains(icsc.AttachedComponent))
								compositesToRefreshList.Add(icsc.AttachedComponent);
							icsc.CompileError = error.ErrorText;
						}
						else
						{
							PBLog.Warn("Unable to link action that produced Error: {0}", error.ErrorText);
						}
					}
				}
			}

			if (MainForm.IsValid)
			{
				foreach (var pbComposite in compositesToRefreshList)
				{
					MainForm.Instance.RefreshActionTree(pbComposite);
				}
				MainForm.Instance.ActionGrid.Refresh();
			}

			CodeIsModified = false;
			return results.Errors.HasErrors ? null : results.CompiledAssembly.GetType("CodeDriver");
		}

		#region Strings

		private const string Postfix =
			@"
            static LocalPlayer Me {get{return StyxWoW.Me;}}
            static PbProfileSettings Settings {get{return ProfessionbuddyBot.Instance.ProfileSettings;}}
            public static Helpers.TradeskillHelper Alchemy { get { return Helpers.Alchemy;} }
            public static Helpers.TradeskillHelper Archaeology { get { return Helpers.Archaeology;} }
            public static Helpers.TradeskillHelper Blacksmithing { get { return Helpers.Blacksmithing;} }
            public static Helpers.TradeskillHelper Cooking { get { return Helpers.Cooking;} }
            public static Helpers.TradeskillHelper Enchanting { get { return Helpers.Enchanting;} }
            public static Helpers.TradeskillHelper Engineering { get { return Helpers.Engineering;} }
            public static Helpers.TradeskillHelper FirstAid { get { return Helpers.FirstAid;} }
            public static Helpers.TradeskillHelper Fishing { get { return Helpers.Fishing;} }
            public static Helpers.TradeskillHelper Inscription { get { return Helpers.Inscription;} }
            public static Helpers.TradeskillHelper Herbalism { get { return Helpers.Herbalism;} }
            public static Helpers.TradeskillHelper Jewelcrafting { get { return Helpers.Jewelcrafting;} }
            public static Helpers.TradeskillHelper Leatherworking { get { return Helpers.Leatherworking;} }
            public static Helpers.TradeskillHelper Mining { get { return Helpers.Mining;} }
            public static Helpers.TradeskillHelper Tailoring { get { return Helpers.Tailoring;} }
			public static Helpers.TradeskillHelper Skinning { get { return Helpers.Skinning;} }
            public static DataStore DataStore {get{return ProfessionbuddyBot.Instance.DataStore;}}
            uint CanRepeatNum (uint id){  return Helpers.TradeskillHelper.CanRepeatNum(id);}
            bool CanCraft (uint id){  return Helpers.TradeskillHelper.CanCraft(id);}
            bool HasMats (uint id){  return Helpers.TradeskillHelper.HasMats(id);}
            bool HasTools (uint id){  return Helpers.TradeskillHelper.HasTools(id);}
            bool HasRecipe (uint id){  return Helpers.TradeskillHelper.HasRecipe(id);}
            bool HasNewMail { get{ return MailFrame.Instance.HasNewMail;}}
            int MailCount { get{ return MailFrame.Instance.MailCount;}}
            bool HasItem (uint id) {return InbagCount(id) > 0; }
            int InbagCount (uint id) {return Helpers.InbagCount(id); }
            int InBankCount (uint id) {return Util.GetBankItemCount(id); }

			int GBankTabFreeSlots(int gbTab, string character = null,string server = null) {return Helpers.GBankTabFreeSlots(gbTab,character,server);}

			int GBankTotalFreeSlots(string character = null,string server = null) {return Helpers.GBankTotalFreeSlots(character,server);}

			int GbankTabCount(string character = null,string server = null) {return Helpers.GbankTabCount(character,server);}

			int InGBankCount(string character, uint itemId) {return Helpers.InGBankCount(itemId, character);}
			int InGBankCount(string character, string server, uint itemId) {return Helpers.InGBankCount(itemId, character,server);}
			int InGBankCount(uint itemId, string character = null, string server = null) {return Helpers.InGBankCount(itemId, character,server);}

			int OnAhCount(string character, uint itemId) {return Helpers.OnAhCount(itemId,character);}
			int OnAhCount(string character,string server, uint itemId) {return Helpers.OnAhCount(itemId,character,server);}
			int OnAhCount(uint itemId, string character = null, string server = null) {return Helpers.OnAhCount(itemId,character,server);}

            void Log (System.Windows.Media.Color c,string f,params object[] args) {Helpers.Log(c,f,args); }
            void Log (System.Drawing.Color c,string f,params object[] args) {Helpers.Log(c,f,args); }
            void Log (string f,params object[] args) {Helpers.Log(f,args); }
            void Log(System.Windows.Media.Color headerColor, string header, System.Windows.Media.Color msgColor, string format, params object[] args) 
            {
                PBLog.Log(headerColor, header, msgColor, format, args);
            }
            void Log(Styx.Common.LogLevel logLevel, System.Windows.Media.Color headerColor, string header, System.Windows.Media.Color msgColor, string format, params object[] args) 
            {
                PBLog.Log(logLevel, headerColor, header, msgColor, format, args);
            }
            void Log(System.Drawing.Color headerColor, string header, System.Drawing.Color msgColor, string format, params object[] args) 
            {
                PBLog.Log(headerColor, header, msgColor, format, args);
            }
            float DistanceTo(double x,double y,double z) {return Helpers.DistanceTo(x,y,z); }
            float DistanceTo(WoWPoint p) {return Helpers.DistanceTo(p.X,p.Y,p.Z); }
            void MoveTo(double x,double y,double z) {Helpers.MoveTo(x,y,z); }
            void MoveTo(WoWPoint p) {Helpers.MoveTo(p.X,p.Y,p.Z); }
            void CTM(double x,double y,double z) {Helpers.CTM(x,y,z); }
            void CTM(WoWPoint p) {Helpers.CTM(p.X,p.Y,p.Z); }
            void RefreshDataStore() {ProfessionbuddyBot.Instance.DataStore.ImportDataStore(); }
            void SwitchToBot(string botName) {try{ProfessionbuddyBot.ChangeSecondaryBot(botName);}catch{}}
            void SwitchCharacter(string character,string server,string botName){Helpers.SwitchCharacter(character,server,botName);}
            BotBase SecondaryBot {get{return ProfessionbuddyBot.Instance.SecondaryBot;}}
            bool HasDataStoreAddon {get{return DataStore.HasDataStoreAddon;}}
            HBRelogApi HBRelog {get{return Helpers.HBRelog;}}
            bool RecipeIsOnCD (int id) {return Lua.GetReturnVal<bool>(string.Format(""return GetSpellCooldown({0})"",id), 0) ; }
        }";

		private static readonly string Prefix =
			@"using System;
			using System.Threading.Tasks;
            using System.Reflection;
            using System.Data;
            using System.Threading;
            using System.Diagnostics;
            using System.Collections.Generic;
            using System.Collections;
            using System.Linq;
            using System.Text;
            using System.IO;
            using System.Windows.Forms;
            using System.Windows.Media;
            using System.Xml.Linq;

            using Styx;
            using Styx.Common;
            using Styx.Helpers;
            using Styx.CommonBot.Routines;
            using Styx.WoWInternals;
            using Styx.WoWInternals.WoWObjects;
            using Styx.CommonBot.AreaManagement;
            using Styx.CommonBot;
            using Styx.CommonBot.Frames;
            using Styx.Pathing;
            using Styx.CommonBot.Profiles;
            using Styx.Plugins;
            using Styx.WoWInternals.World;
			using Styx.CommonBot.Coroutines;
			using Buddy.Coroutines;

			using HighVoltz.Professionbuddy;          
            using HighVoltz.Professionbuddy.Components;
            using HighVoltz.Professionbuddy.Dynamic;  
            using Color = System.Drawing.Color;

        public class CodeDriver
        {
            static object var1,var2,var3,var4,var5,var6,var7,var8,var9;
";

		#endregion
	}
}
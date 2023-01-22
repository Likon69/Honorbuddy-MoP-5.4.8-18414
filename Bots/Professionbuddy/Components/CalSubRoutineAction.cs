using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("CallSubRoutine")]
    public sealed class CallSubRoutineAction : PBAction
    {
        private SubRoutineComposite _sub;

	    public CallSubRoutineAction()
	    {
		    Properties["SubRoutineName"] = new MetaProp(
			    "SubRoutineName",
				    typeof (string),
				    new DisplayNameAttribute(
						ProfessionbuddyBot.Instance.Strings["Action_Common_SubRoutineName"]));
		    SubRoutineName = "";
	    }

        [PBXmlAttribute]
        public string SubRoutineName
        {
            get { return Properties.GetValue<string>("SubRoutineName"); }
            set { Properties["SubRoutineName"].Value = value; }
        }

        public override string Name
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_CallSubRoutine_Name"]; }
        }

        public override string Title
        {
            get { return string.Format("{0}: {1}()", Name, SubRoutineName); }
        }

        public override string Help
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_CallSubRoutine_Help"]; }
        }

		protected async override Task Run()
		{
			if (_sub == null && !SubRoutineComposite.GetSubRoutineMyName(SubRoutineName, out _sub))
			{
				PBLog.Warn("{0}: {1}.", ProfessionbuddyBot.Instance.Strings["Error_SubroutineNotFound"], SubRoutineName);
				IsDone = true;
				return;
			}

			using(SubRoutineComposite.Activate(_sub))
			{
				try 
				{
					if (_sub.IsDone)
						_sub.Reset();
					await _sub;
				}
				finally
				{
					IsDone = _sub.IsDone;
				}
			}
		}

	    public override IPBComponent DeepCopy()
	    {
			return new CallSubRoutineAction { SubRoutineName = SubRoutineName };
	    }

	}
}
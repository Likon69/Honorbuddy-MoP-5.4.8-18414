using System;
using System.ComponentModel;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx.Common.Helpers;
using Styx.CommonBot;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("ChangeBot", new[] { "ChangeBotAction" })]
	public sealed class ChangeBotAction : PBAction
	{
		private BotBase _bot;
		private WaitTimer _changeBotTimer;

		public ChangeBotAction()
		{
			Properties["BotName"] = new MetaProp(
				"BotName",
				typeof (string),
				new DisplayNameAttribute(Strings["Action_ChangeBotAction_BotName"]));
			BotName = "";
		}

		[PBXmlAttribute]
		public string BotName
		{
			get { return Properties.GetValue<string>("BotName"); }
			set { Properties["BotName"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_ChangeBotAction_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("{0}: {1}", Name, BotName); }
		}

		public override string Help
		{
			get { return Strings["Action_ChangeBotAction_Help"]; }
		}

		protected override async Task Run()
		{
			try
			{
				if (_changeBotTimer == null)
				{
					_changeBotTimer = new WaitTimer(TimeSpan.FromSeconds(10));
					_changeBotTimer.Reset();
					_bot = Util.GetBotByName(BotName);
					if (_bot != null)
					{
						if (ProfessionbuddyBot.Instance.SecondaryBot == _bot)
						{
							IsDone = true;
							return;
						}
						ProfessionbuddyBot.ChangeSecondaryBot(BotName);
					}
				}
			}
			finally
			{
				// Wait until bot change completes or fails
				if (_bot == null ||
					_changeBotTimer != null && (_changeBotTimer.IsFinished || ProfessionbuddyBot.Instance.SecondaryBot == _bot))
				{
					IsDone = true;
					_changeBotTimer = null;
				}
			}

			if (IsDone)
			{
				if (_bot == null)
					PBLog.Warn("No bot with name: {0} could be found", BotName);
				else if (ProfessionbuddyBot.Instance.SecondaryBot == _bot)
					PBLog.Log("Successfuly changed secondary bot to: {0}", BotName);
				else
					PBLog.Warn("Unable to switch secondary bot to: {0}", BotName);
			}
		}

		public override IPBComponent DeepCopy()
		{
			return new ChangeBotAction {BotName = BotName};
		}
	}
}
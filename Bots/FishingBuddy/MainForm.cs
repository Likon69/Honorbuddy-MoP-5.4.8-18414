using System;
using System.Windows.Forms;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;

namespace Bots.FishingBuddy
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
			propertyGrid.SelectedObject = FishingBuddySettings.Instance;
        }

        private void PropertyGridPropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
			FishingBuddySettings.Instance.Save();
        }

        private void MailButtonClick(object sender, EventArgs e)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile != null && profile.MailboxManager != null)
            {
                Mailbox mailbox = profile.MailboxManager.GetClosestMailbox();
                if (mailbox != null)
                {
                    if (!string.IsNullOrEmpty(CharacterSettings.Instance.MailRecipient))
                    {
                        BotPoi.Current = new BotPoi(mailbox);
						FishingBuddyBot.Log("Forced Mail run");
                        TreeRoot.StatusText = "Doing Mail Run";
                    }
                    else
						FishingBuddyBot.Log("No mail recipient set");
                }
                else
                {
					FishingBuddyBot.Log("Profile has no Mailbox");
                }
            }
        }
    }
}
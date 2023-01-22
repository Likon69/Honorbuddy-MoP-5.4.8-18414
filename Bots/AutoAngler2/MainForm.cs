using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;

namespace HighVoltz.AutoAngler
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            propertyGrid.SelectedObject = AutoAnglerBot.Instance.MySettings;
        }

        private void DonateButtonClick(object sender, EventArgs e)
        {
            // my debug button :)
            if (Environment.UserName == "highvoltz")
            {

            }
            else
                Process.Start(
                    "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=MMC4GPHR8GQFN&lc=US&item_name=Highvoltz%27s%20Development%20fund&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donate_LG%2egif%3aNonHosted");
        }

        private void RepButtonClick(object sender, EventArgs e)
        {
            Process.Start("http://www.thebuddyforum.com/reputation.php?do=addreputation&p=343952");
        }

        private void PropertyGridPropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (e.ChangedItem.Label == "Poolfishing")
            {
                if (!(bool) e.ChangedItem.Value)
                {
                    if (!string.IsNullOrEmpty(ProfileManager.XmlLocation))
                    {
                        AutoAnglerSettings.Instance.LastLoadedProfile = ProfileManager.XmlLocation;
                        AutoAnglerSettings.Instance.Save();
                    }
                    ProfileManager.LoadEmpty();
                }
                else if ((ProfileManager.CurrentProfile == null || ProfileManager.CurrentProfile.Name == "Empty Profile") &&
                         !string.IsNullOrEmpty(AutoAnglerSettings.Instance.LastLoadedProfile) &&
                         File.Exists(AutoAnglerSettings.Instance.LastLoadedProfile))
                {
                    ProfileManager.LoadNew(AutoAnglerSettings.Instance.LastLoadedProfile);
                }
            }
			AutoAnglerBot.Instance.MySettings.Save();
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
						AutoAnglerBot.Instance.Log("Forced Mail run");
                        TreeRoot.StatusText = "Doing Mail Run";
                    }
                    else
						AutoAnglerBot.Instance.Log("No mail recipient set");
                }
                else
                {
					AutoAnglerBot.Instance.Log("Profile has no Mailbox");
                }
            }
        }
    }
}
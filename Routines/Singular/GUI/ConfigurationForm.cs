
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.POI;

namespace Singular.GUI
{
    public partial class ConfigurationForm : Form
    {
        public ConfigurationForm()
        {
            InitializeComponent();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == DialogResult.OK || DialogResult == DialogResult.Yes)
            {
                Logger.WriteDebug(Color.LightGreen, "Settings saved, rebuilding behaviors...");
                HotkeyDirector.Update();
                MovementManager.Update();
                SingularRoutine.DescribeContext();
                SingularRoutine.Instance.RebuildBehaviors();
                SingularSettings.Instance.LogSettings();
            }
            base.OnClosing(e);
        }

        private void ConfigurationForm_Load(object sender, EventArgs e)
        {
            // lblVersion.Text = string.Format("Version {0}", Assembly.GetExecutingAssembly().GetName().Version);
            lblVersion.Text = string.Format("Version {0}", SingularRoutine.GetSingularVersion());

            //HealTargeting.Instance.OnTargetListUpdateFinished += new Styx.Logic.TargetListUpdateFinishedDelegate(Instance_OnTargetListUpdateFinished);
            pgGeneral.SelectedObject = SingularSettings.Instance;

            tabClass.Text = StyxWoW.Me.Class.ToString().CamelToSpaced().Substring(1) + " Specific";
            
            Styx.Helpers.Settings toSelect = null;
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warrior:
                    toSelect = SingularSettings.Instance.Warrior();
                    break;
                case WoWClass.Paladin:
                    toSelect = SingularSettings.Instance.Paladin();
                    break;
                case WoWClass.Hunter:
                    toSelect = SingularSettings.Instance.Hunter();
                    break;
                case WoWClass.Rogue:
                    toSelect = SingularSettings.Instance.Rogue();
                    break;
                case WoWClass.Priest:
                    toSelect = SingularSettings.Instance.Priest();
                    break;
                case WoWClass.DeathKnight:
                    toSelect = SingularSettings.Instance.DeathKnight();
                    break;
                case WoWClass.Shaman:
                    toSelect = SingularSettings.Instance.Shaman();
                    break;
                case WoWClass.Mage:
                    toSelect = SingularSettings.Instance.Mage();
                    break;
                case WoWClass.Warlock:
                    toSelect = SingularSettings.Instance.Warlock();
                    break;
                case WoWClass.Druid:
                    toSelect = SingularSettings.Instance.Druid();
                    break;
                case WoWClass.Monk:
                    toSelect = SingularSettings.Instance.Monk();
                    break;
                default:
                    break;
            }

            if (toSelect != null)
            {
                pgClass.SelectedObject = toSelect;
            }

            pgHotkeys.SelectedObject = SingularSettings.Instance.Hotkeys();

            InitializeDebugOutputDropdown();
            chkDebugCasting.Checked = SingularSettings.Instance.EnableDebugSpellCasting;
            chkDebugTrace.Checked = SingularSettings.Instance.EnableDebugTrace;

            chkDebugLogging_CheckedChanged(this, new EventArgs());

            InitializeHealContextDropdown(StyxWoW.Me.Class);
            InitializeForceBehaviorsDropdown();

            if (!timer1.Enabled)
                timer1.Start();

            Screen screen = Screen.FromHandle(this.Handle);
            if (this.Left.Between(0, screen.WorkingArea.Width) && this.Top.Between(0, screen.WorkingArea.Height))
            {
                int height = screen.WorkingArea.Height - this.Top;
                if (height > 200)
                {
                    this.Height = height;
                }
            }

            tabControl1_SelectedIndexChanged(this, new EventArgs());
        }

        public static void SetLabelColumnWidth(PropertyGrid grid, int width)
        {
            if (grid == null)
                return;

            FieldInfo fi = grid.GetType().GetField("gridView", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null)
                return;

            Control view = fi.GetValue(grid) as Control;
            if (view == null)
                return;

            MethodInfo mi = view.GetType().GetMethod("MoveSplitterTo", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi == null)
                return;
            mi.Invoke(view, new object[] { width });
        }

        /// <summary>
        /// populates the cboHealContext dropdown with an object list of all healing context setups
        /// that apply to current character.  will initially clear the list, then populate, and
        /// finally set the current context as selected (or first in list if not applicable)
        /// </summary>
        /// <param name="cls"></param>
        private void InitializeHealContextDropdown(WoWClass cls)
        {
            cboHealContext.Items.Clear();
            if (cls == WoWClass.Shaman)
            {
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().RestoBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().RestoInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().RestoRaid));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.None, SingularSettings.Instance.Shaman().OffhealBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.None, SingularSettings.Instance.Shaman().OffhealPVE));
            }

            if (cls == WoWClass.Druid)
            {
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.DruidRestoration, SingularSettings.Instance.Druid().Battleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.DruidRestoration, SingularSettings.Instance.Druid().Instance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.DruidRestoration, SingularSettings.Instance.Druid().Raid));
            }

            if (cls == WoWClass.Monk)
            {
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.MonkMistweaver, SingularSettings.Instance.Monk().MistBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.MonkMistweaver, SingularSettings.Instance.Monk().MistInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.MonkMistweaver, SingularSettings.Instance.Monk().MistRaid));
            }

            if (cls == WoWClass.Priest)
            {
/*
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscRaid));
 */ 
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.PriestHoly, SingularSettings.Instance.Priest().HolyBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.PriestHoly, SingularSettings.Instance.Priest().HolyInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.PriestHoly, SingularSettings.Instance.Priest().HolyRaid));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscRaid));
            }

            bool needHealPage = cboHealContext.Items.Count > 0;
            cboHealContext.Enabled = needHealPage;
            if (needHealPage && !tabControl1.TabPages.Contains(tabGroupHeal))
                tabControl1.TabPages.Insert(2, tabGroupHeal);
            else if (!needHealPage && tabControl1.TabPages.Contains(tabGroupHeal))
                tabControl1.TabPages.Remove(tabGroupHeal);

            foreach (var obj in cboHealContext.Items)
            {
                HealContextItem ctx = (HealContextItem)obj;
                if (ctx.Spec == TalentManager.CurrentSpec)
                {
                    if (ctx.Context == SingularRoutine.CurrentHealContext)
                    {
                        cboHealContext.SelectedItem = ctx;
                        break;
                    }
                    if (SingularRoutine.CurrentHealContext == HealingContext.Normal && ctx.Spec == WoWSpec.PriestHoly && ctx.Context == HealingContext.Instances)
                    {
                        cboHealContext.SelectedItem = ctx;
                        break;
                    }
                    if (SingularRoutine.CurrentHealContext == HealingContext.Normal && ctx.Spec == WoWSpec.PriestDiscipline && ctx.Context == HealingContext.Instances)
                    {
                        cboHealContext.SelectedItem = ctx;
                        break;
                    }
                }
            }

            if (cboHealContext.SelectedItem == null && cboHealContext.Items.Count > 0)
            {
                foreach (var obj in cboHealContext.Items)
                {
                    HealContextItem ctx = (HealContextItem)obj;
                    if (ctx.Spec == WoWSpec.None)
                    {
                        if (ctx.Context == SingularRoutine.CurrentHealContext)
                        {
                            cboHealContext.SelectedItem = ctx;
                            break;
                        }
                        if (ctx.Context != HealingContext.Battlegrounds && HealingContext.Battlegrounds != SingularRoutine.CurrentHealContext)
                        {
                            cboHealContext.SelectedItem = ctx;
                            break;
                        }
                        if (SingularRoutine.CurrentHealContext == HealingContext.Normal && ctx.Spec == WoWSpec.PriestDiscipline && ctx.Context == HealingContext.Instances)
                        {
                            cboHealContext.SelectedItem = ctx;
                            break;
                        }
                    }
                }
            }

            if (cboHealContext.SelectedItem == null && cboHealContext.Items.Count > 0)
            {
                    cboHealContext.SelectedIndex = 0;
            }
        }

        private void InitializeForceBehaviorsDropdown()
        {
            cboForceUseOf.Items.Add(new CboItem((int)WoWContext.Normal, "Normal (Solo)"));
            cboForceUseOf.Items.Add(new CboItem((int)WoWContext.Battlegrounds, "Battlegrounds (PVP)"));
            cboForceUseOf.Items.Add(new CboItem((int)WoWContext.Instances, "Instances (Group)"));

            SetComboBoxEnum(cboForceUseOf, (int)SingularRoutine.TrainingDummyBehaviors);
        }
        private void InitializeDebugOutputDropdown()
        {
            cboDebugOutput.Items.Add(new CboItem((int)DebugOutputDest.None, "None (Off)"));
            cboDebugOutput.Items.Add(new CboItem((int)DebugOutputDest.FileOnly, "File Only"));
            cboDebugOutput.Items.Add(new CboItem((int)DebugOutputDest.WindowAndFile, "Window & File"));

            SetComboBoxEnum(cboDebugOutput, (int)SingularSettings.Instance.DebugOutput);
        }

        /*
                private void Instance_OnTargetListUpdateFinished(object context)
                {
                    if (InvokeRequired)
                    {
                        Invoke(new TargetListUpdateFinishedDelegate(Instance_OnTargetListUpdateFinished), context);
                        return;
                    }

                    int i = 0;
                    var sb = new StringBuilder();
                    foreach (WoWUnit u in Targeting.Instance.TargetList)
                    {
                        sb.AppendLine(u.SafeName() + " - " + u.HealthPercent.ToString("F1") + "% - " + u.Distance.ToString("F1") + " yds");
                        if (++i == 5)
                            break;
                    }
                    lblTargets.Text = sb.ToString();

                    if (HealerManager.Instance.t
                    i = 0;
                    sb = new StringBuilder();
                    foreach (WoWUnit u in HealerManager.Instance.TargetList)
                    {
                        sb.AppendLine(u.SafeName() + " - " + u.HealthPercent.ToString("F1") + "% - " + u.Distance.ToString("F1") + " yds");
                        if (++i == 5)
                            break;
                    }
                    lblHealTargets.Text = sb.ToString();
                }
        */
#pragma warning disable 168 // for ex below

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        { // prevent an exception from closing HB.
            try
            {
                // deal with Debug tab controls individually
                SingularSettings.Instance.DebugOutput = (DebugOutputDest) GetComboBoxEnum(cboDebugOutput);               
                SingularSettings.Instance.EnableDebugSpellCasting = chkDebugCasting.Checked;
                SingularSettings.Instance.EnableDebugTrace = chkDebugTrace.Checked;
                Extensions.ShowPlayerNames = ShowPlayerNames.Checked;
                SingularRoutine.TrainingDummyBehaviors = (WoWContext) GetComboBoxEnum(cboForceUseOf);

                // save form position
                SingularSettings.Instance.FormHeight = this.Height;
                SingularSettings.Instance.FormWidth = this.Width;
                SingularSettings.Instance.FormTabIndex = tabControl1.SelectedIndex; ;

                // save property group settings from each tab
                ((SingularSettings)pgGeneral.SelectedObject).Save();

                if (pgClass.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgClass.SelectedObject).Save();

                if (pgHotkeys.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgHotkeys.SelectedObject).Save();

                foreach (var obj in cboHealContext.Items)
                {
                    HealContextItem ctx = (HealContextItem)obj;
                    ctx.Settings.Save();
                }

                // CleanseBlacklist.Instance.SpellList.Save();
                // PurgeWhitelist.Instance.SpellList.Save();
                // MageSteallist.Instance.SpellList.Save();

                Close();
            }
            catch (Exception ex)
            {
                Logger.Write("ERROR saving settings: {0}", ex.ToString());
            }
        }

#pragma warning disable 168

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
        private const int WM_SETREDRAW = 11; 

        private void timer1_Tick(object sender, EventArgs e)
        {
            // exit quickly if Debug tab not displayed
            if (tabControl1.SelectedTab != tabDebug)
                return;

            SendMessage(lblPoi.Handle, WM_SETREDRAW, false, 0);
            SendMessage(lblTargets.Handle, WM_SETREDRAW, false, 0);
            SendMessage(lblTankToStayNear.Handle, WM_SETREDRAW, false, 0);
            SendMessage(lblTargets.Handle, WM_SETREDRAW, false, 0);
            SendMessage(lblAuxTargets.Handle, WM_SETREDRAW, false, 0);

            // update POI
            int i = 0;
            var sb = new StringBuilder();
            // poitype   distance 
            sb.Append( BotPoi.Current.Type.ToString());

            try {
                WoWObject o = BotPoi.Current.AsObject;
                if (o != null)
                    sb.Append(" @ " + o.Distance.ToString("F1") + " yds - " + o.SafeName());
            }
            catch {
            }

            lblPoi.Text = sb.ToString();

            // update list of Targets
            i = 0;
            sb = new StringBuilder();
            foreach (WoWUnit u in Targeting.Instance.TargetList)
            {
                try
                {
                    sb.AppendLine(u.SafeName().AlignLeft(20) + " " + u.HealthPercent.ToString("F1").AlignRight(5) + "%  " + u.Distance.ToString("F1").AlignRight(5) + " yds");
                    if (++i == 5)
                        break;
                }
                catch 
                {
                }
            }
            lblTargets.Text = sb.ToString();

            // update list of Heal Targets
            sb = new StringBuilder();
            if (HealerManager.NeedHealTargeting)
            {
                try
                {
                    WoWUnit tank = HealerManager.TankToStayNear;
                    if (tank == null)
                        sb.Append("None");
                    else
                        sb.Append(tank.SafeName().AlignLeft(22) + "- " + tank.HealthPercent.ToString("F1").AlignRight(5) + "% @ " + tank.Distance.ToString("F1").AlignRight(5) + " yds");
                }
                catch
                {
                    sb.Append("Error");
                }

                lblTankToStayNear.Text = sb.ToString();

                i = 0;
                sb = new StringBuilder();
                foreach (WoWUnit u in HealerManager.Instance.TargetList)
                {
                    try
                    {
                        sb.AppendLine(u.SafeName().AlignLeft(22) + "- " + u.HealthPercent.ToString("F1").AlignRight(5) + "% @ " + u.Distance.ToString("F1").AlignRight(5) + " yds");
                        if (++i == 5)
                            break;
                    }
                    catch
                    {
                    }
                }
                lblAuxTargets.Text = sb.ToString();
            }

            // update list of Tank Targets
            if (TankManager.NeedTankTargeting)
            {
                i = 0;
                sb = new StringBuilder();
                foreach (WoWUnit u in TankManager.Instance.TargetList)
                {
                    try
                    {
                        sb.AppendLine(u.SafeName().AlignLeft(20) + " " + u.HealthPercent.ToString("F1").AlignRight(5) + "%  " + u.Distance.ToString("F1").AlignRight(5) + " yds");
                        if (++i == 5)
                            break;
                    }
                    catch (System.AccessViolationException)
                    {
                    }
                    catch (Styx.InvalidObjectPointerException)
                    {
                    }
                }
                lblTargets.Text = sb.ToString();
            }

            SendMessage(lblPoi.Handle, WM_SETREDRAW, true, 0);
            SendMessage(lblTargets.Handle, WM_SETREDRAW, true, 0);
            SendMessage(lblTankToStayNear.Handle, WM_SETREDRAW, true, 0);
            SendMessage(lblTargets.Handle, WM_SETREDRAW, true, 0);
            SendMessage(lblAuxTargets.Handle, WM_SETREDRAW, true, 0);
            tabDebug.Refresh();
        }

        // private int lastTried = 0;

        private void button1_Click(object sender, EventArgs e)
        {
            ObjectManager.Update();
            Logger.Write("Current target is immune to frost? {0}", StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost));
            //var val = Enum.GetValues(typeof(WoWMovement.ClickToMoveType)).GetValue(lastTried++);
            //WoWMovement.ClickToMove(StyxWoW.Me.CurrentTargetGuid, (WoWMovement.ClickToMoveType)val);
            //Logging.Write("Trying " + val);
            //TotemManager.RecallTotems();
        }

        private void ConfigurationForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Stop();
        }

        private void ShowPlayerNames_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void cboHealContext_SelectedIndexChanged(object sender, EventArgs e)
        {
            HealContextItem ctx = (HealContextItem)cboHealContext.SelectedItem;
            pgHeal.SelectedObject = ctx.Settings;
        }

        private void chkUseInstanceBehaviorsWhenSolo_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private static int LogMarkIndex = 1;
        private void btnLogMark_Click(object sender, EventArgs e)
        {
            Logger.Write( Color.HotPink, " LOGMARK # {0} at {1}", LogMarkIndex++, DateTime.Now.ToString("HH:mm:ss.fff"));
            btnLogMark.Text = "LOGMARK! " + LogMarkIndex;
        }

        private void tabControl1_VisibleChanged(object sender, EventArgs e)
        {
            // if ( tabControl1.SelectedIndex
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0)
                SetLabelColumnWidth(pgGeneral, 205);
            else if (tabControl1.SelectedIndex == 1)
                SetLabelColumnWidth(pgClass, 205);
        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            if (HealerManager.NeedHealTargeting)
            {
                grpAuxTargeting.Text = "Heal Targets";
            }
            else if (TankManager.NeedTankTargeting)
            {
                grpAuxTargeting.Text = "Tank Targets";
            }

            grpAuxTargeting.Visible = HealerManager.NeedHealTargeting || TankManager.NeedTankTargeting;
            grpAuxTargeting.Enabled = HealerManager.NeedHealTargeting || TankManager.NeedTankTargeting;
        }

        /// <summary>
        /// set current ComboBox selection for list built from CboItems to passe enum value
        /// </summary>
        /// <param name="cb"></param>
        /// <param name="e"></param>
        private static void SetComboBoxEnum(System.Windows.Forms.ComboBox cb, int e)
        {
            CboItem item;
            for (int i = 0; i < cb.Items.Count; i++)
            {
                item = (CboItem)cb.Items[i];
                if (item.e == e)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }

            item = (CboItem)cb.Items[0];
            Logger.WriteDebug("Dialog Error: combobox {0} does not have enum({1}) in list, defaulting to enum({2})", cb.Name, e, item.e);
            cb.SelectedIndex = 0;
        }

        /// <summary>
        /// retrieves the current Enum value from combobox populated with CboItem objects
        /// </summary>
        /// <param name="cb"></param>
        /// <returns></returns>
        private static int GetComboBoxEnum(System.Windows.Forms.ComboBox cb)
        {
            CboItem item = (CboItem)cb.Items[cb.SelectedIndex];
            return item.e;
        }

        private void chkDebugLogging_CheckedChanged(object sender, EventArgs e)
        {
            DebugOutputDest dbgdest = (DebugOutputDest)GetComboBoxEnum(cboDebugOutput);
            if (chkDebugCasting.Checked && dbgdest == DebugOutputDest.None)
                chkDebugCasting.Checked = false;

            chkDebugCasting.Enabled = (dbgdest != DebugOutputDest.None);
        }
    }

    public class CboItem
    {
        public int e;
        public string s;

        public override string ToString()
        {
            return s;
        }

        public CboItem(int pe, string ps)
        {
            e = pe;
            s = ps;
        }
    }

    public class HealContextItem
    {
        public HealingContext Context { get; set; }
        public WoWSpec Spec { get; set; }
        public HealerSettings Settings { get; set; }

        public override bool Equals(object obj)
        {
            return ToString() == obj.ToString();
        }

        public override string ToString()
        {
            if (Spec == WoWSpec.None)
            {
                if ( Context == HealingContext.Battlegrounds )
                    return "Offheal - Battlegrounds";
                return "Offheal - Group/Companion";
            }

            return Spec.ToString().CamelToSpaced().Trim() + " - " + Context.ToString();
        }

        // ctor for list item
        public HealContextItem(HealingContext context, WoWSpec spec, HealerSettings stgs)
        {
            Context = context;
            Spec = spec;
            Settings = stgs;
        }

        // ctor for lookup only
        public HealContextItem(HealingContext context, WoWSpec spec)
        {
            Context = context;
            Spec = spec;
        }
    }


}
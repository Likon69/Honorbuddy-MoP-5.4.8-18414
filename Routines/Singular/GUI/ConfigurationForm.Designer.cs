namespace Singular.GUI
{
    partial class ConfigurationForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.pgGeneral = new System.Windows.Forms.PropertyGrid();
            this.tabClass = new System.Windows.Forms.TabPage();
            this.pgClass = new System.Windows.Forms.PropertyGrid();
            this.tabGroupHeal = new System.Windows.Forms.TabPage();
            this.pgHeal = new System.Windows.Forms.PropertyGrid();
            this.grpHealHeader = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cboHealContext = new System.Windows.Forms.ComboBox();
            this.tabHotkeys = new System.Windows.Forms.TabPage();
            this.pgHotkeys = new System.Windows.Forms.PropertyGrid();
            this.tabDebug = new System.Windows.Forms.TabPage();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lblTankToStayNear = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.cboForceUseOf = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.chkDebugCasting = new System.Windows.Forms.CheckBox();
            this.ShowPlayerNames = new System.Windows.Forms.CheckBox();
            this.chkDebugTrace = new System.Windows.Forms.CheckBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.lblPoi = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lblTargets = new System.Windows.Forms.Label();
            this.grpAuxTargeting = new System.Windows.Forms.GroupBox();
            this.lblAuxTargets = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.grpFooter = new System.Windows.Forms.GroupBox();
            this.btnLogMark = new System.Windows.Forms.Button();
            this.btnSaveAndClose = new System.Windows.Forms.Button();
            this.lblVersion = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.cboDebugOutput = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabClass.SuspendLayout();
            this.tabGroupHeal.SuspendLayout();
            this.grpHealHeader.SuspendLayout();
            this.tabHotkeys.SuspendLayout();
            this.tabDebug.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.grpAuxTargeting.SuspendLayout();
            this.grpFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabGeneral);
            this.tabControl1.Controls.Add(this.tabClass);
            this.tabControl1.Controls.Add(this.tabGroupHeal);
            this.tabControl1.Controls.Add(this.tabHotkeys);
            this.tabControl1.Controls.Add(this.tabDebug);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(347, 407);
            this.tabControl1.TabIndex = 4;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            this.tabControl1.Selected += new System.Windows.Forms.TabControlEventHandler(this.tabControl1_Selected);
            this.tabControl1.VisibleChanged += new System.EventHandler(this.tabControl1_VisibleChanged);
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.pgGeneral);
            this.tabGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(339, 381);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // pgGeneral
            // 
            this.pgGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgGeneral.Location = new System.Drawing.Point(3, 3);
            this.pgGeneral.Name = "pgGeneral";
            this.pgGeneral.Size = new System.Drawing.Size(333, 375);
            this.pgGeneral.TabIndex = 0;
            // 
            // tabClass
            // 
            this.tabClass.Controls.Add(this.pgClass);
            this.tabClass.Location = new System.Drawing.Point(4, 22);
            this.tabClass.Name = "tabClass";
            this.tabClass.Padding = new System.Windows.Forms.Padding(3);
            this.tabClass.Size = new System.Drawing.Size(339, 381);
            this.tabClass.TabIndex = 1;
            this.tabClass.Text = "Class Specific";
            this.tabClass.UseVisualStyleBackColor = true;
            // 
            // pgClass
            // 
            this.pgClass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgClass.Location = new System.Drawing.Point(3, 3);
            this.pgClass.Name = "pgClass";
            this.pgClass.Size = new System.Drawing.Size(333, 375);
            this.pgClass.TabIndex = 0;
            // 
            // tabGroupHeal
            // 
            this.tabGroupHeal.Controls.Add(this.pgHeal);
            this.tabGroupHeal.Controls.Add(this.grpHealHeader);
            this.tabGroupHeal.Location = new System.Drawing.Point(4, 22);
            this.tabGroupHeal.Name = "tabGroupHeal";
            this.tabGroupHeal.Size = new System.Drawing.Size(339, 381);
            this.tabGroupHeal.TabIndex = 3;
            this.tabGroupHeal.Text = "Group Healing";
            this.tabGroupHeal.UseVisualStyleBackColor = true;
            // 
            // pgHeal
            // 
            this.pgHeal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgHeal.Location = new System.Drawing.Point(0, 39);
            this.pgHeal.Name = "pgHeal";
            this.pgHeal.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.pgHeal.Size = new System.Drawing.Size(339, 342);
            this.pgHeal.TabIndex = 5;
            // 
            // grpHealHeader
            // 
            this.grpHealHeader.Controls.Add(this.label3);
            this.grpHealHeader.Controls.Add(this.cboHealContext);
            this.grpHealHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpHealHeader.ForeColor = System.Drawing.SystemColors.Control;
            this.grpHealHeader.Location = new System.Drawing.Point(0, 0);
            this.grpHealHeader.Margin = new System.Windows.Forms.Padding(0);
            this.grpHealHeader.Name = "grpHealHeader";
            this.grpHealHeader.Padding = new System.Windows.Forms.Padding(0);
            this.grpHealHeader.Size = new System.Drawing.Size(339, 39);
            this.grpHealHeader.TabIndex = 1;
            this.grpHealHeader.TabStop = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label3.Location = new System.Drawing.Point(7, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(101, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Healing Context:";
            // 
            // cboHealContext
            // 
            this.cboHealContext.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboHealContext.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboHealContext.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboHealContext.FormattingEnabled = true;
            this.cboHealContext.Location = new System.Drawing.Point(114, 10);
            this.cboHealContext.Name = "cboHealContext";
            this.cboHealContext.Size = new System.Drawing.Size(208, 21);
            this.cboHealContext.TabIndex = 3;
            this.toolTip1.SetToolTip(this.cboHealContext, "Choose the Spec + Context you want to configure");
            this.cboHealContext.SelectedIndexChanged += new System.EventHandler(this.cboHealContext_SelectedIndexChanged);
            // 
            // tabHotkeys
            // 
            this.tabHotkeys.Controls.Add(this.pgHotkeys);
            this.tabHotkeys.Location = new System.Drawing.Point(4, 22);
            this.tabHotkeys.Name = "tabHotkeys";
            this.tabHotkeys.Padding = new System.Windows.Forms.Padding(3);
            this.tabHotkeys.Size = new System.Drawing.Size(339, 381);
            this.tabHotkeys.TabIndex = 4;
            this.tabHotkeys.Text = "Hotkeys";
            this.tabHotkeys.UseVisualStyleBackColor = true;
            // 
            // pgHotkeys
            // 
            this.pgHotkeys.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgHotkeys.Location = new System.Drawing.Point(3, 3);
            this.pgHotkeys.Name = "pgHotkeys";
            this.pgHotkeys.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.pgHotkeys.Size = new System.Drawing.Size(333, 375);
            this.pgHotkeys.TabIndex = 1;
            // 
            // tabDebug
            // 
            this.tabDebug.Controls.Add(this.groupBox1);
            this.tabDebug.Controls.Add(this.groupBox3);
            this.tabDebug.Controls.Add(this.groupBox5);
            this.tabDebug.Controls.Add(this.groupBox2);
            this.tabDebug.Controls.Add(this.grpAuxTargeting);
            this.tabDebug.Location = new System.Drawing.Point(4, 22);
            this.tabDebug.Name = "tabDebug";
            this.tabDebug.Padding = new System.Windows.Forms.Padding(3);
            this.tabDebug.Size = new System.Drawing.Size(339, 381);
            this.tabDebug.TabIndex = 2;
            this.tabDebug.Text = "Debugging";
            this.tabDebug.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.lblTankToStayNear);
            this.groupBox1.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox1.Location = new System.Drawing.Point(7, 133);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(313, 38);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tank to Stay Near";
            // 
            // lblTankToStayNear
            // 
            this.lblTankToStayNear.AutoSize = true;
            this.lblTankToStayNear.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTankToStayNear.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblTankToStayNear.Location = new System.Drawing.Point(6, 16);
            this.lblTankToStayNear.Name = "lblTankToStayNear";
            this.lblTankToStayNear.Size = new System.Drawing.Size(35, 14);
            this.lblTankToStayNear.TabIndex = 0;
            this.lblTankToStayNear.Text = "None";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.cboDebugOutput);
            this.groupBox3.Controls.Add(this.cboForceUseOf);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.chkDebugCasting);
            this.groupBox3.Controls.Add(this.ShowPlayerNames);
            this.groupBox3.Controls.Add(this.chkDebugTrace);
            this.groupBox3.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox3.Location = new System.Drawing.Point(7, 275);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(314, 96);
            this.groupBox3.TabIndex = 9;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Miscellaneous Debug Settings";
            // 
            // cboForceUseOf
            // 
            this.cboForceUseOf.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboForceUseOf.FormattingEnabled = true;
            this.cboForceUseOf.Location = new System.Drawing.Point(6, 69);
            this.cboForceUseOf.Name = "cboForceUseOf";
            this.cboForceUseOf.Size = new System.Drawing.Size(130, 21);
            this.cboForceUseOf.TabIndex = 5;
            this.toolTip1.SetToolTip(this.cboForceUseOf, "*not saved* - Select behaviors to use on Taining Dummy");
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label5.Location = new System.Drawing.Point(143, 72);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(147, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "behaviors on Training Dummy";
            // 
            // chkDebugCasting
            // 
            this.chkDebugCasting.AutoSize = true;
            this.chkDebugCasting.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkDebugCasting.Location = new System.Drawing.Point(6, 42);
            this.chkDebugCasting.Name = "chkDebugCasting";
            this.chkDebugCasting.Size = new System.Drawing.Size(132, 17);
            this.chkDebugCasting.TabIndex = 2;
            this.chkDebugCasting.Text = "Debug Casting Engine";
            this.toolTip1.SetToolTip(this.chkDebugCasting, "Enable additional debug output for spell casting (more verbose)");
            this.chkDebugCasting.UseVisualStyleBackColor = true;
            this.chkDebugCasting.CheckedChanged += new System.EventHandler(this.chkDebugLogging_CheckedChanged);
            // 
            // ShowPlayerNames
            // 
            this.ShowPlayerNames.AutoSize = true;
            this.ShowPlayerNames.ForeColor = System.Drawing.SystemColors.ControlText;
            this.ShowPlayerNames.Location = new System.Drawing.Point(164, 42);
            this.ShowPlayerNames.Name = "ShowPlayerNames";
            this.ShowPlayerNames.Size = new System.Drawing.Size(121, 17);
            this.ShowPlayerNames.TabIndex = 4;
            this.ShowPlayerNames.Text = "Show Player Names";
            this.toolTip1.SetToolTip(this.ShowPlayerNames, "*not saved* - Use Player Name in log output instad of class name");
            this.ShowPlayerNames.UseVisualStyleBackColor = true;
            // 
            // chkDebugTrace
            // 
            this.chkDebugTrace.AutoSize = true;
            this.chkDebugTrace.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkDebugTrace.Location = new System.Drawing.Point(164, 19);
            this.chkDebugTrace.Name = "chkDebugTrace";
            this.chkDebugTrace.Size = new System.Drawing.Size(124, 17);
            this.chkDebugTrace.TabIndex = 3;
            this.chkDebugTrace.Text = "Trace Behavior Calls";
            this.toolTip1.SetToolTip(this.chkDebugTrace, "Enable Singular Behavior tracing -- EXTREMELY VERBOSE!!!");
            this.chkDebugTrace.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.lblPoi);
            this.groupBox5.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox5.Location = new System.Drawing.Point(7, 6);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(313, 38);
            this.groupBox5.TabIndex = 0;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "BotPoi (Point of Interest)";
            this.groupBox5.Enter += new System.EventHandler(this.groupBox2_Enter);
            // 
            // lblPoi
            // 
            this.lblPoi.AutoSize = true;
            this.lblPoi.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPoi.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblPoi.Location = new System.Drawing.Point(6, 16);
            this.lblPoi.Name = "lblPoi";
            this.lblPoi.Size = new System.Drawing.Size(35, 14);
            this.lblPoi.TabIndex = 0;
            this.lblPoi.Text = "None";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lblTargets);
            this.groupBox2.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox2.Location = new System.Drawing.Point(8, 50);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(313, 77);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Target List";
            this.groupBox2.Enter += new System.EventHandler(this.groupBox2_Enter);
            // 
            // lblTargets
            // 
            this.lblTargets.AutoSize = true;
            this.lblTargets.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTargets.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblTargets.Location = new System.Drawing.Point(6, 16);
            this.lblTargets.Name = "lblTargets";
            this.lblTargets.Size = new System.Drawing.Size(147, 14);
            this.lblTargets.TabIndex = 0;
            this.lblTargets.Text = "Target  99% @ 10 yds";
            // 
            // grpAuxTargeting
            // 
            this.grpAuxTargeting.Controls.Add(this.lblAuxTargets);
            this.grpAuxTargeting.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.grpAuxTargeting.Location = new System.Drawing.Point(8, 178);
            this.grpAuxTargeting.Name = "grpAuxTargeting";
            this.grpAuxTargeting.Size = new System.Drawing.Size(313, 93);
            this.grpAuxTargeting.TabIndex = 1;
            this.grpAuxTargeting.TabStop = false;
            this.grpAuxTargeting.Text = "Other Targeting";
            // 
            // lblAuxTargets
            // 
            this.lblAuxTargets.AutoSize = true;
            this.lblAuxTargets.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblAuxTargets.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblAuxTargets.Location = new System.Drawing.Point(6, 16);
            this.lblAuxTargets.Name = "lblAuxTargets";
            this.lblAuxTargets.Size = new System.Drawing.Size(133, 14);
            this.lblAuxTargets.TabIndex = 0;
            this.lblAuxTargets.Text = "Other Target @ ...";
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 250;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // grpFooter
            // 
            this.grpFooter.Controls.Add(this.btnLogMark);
            this.grpFooter.Controls.Add(this.btnSaveAndClose);
            this.grpFooter.Controls.Add(this.lblVersion);
            this.grpFooter.Controls.Add(this.label2);
            this.grpFooter.Controls.Add(this.label1);
            this.grpFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.grpFooter.ForeColor = System.Drawing.SystemColors.Control;
            this.grpFooter.Location = new System.Drawing.Point(0, 410);
            this.grpFooter.Margin = new System.Windows.Forms.Padding(0);
            this.grpFooter.Name = "grpFooter";
            this.grpFooter.Padding = new System.Windows.Forms.Padding(0);
            this.grpFooter.Size = new System.Drawing.Size(347, 71);
            this.grpFooter.TabIndex = 5;
            this.grpFooter.TabStop = false;
            // 
            // btnLogMark
            // 
            this.btnLogMark.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnLogMark.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnLogMark.Location = new System.Drawing.Point(132, 33);
            this.btnLogMark.Name = "btnLogMark";
            this.btnLogMark.Size = new System.Drawing.Size(96, 23);
            this.btnLogMark.TabIndex = 6;
            this.btnLogMark.Text = "LOGMARK!";
            this.toolTip1.SetToolTip(this.btnLogMark, "Add a LogMark to log file to simplify indicating where a problem occurred");
            this.btnLogMark.UseVisualStyleBackColor = true;
            this.btnLogMark.Click += new System.EventHandler(this.btnLogMark_Click);
            // 
            // btnSaveAndClose
            // 
            this.btnSaveAndClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSaveAndClose.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSaveAndClose.Location = new System.Drawing.Point(234, 33);
            this.btnSaveAndClose.Name = "btnSaveAndClose";
            this.btnSaveAndClose.Size = new System.Drawing.Size(96, 23);
            this.btnSaveAndClose.TabIndex = 7;
            this.btnSaveAndClose.Text = "Save && Close";
            this.btnSaveAndClose.UseVisualStyleBackColor = true;
            this.btnSaveAndClose.Click += new System.EventHandler(this.btnSaveAndClose_Click);
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblVersion.Location = new System.Drawing.Point(8, 51);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(46, 13);
            this.lblVersion.TabIndex = 6;
            this.lblVersion.Text = "v0.1.0.0";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label2.Location = new System.Drawing.Point(8, 38);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(92, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Community Driven";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Impact", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label1.Location = new System.Drawing.Point(6, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(79, 25);
            this.label1.TabIndex = 4;
            this.label1.Text = "Singular";
            // 
            // cboDebugOutput
            // 
            this.cboDebugOutput.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboDebugOutput.FormattingEnabled = true;
            this.cboDebugOutput.Location = new System.Drawing.Point(6, 16);
            this.cboDebugOutput.Name = "cboDebugOutput";
            this.cboDebugOutput.Size = new System.Drawing.Size(68, 21);
            this.cboDebugOutput.TabIndex = 5;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label6.Location = new System.Drawing.Point(80, 20);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(74, 13);
            this.label6.TabIndex = 1;
            this.label6.Text = "Debug Output";
            // 
            // ConfigurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(347, 481);
            this.Controls.Add(this.grpFooter);
            this.Controls.Add(this.tabControl1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigurationForm";
            this.ShowIcon = false;
            this.Text = "Singular Configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfigurationForm_FormClosing);
            this.Load += new System.EventHandler(this.ConfigurationForm_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabClass.ResumeLayout(false);
            this.tabGroupHeal.ResumeLayout(false);
            this.grpHealHeader.ResumeLayout(false);
            this.grpHealHeader.PerformLayout();
            this.tabHotkeys.ResumeLayout(false);
            this.tabDebug.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.grpAuxTargeting.ResumeLayout(false);
            this.grpAuxTargeting.PerformLayout();
            this.grpFooter.ResumeLayout(false);
            this.grpFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.PropertyGrid pgGeneral;
        private System.Windows.Forms.TabPage tabClass;
        private System.Windows.Forms.PropertyGrid pgClass;
        private System.Windows.Forms.TabPage tabDebug;
        private System.Windows.Forms.GroupBox grpAuxTargeting;
        private System.Windows.Forms.Label lblAuxTargets;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TabPage tabGroupHeal;
        private System.Windows.Forms.TabPage tabHotkeys;
        private System.Windows.Forms.PropertyGrid pgHotkeys;
        private System.Windows.Forms.GroupBox grpHealHeader;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cboHealContext;
        private System.Windows.Forms.GroupBox grpFooter;
        private System.Windows.Forms.Button btnSaveAndClose;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PropertyGrid pgHeal;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label lblTargets;
        private System.Windows.Forms.Button btnLogMark;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox ShowPlayerNames;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label lblPoi;
        private System.Windows.Forms.CheckBox chkDebugTrace;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ComboBox cboForceUseOf;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label lblTankToStayNear;
        private System.Windows.Forms.CheckBox chkDebugCasting;
        private System.Windows.Forms.ComboBox cboDebugOutput;
        private System.Windows.Forms.Label label6;
    }
}
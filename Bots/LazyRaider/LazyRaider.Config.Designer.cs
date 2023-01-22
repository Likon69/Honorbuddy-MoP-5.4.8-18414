namespace Styx.Bot.CustomBots
{
    partial class SelectTankForm
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
            this.colMaxHealth = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnClose = new System.Windows.Forms.Button();
            this.btnSetLeader = new System.Windows.Forms.Button();
            this.colRole = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listView = new System.Windows.Forms.ListView();
            this.colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colClass = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnRefresh = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkRaidBotLikeBehavior = new System.Windows.Forms.CheckBox();
            this.lblPauseKey = new System.Windows.Forms.Label();
            this.cboKeyPause = new System.Windows.Forms.ComboBox();
            this.chkAutoTargetOnlyIfNotValid = new System.Windows.Forms.CheckBox();
            this.chkPauseMessageInGame = new System.Windows.Forms.CheckBox();
            this.chkKeyPauseWhilePressed = new System.Windows.Forms.CheckBox();
            this.chkAutoTarget = new System.Windows.Forms.CheckBox();
            this.chkAutoSelectTank = new System.Windows.Forms.CheckBox();
            this.numFollowDistance = new System.Windows.Forms.NumericUpDown();
            this.lblFollowDistance = new System.Windows.Forms.Label();
            this.chkRunWithoutTank = new System.Windows.Forms.CheckBox();
            this.chkAutoFollow = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.numFPS = new System.Windows.Forms.NumericUpDown();
            this.chkDisablePlugins = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.chkLockMemory = new System.Windows.Forms.CheckBox();
            this.btnAutoSetup = new System.Windows.Forms.Button();
            this.btnRaidBotSettings = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.btnLowCpuSettings = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel1 = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.chkDisableCharacterManager = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFollowDistance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFPS)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // colMaxHealth
            // 
            this.colMaxHealth.Text = "Health";
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(344, 418);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(89, 23);
            this.btnClose.TabIndex = 6;
            this.btnClose.Text = "Close";
            this.toolTip1.SetToolTip(this.btnClose, "Close Settings Window");
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnSetLeader
            // 
            this.btnSetLeader.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSetLeader.Location = new System.Drawing.Point(6, 19);
            this.btnSetLeader.Name = "btnSetLeader";
            this.btnSetLeader.Size = new System.Drawing.Size(83, 23);
            this.btnSetLeader.TabIndex = 0;
            this.btnSetLeader.Text = "Set Tank";
            this.toolTip1.SetToolTip(this.btnSetLeader, "Set currently highlighted group member as Tank");
            this.btnSetLeader.UseVisualStyleBackColor = true;
            this.btnSetLeader.Click += new System.EventHandler(this.btnSetLeader_Click);
            // 
            // colRole
            // 
            this.colRole.Text = "Role";
            // 
            // listView
            // 
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colName,
            this.colClass,
            this.colRole,
            this.colMaxHealth});
            this.listView.FullRowSelect = true;
            this.listView.HideSelection = false;
            this.listView.LabelWrap = false;
            this.listView.Location = new System.Drawing.Point(12, 267);
            this.listView.MultiSelect = false;
            this.listView.Name = "listView";
            this.listView.ShowGroups = false;
            this.listView.ShowItemToolTips = true;
            this.listView.Size = new System.Drawing.Size(314, 174);
            this.listView.TabIndex = 3;
            this.toolTip1.SetToolTip(this.listView, "Click Column to Sort, DblClick Row to Select");
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            this.listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listView_ColumnClick);
            this.listView.SelectedIndexChanged += new System.EventHandler(this.listView_SelectedIndexChanged);
            this.listView.Click += new System.EventHandler(this.listView_Click);
            this.listView.DoubleClick += new System.EventHandler(this.listView_DoubleClick);
            // 
            // colName
            // 
            this.colName.Text = "Name";
            this.colName.Width = 120;
            // 
            // colClass
            // 
            this.colClass.Text = "Class";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRefresh.Location = new System.Drawing.Point(6, 48);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(83, 23);
            this.btnRefresh.TabIndex = 1;
            this.btnRefresh.Text = "Refresh";
            this.toolTip1.SetToolTip(this.btnRefresh, "Refresh list of Group Members");
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.Control;
            this.groupBox1.Controls.Add(this.chkRaidBotLikeBehavior);
            this.groupBox1.Controls.Add(this.lblPauseKey);
            this.groupBox1.Controls.Add(this.cboKeyPause);
            this.groupBox1.Controls.Add(this.chkAutoTargetOnlyIfNotValid);
            this.groupBox1.Controls.Add(this.chkPauseMessageInGame);
            this.groupBox1.Controls.Add(this.chkKeyPauseWhilePressed);
            this.groupBox1.Controls.Add(this.chkAutoTarget);
            this.groupBox1.Controls.Add(this.chkAutoSelectTank);
            this.groupBox1.Controls.Add(this.numFollowDistance);
            this.groupBox1.Controls.Add(this.lblFollowDistance);
            this.groupBox1.Controls.Add(this.chkRunWithoutTank);
            this.groupBox1.Controls.Add(this.chkAutoFollow);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(12, 57);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(314, 204);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Behavior";
            // 
            // chkRaidBotLikeBehavior
            // 
            this.chkRaidBotLikeBehavior.AutoSize = true;
            this.chkRaidBotLikeBehavior.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkRaidBotLikeBehavior.Location = new System.Drawing.Point(11, 17);
            this.chkRaidBotLikeBehavior.Name = "chkRaidBotLikeBehavior";
            this.chkRaidBotLikeBehavior.Size = new System.Drawing.Size(168, 17);
            this.chkRaidBotLikeBehavior.TabIndex = 0;
            this.chkRaidBotLikeBehavior.Text = "RaidBot-mode (disable all frills)";
            this.toolTip1.SetToolTip(this.chkRaidBotLikeBehavior, "Disable all plugins, following, targeting, and pause behaviors");
            this.chkRaidBotLikeBehavior.UseVisualStyleBackColor = true;
            this.chkRaidBotLikeBehavior.CheckedChanged += new System.EventHandler(this.chkRaidBotLikeBehavior_CheckedChanged);
            // 
            // lblPauseKey
            // 
            this.lblPauseKey.AutoSize = true;
            this.lblPauseKey.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPauseKey.Location = new System.Drawing.Point(11, 137);
            this.lblPauseKey.Name = "lblPauseKey";
            this.lblPauseKey.Size = new System.Drawing.Size(159, 13);
            this.lblPauseKey.TabIndex = 8;
            this.lblPauseKey.Text = "WOW Key to Pause LazyRaider";
            // 
            // cboKeyPause
            // 
            this.cboKeyPause.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboKeyPause.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboKeyPause.FormattingEnabled = true;
            this.cboKeyPause.Location = new System.Drawing.Point(177, 134);
            this.cboKeyPause.Name = "cboKeyPause";
            this.cboKeyPause.Size = new System.Drawing.Size(127, 21);
            this.cboKeyPause.TabIndex = 9;
            this.toolTip1.SetToolTip(this.cboKeyPause, "Keys which will toggle On / Off");
            this.cboKeyPause.SelectedIndexChanged += new System.EventHandler(this.cboKeyPause_SelectedIndexChanged);
            // 
            // chkAutoTargetOnlyIfNotValid
            // 
            this.chkAutoTargetOnlyIfNotValid.AutoSize = true;
            this.chkAutoTargetOnlyIfNotValid.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkAutoTargetOnlyIfNotValid.Location = new System.Drawing.Point(99, 107);
            this.chkAutoTargetOnlyIfNotValid.Name = "chkAutoTargetOnlyIfNotValid";
            this.chkAutoTargetOnlyIfNotValid.Size = new System.Drawing.Size(172, 17);
            this.chkAutoTargetOnlyIfNotValid.TabIndex = 7;
            this.chkAutoTargetOnlyIfNotValid.Text = "... only if not valid enemy target";
            this.toolTip1.SetToolTip(this.chkAutoTargetOnlyIfNotValid, "Stay on same target until killed");
            this.chkAutoTargetOnlyIfNotValid.UseVisualStyleBackColor = true;
            // 
            // chkPauseMessageInGame
            // 
            this.chkPauseMessageInGame.AutoSize = true;
            this.chkPauseMessageInGame.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkPauseMessageInGame.Location = new System.Drawing.Point(11, 183);
            this.chkPauseMessageInGame.Name = "chkPauseMessageInGame";
            this.chkPauseMessageInGame.Size = new System.Drawing.Size(186, 17);
            this.chkPauseMessageInGame.TabIndex = 11;
            this.chkPauseMessageInGame.Text = "Display Pause Messages in Game";
            this.chkPauseMessageInGame.UseVisualStyleBackColor = true;
            this.chkPauseMessageInGame.CheckedChanged += new System.EventHandler(this.chkAutoTarget_CheckedChanged);
            // 
            // chkKeyPauseWhilePressed
            // 
            this.chkKeyPauseWhilePressed.AutoSize = true;
            this.chkKeyPauseWhilePressed.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkKeyPauseWhilePressed.Location = new System.Drawing.Point(11, 160);
            this.chkKeyPauseWhilePressed.Name = "chkKeyPauseWhilePressed";
            this.chkKeyPauseWhilePressed.Size = new System.Drawing.Size(268, 17);
            this.chkKeyPauseWhilePressed.TabIndex = 10;
            this.chkKeyPauseWhilePressed.Text = "WOW Key Pause while Pressed ( instead of toggle)";
            this.chkKeyPauseWhilePressed.UseVisualStyleBackColor = true;
            this.chkKeyPauseWhilePressed.CheckedChanged += new System.EventHandler(this.chkAutoTarget_CheckedChanged);
            // 
            // chkAutoTarget
            // 
            this.chkAutoTarget.AutoSize = true;
            this.chkAutoTarget.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkAutoTarget.Location = new System.Drawing.Point(11, 107);
            this.chkAutoTarget.Name = "chkAutoTarget";
            this.chkAutoTarget.Size = new System.Drawing.Size(82, 17);
            this.chkAutoTarget.TabIndex = 6;
            this.chkAutoTarget.Text = "Auto Target";
            this.toolTip1.SetToolTip(this.chkAutoTarget, "Automatically select a DPS Target");
            this.chkAutoTarget.UseVisualStyleBackColor = true;
            this.chkAutoTarget.CheckedChanged += new System.EventHandler(this.chkAutoTarget_CheckedChanged);
            // 
            // chkAutoSelectTank
            // 
            this.chkAutoSelectTank.AutoSize = true;
            this.chkAutoSelectTank.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkAutoSelectTank.Location = new System.Drawing.Point(11, 84);
            this.chkAutoSelectTank.Name = "chkAutoSelectTank";
            this.chkAutoSelectTank.Size = new System.Drawing.Size(109, 17);
            this.chkAutoSelectTank.TabIndex = 5;
            this.chkAutoSelectTank.Text = "Auto Select Tank";
            this.toolTip1.SetToolTip(this.chkAutoSelectTank, "Auto select new tank if tank out of range");
            this.chkAutoSelectTank.UseVisualStyleBackColor = true;
            // 
            // numFollowDistance
            // 
            this.numFollowDistance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numFollowDistance.Location = new System.Drawing.Point(259, 60);
            this.numFollowDistance.Name = "numFollowDistance";
            this.numFollowDistance.Size = new System.Drawing.Size(45, 20);
            this.numFollowDistance.TabIndex = 4;
            this.numFollowDistance.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.toolTip1.SetToolTip(this.numFollowDistance, "Distance to follow behind Tank");
            // 
            // lblFollowDistance
            // 
            this.lblFollowDistance.AutoSize = true;
            this.lblFollowDistance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFollowDistance.Location = new System.Drawing.Point(174, 62);
            this.lblFollowDistance.Name = "lblFollowDistance";
            this.lblFollowDistance.Size = new System.Drawing.Size(82, 13);
            this.lblFollowDistance.TabIndex = 3;
            this.lblFollowDistance.Text = "Follow Distance";
            this.lblFollowDistance.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkRunWithoutTank
            // 
            this.chkRunWithoutTank.AutoSize = true;
            this.chkRunWithoutTank.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkRunWithoutTank.Location = new System.Drawing.Point(11, 40);
            this.chkRunWithoutTank.Name = "chkRunWithoutTank";
            this.chkRunWithoutTank.Size = new System.Drawing.Size(176, 17);
            this.chkRunWithoutTank.TabIndex = 1;
            this.chkRunWithoutTank.Text = "Run Without a Tank (no leader)";
            this.toolTip1.SetToolTip(this.chkRunWithoutTank, "Do not select, follow, or assist Tank");
            this.chkRunWithoutTank.UseVisualStyleBackColor = true;
            this.chkRunWithoutTank.CheckedChanged += new System.EventHandler(this.chkDisableTank_CheckedChanged);
            // 
            // chkAutoFollow
            // 
            this.chkAutoFollow.AutoSize = true;
            this.chkAutoFollow.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkAutoFollow.Location = new System.Drawing.Point(11, 61);
            this.chkAutoFollow.Name = "chkAutoFollow";
            this.chkAutoFollow.Size = new System.Drawing.Size(149, 17);
            this.chkAutoFollow.TabIndex = 2;
            this.chkAutoFollow.Text = "Automatically Follow Tank";
            this.toolTip1.SetToolTip(this.chkAutoFollow, "Follow tank when not in combat");
            this.chkAutoFollow.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(9, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(27, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "FPS";
            // 
            // numFPS
            // 
            this.numFPS.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numFPS.Location = new System.Drawing.Point(42, 16);
            this.numFPS.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numFPS.Name = "numFPS";
            this.numFPS.Size = new System.Drawing.Size(45, 20);
            this.numFPS.TabIndex = 1;
            this.numFPS.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.toolTip1.SetToolTip(this.numFPS, "Frames Per Second that Bot runs at");
            this.numFPS.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // chkDisablePlugins
            // 
            this.chkDisablePlugins.AutoSize = true;
            this.chkDisablePlugins.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkDisablePlugins.Location = new System.Drawing.Point(12, 40);
            this.chkDisablePlugins.Name = "chkDisablePlugins";
            this.chkDisablePlugins.Size = new System.Drawing.Size(101, 17);
            this.chkDisablePlugins.TabIndex = 2;
            this.chkDisablePlugins.Text = "Disable Plug-ins";
            this.toolTip1.SetToolTip(this.chkDisablePlugins, "Faster response (less overhead) by Disable Plug-ins");
            this.chkDisablePlugins.UseVisualStyleBackColor = false;
            // 
            // groupBox2
            // 
            this.groupBox2.BackColor = System.Drawing.SystemColors.Control;
            this.groupBox2.Controls.Add(this.chkDisableCharacterManager);
            this.groupBox2.Controls.Add(this.chkLockMemory);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.chkDisablePlugins);
            this.groupBox2.Controls.Add(this.numFPS);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(343, 57);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(203, 204);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Performance";
            // 
            // chkLockMemory
            // 
            this.chkLockMemory.AutoSize = true;
            this.chkLockMemory.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkLockMemory.Location = new System.Drawing.Point(12, 82);
            this.chkLockMemory.Name = "chkLockMemory";
            this.chkLockMemory.Size = new System.Drawing.Size(181, 17);
            this.chkLockMemory.TabIndex = 3;
            this.chkLockMemory.Text = "Frame Lock (not all CC\'s support)";
            this.toolTip1.SetToolTip(this.chkLockMemory, "Faster response by locking memory (may make unstable)");
            this.chkLockMemory.UseVisualStyleBackColor = true;
            // 
            // btnAutoSetup
            // 
            this.btnAutoSetup.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAutoSetup.Location = new System.Drawing.Point(7, 19);
            this.btnAutoSetup.Name = "btnAutoSetup";
            this.btnAutoSetup.Size = new System.Drawing.Size(83, 23);
            this.btnAutoSetup.TabIndex = 0;
            this.btnAutoSetup.Text = "Auto";
            this.toolTip1.SetToolTip(this.btnAutoSetup, "Auto detect and setup for balance of features and performance");
            this.btnAutoSetup.UseVisualStyleBackColor = true;
            this.btnAutoSetup.Click += new System.EventHandler(this.btnAutoSetup_Click);
            // 
            // btnRaidBotSettings
            // 
            this.btnRaidBotSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRaidBotSettings.Location = new System.Drawing.Point(7, 48);
            this.btnRaidBotSettings.Name = "btnRaidBotSettings";
            this.btnRaidBotSettings.Size = new System.Drawing.Size(83, 23);
            this.btnRaidBotSettings.TabIndex = 1;
            this.btnRaidBotSettings.Text = "RaidBot";
            this.toolTip1.SetToolTip(this.btnRaidBotSettings, "For RaidBot users wantting to match behavior and performance");
            this.btnRaidBotSettings.UseVisualStyleBackColor = true;
            this.btnRaidBotSettings.Click += new System.EventHandler(this.btnRaidBotSettings_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.BackColor = System.Drawing.SystemColors.Control;
            this.groupBox3.Controls.Add(this.btnAutoSetup);
            this.groupBox3.Controls.Add(this.btnLowCpuSettings);
            this.groupBox3.Controls.Add(this.btnRaidBotSettings);
            this.groupBox3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox3.Location = new System.Drawing.Point(449, 269);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(97, 127);
            this.groupBox3.TabIndex = 5;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "1-click Setup";
            // 
            // btnLowCpuSettings
            // 
            this.btnLowCpuSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnLowCpuSettings.Location = new System.Drawing.Point(7, 77);
            this.btnLowCpuSettings.Name = "btnLowCpuSettings";
            this.btnLowCpuSettings.Size = new System.Drawing.Size(83, 23);
            this.btnLowCpuSettings.TabIndex = 2;
            this.btnLowCpuSettings.Text = "Low CPU";
            this.toolTip1.SetToolTip(this.btnLowCpuSettings, "For users with excessive lag or without a high end computer");
            this.btnLowCpuSettings.UseVisualStyleBackColor = true;
            this.btnLowCpuSettings.Click += new System.EventHandler(this.btnLowCpuSettings_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.BackColor = System.Drawing.SystemColors.Control;
            this.groupBox4.Controls.Add(this.btnSetLeader);
            this.groupBox4.Controls.Add(this.btnRefresh);
            this.groupBox4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox4.Location = new System.Drawing.Point(344, 269);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(97, 127);
            this.groupBox4.TabIndex = 4;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Tank List";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.lblVersion);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Font = new System.Drawing.Font("Courier New", 28F, System.Drawing.FontStyle.Bold);
            this.panel1.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(557, 48);
            this.panel1.TabIndex = 0;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.label5.Location = new System.Drawing.Point(324, 23);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(195, 16);
            this.label5.TabIndex = 3;
            this.label5.Text = "control movement and targeting";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.label4.Location = new System.Drawing.Point(324, 6);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(221, 16);
            this.label4.TabIndex = 2;
            this.label4.Text = "A manual assist BotBase where you";
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblVersion.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVersion.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.lblVersion.Location = new System.Drawing.Point(167, 8);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(89, 32);
            this.lblVersion.TabIndex = 1;
            this.lblVersion.Text = "v1.0.11";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.label2.Location = new System.Drawing.Point(3, 2);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(167, 40);
            this.label2.TabIndex = 0;
            this.label2.Text = "LazyRaider";
            // 
            // chkDisableCharacterManager
            // 
            this.chkDisableCharacterManager.AutoSize = true;
            this.chkDisableCharacterManager.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkDisableCharacterManager.Location = new System.Drawing.Point(12, 61);
            this.chkDisableCharacterManager.Name = "chkDisableCharacterManager";
            this.chkDisableCharacterManager.Size = new System.Drawing.Size(155, 17);
            this.chkDisableCharacterManager.TabIndex = 3;
            this.chkDisableCharacterManager.Text = "Disable roll and loot";
            this.toolTip1.SetToolTip(this.chkDisableCharacterManager, "Disables HonorBuddy roll on loot and auto-equip");
            this.chkDisableCharacterManager.UseVisualStyleBackColor = true;
            // 
            // SelectTankForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(557, 449);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.listView);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "SelectTankForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Activated += new System.EventHandler(this.SelectTankForm_Activated);
            this.Deactivate += new System.EventHandler(this.SelectTankForm_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SelectTankForm_FormClosing);
            this.Shown += new System.EventHandler(this.SelectTankForm_Shown);
            this.VisibleChanged += new System.EventHandler(this.SelectTankForm_VisibleChanged);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFollowDistance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFPS)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ColumnHeader colMaxHealth;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnSetLeader;
        private System.Windows.Forms.ColumnHeader colRole;
        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.ColumnHeader colName;
        private System.Windows.Forms.ColumnHeader colClass;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.NumericUpDown numFollowDistance;
        private System.Windows.Forms.Label lblFollowDistance;
        private System.Windows.Forms.CheckBox chkAutoFollow;
        private System.Windows.Forms.CheckBox chkRunWithoutTank;
        private System.Windows.Forms.CheckBox chkAutoSelectTank;
        private System.Windows.Forms.CheckBox chkDisablePlugins;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numFPS;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox chkLockMemory;
        private System.Windows.Forms.Button btnAutoSetup;
        private System.Windows.Forms.CheckBox chkAutoTarget;
        private System.Windows.Forms.Button btnRaidBotSettings;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox chkRaidBotLikeBehavior;
        private System.Windows.Forms.Button btnLowCpuSettings;
        private System.Windows.Forms.CheckBox chkAutoTargetOnlyIfNotValid;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lblPauseKey;
        private System.Windows.Forms.ComboBox cboKeyPause;
        private System.Windows.Forms.CheckBox chkPauseMessageInGame;
        private System.Windows.Forms.CheckBox chkKeyPauseWhilePressed;
        private System.Windows.Forms.CheckBox chkDisableCharacterManager;

    }
}
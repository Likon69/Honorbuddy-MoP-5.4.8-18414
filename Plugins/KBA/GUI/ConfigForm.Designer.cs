namespace KBA.GUI
{
    partial class ConfigForm
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
            this.Description = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ItemName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ItemId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BuffName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BuffId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SituationalUse = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.SituationalValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Active = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.pbSpell = new System.Windows.Forms.PictureBox();
            this.pbItem = new System.Windows.Forms.PictureBox();
            this.pbPotion = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.pbSpell)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbItem)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbPotion)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // Description
            // 
            this.Description.HeaderText = "Description";
            this.Description.Name = "Description";
            this.Description.Width = 85;
            // 
            // ItemName
            // 
            this.ItemName.HeaderText = "ItemName";
            this.ItemName.Name = "ItemName";
            this.ItemName.Width = 80;
            // 
            // ItemId
            // 
            this.ItemId.HeaderText = "ItemId";
            this.ItemId.Name = "ItemId";
            this.ItemId.Width = 61;
            // 
            // BuffName
            // 
            this.BuffName.HeaderText = "BuffName";
            this.BuffName.Name = "BuffName";
            this.BuffName.Width = 79;
            // 
            // BuffId
            // 
            this.BuffId.HeaderText = "BuffId";
            this.BuffId.Name = "BuffId";
            this.BuffId.Width = 60;
            // 
            // SituationalUse
            // 
            this.SituationalUse.HeaderText = "SituationalUse";
            this.SituationalUse.Items.AddRange(new object[] {
            "Always",
            "Heroism",
            "LowHealth",
            "LowMana",
            "TimelessIsle"});
            this.SituationalUse.Name = "SituationalUse";
            this.SituationalUse.Width = 81;
            // 
            // SituationalValue
            // 
            this.SituationalValue.HeaderText = "SituationalValue";
            this.SituationalValue.Name = "SituationalValue";
            this.SituationalValue.Width = 108;
            // 
            // Active
            // 
            this.Active.HeaderText = "Active";
            this.Active.Name = "Active";
            this.Active.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Active.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.Active.Width = 62;
            // 
            // pbSpell
            // 
            this.pbSpell.Location = new System.Drawing.Point(198, 84);
            this.pbSpell.Name = "pbSpell";
            this.pbSpell.Size = new System.Drawing.Size(66, 66);
            this.pbSpell.TabIndex = 1;
            this.pbSpell.TabStop = false;
            // 
            // pbItem
            // 
            this.pbItem.Location = new System.Drawing.Point(120, 46);
            this.pbItem.Name = "pbItem";
            this.pbItem.Size = new System.Drawing.Size(66, 66);
            this.pbItem.TabIndex = 2;
            this.pbItem.TabStop = false;
            // 
            // pbPotion
            // 
            this.pbPotion.Location = new System.Drawing.Point(9, 9);
            this.pbPotion.Name = "pbPotion";
            this.pbPotion.Size = new System.Drawing.Size(98, 141);
            this.pbPotion.TabIndex = 3;
            this.pbPotion.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Minerva", 20.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(114, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 34);
            this.label1.TabIndex = 4;
            this.label1.Text = "Potions";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Minerva", 20.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(192, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(71, 34);
            this.label2.TabIndex = 5;
            this.label2.Text = "Items";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Minerva", 20.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(270, 84);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(76, 34);
            this.label3.TabIndex = 6;
            this.label3.Text = "Spells";
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(499, 150);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(174, 23);
            this.btnDelete.TabIndex = 7;
            this.btnDelete.Text = "&Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(447, 42);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(174, 23);
            this.btnSave.TabIndex = 8;
            this.btnSave.Text = "&Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(447, 71);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(174, 23);
            this.btnClose.TabIndex = 9;
            this.btnClose.Text = "&Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(447, 98);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(174, 23);
            this.btnAdd.TabIndex = 10;
            this.btnAdd.Text = "&Add new";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(9, 179);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(664, 150);
            this.dataGridView1.TabIndex = 11;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            // 
            // ConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(685, 455);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pbPotion);
            this.Controls.Add(this.pbItem);
            this.Controls.Add(this.pbSpell);
            this.Name = "ConfigForm";
            this.Text = "Settings";
            this.Load += new System.EventHandler(this.ConfigForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pbSpell)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbItem)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbPotion)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pbSpell;
        private System.Windows.Forms.PictureBox pbItem;
        private System.Windows.Forms.PictureBox pbPotion;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.DataGridViewTextBoxColumn Description;
        private System.Windows.Forms.DataGridViewTextBoxColumn ItemName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ItemId;
        private System.Windows.Forms.DataGridViewTextBoxColumn BuffName;
        private System.Windows.Forms.DataGridViewTextBoxColumn BuffId;
        private System.Windows.Forms.DataGridViewComboBoxColumn SituationalUse;
        private System.Windows.Forms.DataGridViewTextBoxColumn SituationalValue;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Active;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.DataGridView dataGridView1;

    }
}
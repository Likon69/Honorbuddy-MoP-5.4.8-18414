namespace KBA.GUI
{
    partial class BuffsDialog
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
            this.txtObjectId = new System.Windows.Forms.TextBox();
            this.txtObjectName = new System.Windows.Forms.TextBox();
            this.txtBuffId = new System.Windows.Forms.TextBox();
            this.txtBuffName = new System.Windows.Forms.TextBox();
            this.cmbBuffWhere = new System.Windows.Forms.ComboBox();
            this.cmbBuffWhen = new System.Windows.Forms.ComboBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.toolTip2 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // txtObjectId
            // 
            this.txtObjectId.Location = new System.Drawing.Point(167, 7);
            this.txtObjectId.Name = "txtObjectId";
            this.txtObjectId.Size = new System.Drawing.Size(176, 20);
            this.txtObjectId.TabIndex = 0;
            this.toolTip2.SetToolTip(this.txtObjectId, "The ID of the Item or Spell you want to be used. We prefer IDs over Names. One of" +
        " both MUST be filled.");
            // 
            // txtObjectName
            // 
            this.txtObjectName.Location = new System.Drawing.Point(167, 33);
            this.txtObjectName.Name = "txtObjectName";
            this.txtObjectName.Size = new System.Drawing.Size(176, 20);
            this.txtObjectName.TabIndex = 1;
            this.toolTip2.SetToolTip(this.txtObjectName, "The Name of the Item or Spell you want to be used. We prefer IDs over Names. One " +
        "of both MUST be filled.");
            // 
            // txtBuffId
            // 
            this.txtBuffId.Location = new System.Drawing.Point(167, 59);
            this.txtBuffId.Name = "txtBuffId";
            this.txtBuffId.Size = new System.Drawing.Size(176, 20);
            this.txtBuffId.TabIndex = 2;
            this.toolTip2.SetToolTip(this.txtBuffId, "The ID of the Aura that must be missing if you want to used the defined Spell / I" +
        "tem. \r\nWe prefer IDs over Names.");
            // 
            // txtBuffName
            // 
            this.txtBuffName.Location = new System.Drawing.Point(167, 85);
            this.txtBuffName.Name = "txtBuffName";
            this.txtBuffName.Size = new System.Drawing.Size(176, 20);
            this.txtBuffName.TabIndex = 3;
            this.toolTip2.SetToolTip(this.txtBuffName, "The ID of the Aura that must be missing if you want to used the defined Spell / I" +
        "tem. \r\nWe prefer IDs over Names.\r\n");
            // 
            // cmbBuffWhere
            // 
            this.cmbBuffWhere.FormattingEnabled = true;
            this.cmbBuffWhere.Location = new System.Drawing.Point(167, 111);
            this.cmbBuffWhere.Name = "cmbBuffWhere";
            this.cmbBuffWhere.Size = new System.Drawing.Size(176, 21);
            this.cmbBuffWhere.TabIndex = 4;
            this.toolTip2.SetToolTip(this.cmbBuffWhere, "Where should the item / spell be used.");
            // 
            // cmbBuffWhen
            // 
            this.cmbBuffWhen.FormattingEnabled = true;
            this.cmbBuffWhen.Location = new System.Drawing.Point(167, 138);
            this.cmbBuffWhen.Name = "cmbBuffWhen";
            this.cmbBuffWhen.Size = new System.Drawing.Size(176, 21);
            this.cmbBuffWhen.TabIndex = 5;
            this.toolTip2.SetToolTip(this.cmbBuffWhen, "When should the item / spell be used");
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(462, 5);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 6;
            this.btnSave.Text = "&Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(462, 63);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(28, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 13);
            this.label1.TabIndex = 9;
            this.label1.Text = "Spell / Item ID";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(28, 36);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(92, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "Spell / Item Name";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(28, 59);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(88, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "Aura ID to check";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(28, 85);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(105, 13);
            this.label4.TabIndex = 12;
            this.label4.Text = "Aura Name to check";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(28, 114);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(103, 13);
            this.label5.TabIndex = 13;
            this.label5.Text = "Situation /  Location";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(30, 138);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(51, 13);
            this.label6.TabIndex = 14;
            this.label6.Text = "Condition";
            // 
            // BuffsDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(541, 244);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.cmbBuffWhen);
            this.Controls.Add(this.cmbBuffWhere);
            this.Controls.Add(this.txtBuffName);
            this.Controls.Add(this.txtBuffId);
            this.Controls.Add(this.txtObjectName);
            this.Controls.Add(this.txtObjectId);
            this.Name = "BuffsDialog";
            this.Text = "BuffsDialog";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtObjectId;
        private System.Windows.Forms.TextBox txtObjectName;
        private System.Windows.Forms.TextBox txtBuffId;
        private System.Windows.Forms.TextBox txtBuffName;
        private System.Windows.Forms.ComboBox cmbBuffWhere;
        private System.Windows.Forms.ComboBox cmbBuffWhen;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip toolTip2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
    }
}
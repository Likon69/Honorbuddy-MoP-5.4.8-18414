using System;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KBA.Helpers;

namespace KBA.GUI
{
    public partial class BuffsDialog : Form
    {
        public bool ValidRecordFound { get; private set; }

        private BuffEntry CurrentRecord { get; set; }

        private bool NewRecordStarted { get; set; }
        public BuffsDialog()
        {
            InitializeComponent();
        }

        public void Init(int id)
        {
            ValidRecordFound = false;
            NewRecordStarted = false;

            cmbBuffWhere.DataSource = Enum.GetValues(typeof(BuffWhere));
            cmbBuffWhen.DataSource = Enum.GetValues(typeof(BuffWhen));

            // find the DispelableSpell
            CurrentRecord = Buffs.Instance.SpellList.Spells.Find(s => s.ObjectId == id);

            if (CurrentRecord == null) return;

            ValidRecordFound = true;

            // Populate the Form..
            txtObjectId.Text = CurrentRecord.ObjectId.ToString(CultureInfo.InvariantCulture);
            txtObjectName.Text = CurrentRecord.ObjectName;
            txtBuffId.Text = CurrentRecord.BuffId.ToString(CultureInfo.InvariantCulture);
            txtBuffName.Text = CurrentRecord.BuffName.ToString(CultureInfo.InvariantCulture);
            cmbBuffWhere.SelectedItem = CurrentRecord.BuffLocation;
            cmbBuffWhen.SelectedItem = CurrentRecord.BuffCondition;
        }

        public void InitNew()
        {
            NewRecordStarted = true;

            cmbBuffWhere.DataSource = Enum.GetValues(typeof(BuffWhere));
            cmbBuffWhen.DataSource = Enum.GetValues(typeof(BuffWhen));
            // Populate the Form..
            txtObjectId.Text = "0";
            txtObjectName.Text = "";
            txtBuffId.Text = "0";
            txtBuffName.Text = "0";
            cmbBuffWhere.SelectedItem = BuffWhere.NoWhere;
            cmbBuffWhen.SelectedItem = BuffWhen.Never;
        }

        private void CreateNewRecord()
        {
            var ObjectName = txtObjectName.Text;
            var ObjectId = Convert.ToInt32(txtObjectId.Text);
            var BuffId = Convert.ToInt32(txtBuffId.Text);
            var BuffName = txtBuffName.Text;
            var BuffLocation = GetBuffWhere();
            var BuffCondition = GetBuffWhen();

            Buffs.Instance.SpellList.Add(ObjectId, ObjectName, BuffId, BuffName, BuffLocation, BuffCondition);
            Logger.InfoLog(string.Format("ObjectName: {0} ObjectId: {1}  BuffName: {2}, BuffId: {3} Where: {4} When: {5}", ObjectName, ObjectId, BuffName, BuffId, BuffLocation, BuffCondition));
        }

        private BuffWhen GetBuffWhen()
        {
            BuffWhen dspType;
            Enum.TryParse(cmbBuffWhen.SelectedValue.ToString(), out dspType);

            return dspType;
        }

        private BuffWhere GetBuffWhere()
        {
            BuffWhere dspType;
            Enum.TryParse(cmbBuffWhere.SelectedValue.ToString(), out dspType);

            return dspType;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (NewRecordStarted)
            {
                CreateNewRecord();
                DialogResult = DialogResult.OK;
                return;
            }

            var result = Buffs.Instance.SpellList.Spells.Find(s => s.ObjectId == CurrentRecord.ObjectId);
            if (result == null) return;

            // Save to memory..
            result.ObjectId = Convert.ToInt32(txtObjectId.Text);
            result.ObjectName = txtObjectName.Text;
            result.BuffId = Convert.ToInt32(txtBuffId.Text);
            result.BuffName = txtBuffName.Text;
            result.BuffLocation = GetBuffWhere();
            result.BuffCondition = GetBuffWhen();

            Logger.InfoLog(" Buff changes applied for {0}", CurrentRecord.ObjectId);

            DialogResult = DialogResult.OK;
        }
    }
}

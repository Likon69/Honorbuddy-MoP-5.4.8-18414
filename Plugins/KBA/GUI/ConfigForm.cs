using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using KBA.Helpers;
using Styx.Common;
namespace KBA.GUI
{
    public partial class ConfigForm : Form
    {
        public ConfigForm()
        {
            InitializeComponent();
            string IconToLoad = "";
            try
            {
                IconToLoad = string.Format("{0}\\Plugins\\KBA\\Images\\spell.png", Utilities.AssemblyDirectory);
                if (File.Exists(IconToLoad))
                    pbSpell.Image = new Bitmap(IconToLoad);
                IconToLoad = string.Format("{0}\\Plugins\\KBA\\Images\\potion.png", Utilities.AssemblyDirectory);
                if (File.Exists(IconToLoad))
                    pbPotion.Image = new Bitmap(IconToLoad);
                IconToLoad = string.Format("{0}\\Plugins\\KBA\\Images\\item.png", Utilities.AssemblyDirectory);
                if (File.Exists(IconToLoad))
                    pbItem.Image = new Bitmap(IconToLoad);
            }
            catch (Exception ex)
            {
                Logger.DebugLog(ex.ToString());
            }
        }

        private void ConfigForm_Load(object sender, EventArgs e)
        {
            dataGridView1.DataSource = Buffs.Instance.SpellList.Spells;

        }
        protected override void OnClosing(CancelEventArgs e)
        {
            SaveData();
            base.OnClosing(e);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            //save and close
            SaveData();
            base.Close();
        }
        private void SaveData()
        {
            try
            {
                Buffs.Instance.SpellList.Save();
                Logger.InfoLog("current list saved");
            }
            catch (Exception e)
            {
                Logger.InfoLog(e.ToString());
            }
        }
        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveData();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedCells.Count > 0)
            {
                int selectedrowindex = dataGridView1.SelectedCells[0].RowIndex;

                DataGridViewRow selectedRow = dataGridView1.Rows[selectedrowindex];
                int id = Convert.ToInt32(selectedRow.Cells["ObjectId"].Value);
                //Buffs.Instance.SpellList.Spells.Where(q=>q.ObjectId==id)
                var itemToRemove = Buffs.Instance.SpellList.Spells.SingleOrDefault(r => r.ObjectId == id);
                if (itemToRemove != null)
                {
                    Buffs.Instance.SpellList.Spells.Remove(itemToRemove);
                    Logger.InfoLog("deleted {0} from list",itemToRemove.ToString());
                    SaveData();
                    dataGridView1.DataSource = null;
                    dataGridView1.DataSource = Buffs.Instance.SpellList.Spells;
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var dlgBuff = new BuffsDialog();

            dlgBuff.InitNew();

            DialogResult settingsChoice = dlgBuff.ShowDialog();
            if (settingsChoice == DialogResult.OK)
            {
                dataGridView1.DataSource = null;
                dataGridView1.DataSource = Buffs.Instance.SpellList.Spells;
            }
        }
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count > 0)
            {
                int selectedrowindex = dataGridView1.SelectedCells[0].RowIndex;

                DataGridViewRow selectedRow = dataGridView1.Rows[selectedrowindex];
                string id = Convert.ToString(selectedRow.Cells["ObjectId"].Value);
                var dlgDispel = new BuffsDialog();
                dlgDispel.Init(Convert.ToInt32(id));
                if (dlgDispel.ValidRecordFound)
                {
                    DialogResult settingsChoice = dlgDispel.ShowDialog();
                    if (settingsChoice == DialogResult.OK)
                    {
                        dataGridView1.DataSource = null;
                        dataGridView1.DataSource = Buffs.Instance.SpellList.Spells;
                    }
                }
            }
        }

    }
}

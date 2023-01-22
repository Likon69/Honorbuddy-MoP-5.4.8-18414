using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using P = RaresAndChests.RareSettings;

namespace RaresAndChests
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            checkBox1.Checked = P.myPrefs.CombatLooter;
            checkBox2.Checked = P.myPrefs.ChestFinder;
            checkBox3.Checked = P.myPrefs.Rarefinder;
            numericUpDown1.Value = P.myPrefs.radius;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            P.myPrefs.Save();
            Close();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            P.myPrefs.CombatLooter = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            P.myPrefs.ChestFinder = checkBox2.Checked;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            P.myPrefs.Rarefinder = checkBox3.Checked;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            P.myPrefs.radius = (int)numericUpDown1.Value;
        }
    }
}

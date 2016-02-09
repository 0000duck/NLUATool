using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;

namespace NLuaToolVSIX
{
    public partial class LaunchFrom : Form
    {
        DTE2 _dte;
        string _filePath;
        public LaunchFrom(DTE2 dte)
        {
            _dte = dte;
            InitializeComponent();
        }
        public string FilePath
        {
            get { return this._filePath; }
        }

        public string SelectLuaFile { get; set; }

        private void LaunchFrom_Load(object sender, EventArgs e)
        {
            if (_dte.ActiveDocument != null)
            {
                string activeDocName = _dte.ActiveDocument.FullName;
                cmbProjects.Items.Add(activeDocName);
            }

            if (this.cmbProjects.Items.Count > 0)
                this.cmbProjects.SelectedIndex = 0;
        }

        private void btnLaunch_Click(object sender, EventArgs e)
        {
            if (_filePath != null)
            {
                SelectLuaFile = cmbProjects.SelectedItem.ToString();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.No;
            this.Close();

        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Lua Hosts|*.exe";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.lblPath.Text = openFileDialog.FileName;
                _filePath = openFileDialog.FileName;
            }
        }
    }
}

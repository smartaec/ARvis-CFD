using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VtkToolkit
{
    public partial class MainForm :Form
    {
        private List<FileItem> fileItems=new List<FileItem>();
        public MainForm()
        {
            InitializeComponent();
            lvFiles.AfterLabelEdit+=LvFiles_AfterLabelEdit;
            toolTip.SetToolTip(lvFiles, "Edit order to change sequence of time series.");
        }

        private void LvFiles_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            var item = lvFiles.Items[e.Item].Tag as FileItem;
            int order = 0;
            if(int.TryParse(e.Label, out order)) {
                item.Order=order;
                lvFiles.BeginInvoke((update)UpdateListView);
            } else {
                MessageBox.Show("Please input an integer.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.CancelEdit=true;
            }
        }

        private delegate void update();
        private void btnAddFile_Click(object sender, EventArgs e)
        {
            using(var dlg = new OpenFileDialog()) {
                dlg.Title="Select vtk file";
                dlg.Filter="(vtk file)|*.vtk|(all files)|*.*";
                dlg.Multiselect=true;
                if(dlg.ShowDialog()==DialogResult.OK) {
                    var orderStart = fileItems.Count;
                    foreach(var fileName in dlg.FileNames) {
                        var item = new FileItem();
                        item.Order=orderStart++;
                        item.Path=fileName;
                        fileItems.Add(item);
                    }

                    UpdateListView();
                }
            }
        }

        void UpdateListView()
        {
            lvFiles.BeginUpdate();
            lvFiles.Items.Clear();
            foreach(var fileItem in fileItems.OrderBy(i => i.Order)) {
                var item = new ListViewItem(new string[] { fileItem.Order.ToString(), fileItem.Path, fileItem.Model==null ? "Unloaded" : "Loaded" });
                item.Tag=fileItem;
                lvFiles.Items.Add(item);
            }
            lvFiles.EndUpdate();
        }

        private void btnDelFile_Click(object sender, EventArgs e)
        {
            if(lvFiles.SelectedItems.Count>0) {
                foreach(ListViewItem item in lvFiles.SelectedItems) {
                    fileItems.Remove(item.Tag as FileItem);
                }
                UpdateListView();
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if(lvFiles.SelectedItems.Count>0) {
                var fItems = lvFiles.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as FileItem).ToArray();
                using(var dlg = new SaveFileDialog()) {
                    dlg.Title="Save c4a file";
                    dlg.Filter="(c4a file)|*.c4a";
                    if(dlg.ShowDialog()==DialogResult.OK) {
                        var transfer = new VtkTransfer();
                        bool slice = MessageBox.Show("Slice mesh into submeshes(will be saved in the same file)?", "Help", MessageBoxButtons.YesNo, MessageBoxIcon.Question)==DialogResult.Yes;
                        transfer.Convert(fItems.OrderBy(f => f.Order).Select(f => f.Path).ToList(), dlg.FileName, slice);
                    }
                }
            }
        }

        private void btnSaveAll_Click(object sender, EventArgs e)
        {
            if(lvFiles.Items.Count>0) {
                var fItems = lvFiles.Items.Cast<ListViewItem>().Select(i => i.Tag as FileItem).ToArray();
                using(var dlg = new SaveFileDialog()) {
                    dlg.Title="Save c4a file";
                    dlg.Filter="(c4a file)|*.c4a";
                    if(dlg.ShowDialog()==DialogResult.OK) {
                        var transfer = new VtkTransfer();
                        bool slice = MessageBox.Show("Slice mesh into submeshes(will be saved in the same file)?", "Help", MessageBoxButtons.YesNo, MessageBoxIcon.Question)==DialogResult.Yes;
                        transfer.Convert(fItems.OrderBy(f => f.Order).Select(f => f.Path).ToList(), dlg.FileName, slice);
                    }
                }
            }
        }
    }

    class FileItem
    {
        public int Order { get; set; }
        public string Path { get; set; }
        public VtkModel Model { get; set; }
    }
}

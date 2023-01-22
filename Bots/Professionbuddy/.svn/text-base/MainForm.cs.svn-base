//#define PBDEBUG

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Components;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Component = HighVoltz.BehaviorTree.Component;
using PBAction = HighVoltz.Professionbuddy.ComponentBase.PBAction;

namespace HighVoltz.Professionbuddy
{
	public partial class MainForm : Form
	{
		#region Callbacks

		private readonly FileSystemWatcher _profileWatcher;
		private PropertyBag _profilePropertyBag;
		private CopyPasteOperactions _copyAction = CopyPasteOperactions.Cut;
		private IPBComponent[] _pbComponents;

		private TreeNode _copySource;

		public static MainForm Instance { get; private set; }

		public static bool IsValid
		{
			get { return Instance != null && Instance.Visible && !Instance.Disposing && !Instance.IsDisposed; }
		}

		// used to update GUI controls via other threads

		private void ActionTreeDragDrop(object sender, DragEventArgs e)
		{
			_copyAction = CopyPasteOperactions.Cut;

			if ((e.KeyState & 4) > 0) // shift key
				_copyAction |= CopyPasteOperactions.IgnoreRoot;
			if ((e.KeyState & 8) > 0) // ctrl key
				_copyAction |= CopyPasteOperactions.Copy;

			if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
			{
				Point pt = ((TreeView) sender).PointToClient(new Point(e.X, e.Y));
				TreeNode dest = ((TreeView) sender).GetNodeAt(pt);
				var newNode = (TreeNode) e.Data.GetData("System.Windows.Forms.TreeNode");
				PasteAction(newNode, dest);
			}
			else if (e.Data.GetDataPresent("System.Windows.Forms.DataGridViewRow", false))
			{
				Point pt = ((TreeView) sender).PointToClient(new Point(e.X, e.Y));
				TreeNode dest = ((TreeView) sender).GetNodeAt(pt);
				var row = (DataGridViewRow) e.Data.GetData("System.Windows.Forms.DataGridViewRow");
				if (row.Tag.GetType().GetInterface("IPBComponent") != null)
				{
					var pa = (IPBComponent) Activator.CreateInstance(row.Tag.GetType());
					AddToActionTree(pa, dest);
				}
			}
		}

		private void PasteAction(TreeNode source, TreeNode dest)
		{
			if (dest != source && (!IsChildNode(source, dest) || dest == null))
			{
				var srcComponent = (Component) source.Tag;
				var gc = (Composite)srcComponent.Parent;
				if ((_copyAction & CopyPasteOperactions.Copy) != CopyPasteOperactions.Copy)
					gc.Children.Remove(srcComponent);
				AddToActionTree(source, dest);
				if ((_copyAction & CopyPasteOperactions.Copy) != CopyPasteOperactions.Copy) // ctrl key
					source.Remove();
				_copySource = null; // free any ref..
			}
		}

		private void ActionTreeDragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void ActionTreeItemDrag(object sender, ItemDragEventArgs e)
		{
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void ActionTreeKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyData == Keys.Escape)
			{
				ActionTree.SelectedNode = null;
				e.Handled = true;
			}
			else if (e.KeyData == Keys.Delete)
			{
				if (ActionTree.SelectedNode != null)
					RemoveSelectedNodes();
			}
		}

		private void ActionTreeAfterSelect(object sender, TreeViewEventArgs e)
		{
			if (!IsValid)
				return;
			var comp = (IPBComponent) e.Node.Tag;
			if (comp != null && comp.Properties != null)
			{
				Instance.ActionGrid.SelectedObject = comp.Properties;
			}
		}

		private void OnTradeSkillsLoadedEventHandler(object sender, EventArgs e)
		{
			// must create GUI elements on its parent thread
			if (IsHandleCreated)
				BeginInvoke(new InitDelegate(Initialize));
			else
			{
				HandleCreated += MainFormHandleCreated;
			}
			ProfessionbuddyBot.Instance.OnTradeSkillsLoaded -= OnTradeSkillsLoadedEventHandler;
		}

		private void MainFormHandleCreated(object sender, EventArgs e)
		{
			BeginInvoke(new InitDelegate(Initialize));
			HandleCreated -= MainFormHandleCreated;
		}

		private void MainFormLoad(object sender, EventArgs e)
		{
			if (!ProfessionbuddyBot.Instance.IsTradeSkillsLoaded)
			{
				ProfessionbuddyBot.Instance.OnTradeSkillsLoaded -= OnTradeSkillsLoadedEventHandler;
				ProfessionbuddyBot.Instance.OnTradeSkillsLoaded += OnTradeSkillsLoadedEventHandler;
			}
			else
				Initialize();
			if (DynamicCodeCompiler.CodeIsModified)
				DynamicCodeCompiler.GenorateDynamicCode();
		}

		private void MainFormResizeBegin(object sender, EventArgs e)
		{
			SuspendLayout();
		}

		private void MainFormResizeEnd(object sender, EventArgs e)
		{
			ResumeLayout();
		}

		private void ActionGridPropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			if (ActionGrid.SelectedObject is CastSpellAction && ((CastSpellAction) ActionGrid.SelectedObject).IsRecipe)
			{
				ProfessionbuddyBot.Instance.UpdateMaterials();
				RefreshTradeSkillTabs();
				RefreshActionTree(typeof (CastSpellAction));
			}
			else
			{
				ActionTree.SuspendLayout();
				UdateTreeNode(ActionTree.SelectedNode, null, null, false);
				ActionTree.ResumeLayout();
			}

			if (DynamicCodeCompiler.CodeIsModified)
			{
				new Thread(DynamicCodeCompiler.GenorateDynamicCode) {IsBackground = true}.Start();
			}
		}

		private void RemoveSelectedNodes()
		{
			if (ActionTree.SelectedNode != null)
			{
				var comp = (Component) ActionTree.SelectedNode.Tag;
				((Composite) ((Component) comp).Parent).Children.Remove(comp);
				if (comp is CastSpellAction && ((CastSpellAction) comp).IsRecipe)
				{
					ProfessionbuddyBot.Instance.UpdateMaterials();
					RefreshTradeSkillTabs();
				}
				if (ActionTree.SelectedNode.Parent != null)
					ActionTree.SelectedNode.Parent.Nodes.RemoveAt(ActionTree.SelectedNode.Index);
				else
					ActionTree.Nodes.RemoveAt(ActionTree.SelectedNode.Index);
			}
		}

		private void ActionGridViewMouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				ActionGridView.DoDragDrop(ActionGridView.CurrentRow, DragDropEffects.All);
			}
		}

		private void ActionGridViewSelectionChanged(object sender, EventArgs e)
		{
			if (ActionGridView.SelectedRows.Count > 0)
				HelpTextBox.Text = ((IPBComponent) ActionGridView.SelectedRows[0].Tag).Help;
		}

		private void ToolStripOpenClick(object sender, EventArgs e)
		{
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				ProfileManager.LoadNew(openFileDialog.FileName, true);
				//  if (ProfessionbuddyBot.Instance.ProfileSettings.SettingsDictionary.Count > 0)
				//       AddProfileSettingsTab();
				//  else
				//      RemoveProfileSettingsTab();
			}
		}

		private void ToolStripSaveClick(object sender, EventArgs e)
		{
			saveFileDialog.DefaultExt = "xml";
			saveFileDialog.FilterIndex = 1;
			saveFileDialog.FileName = ProfessionbuddyBot.Instance.CurrentProfile != null &&
									ProfessionbuddyBot.Instance.CurrentProfile.XmlPath != null
				? ProfessionbuddyBot.Instance.CurrentProfile.XmlPath
				: "";
			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				string extension = Path.GetExtension(saveFileDialog.FileName);
				bool zip = extension != null && extension.Equals(
					".package",
					StringComparison.InvariantCultureIgnoreCase);
				// if we are saving to a zip check if CurrentProfile.XmlPath is not blank/null and use it if not. 
				// otherwise use the selected zipname with xml ext.
				if (ProfessionbuddyBot.Instance.CurrentProfile != null)
				{
					string xmlfile = zip
						? (ProfessionbuddyBot.Instance.CurrentProfile != null &&
							string.IsNullOrEmpty(ProfessionbuddyBot.Instance.CurrentProfile.XmlPath)
							? Path.ChangeExtension(saveFileDialog.FileName, ".xml")
							: ProfessionbuddyBot.Instance.CurrentProfile.XmlPath)
						: saveFileDialog.FileName;
					PBLog.Log("Saving profile to {0}", saveFileDialog.FileName);
					if (ProfessionbuddyBot.Instance.CurrentProfile != null)
					{
						ProfessionbuddyBot.Instance.CurrentProfile.SaveXml(xmlfile);
						if (zip)
							PbProfile.CreatePackage(saveFileDialog.FileName, xmlfile);
					}
				}
				ProfessionbuddyBot.Instance.MySettings.LastProfile = saveFileDialog.FileName;
				ProfessionbuddyBot.Instance.MySettings.Save();
				UpdateControls();
			}
		}

		private void ToolStripAddBtnClick(object sender, EventArgs e)
		{
			var compositeList = new List<IPBComponent>();
			// if the tradeskill tab is selected
			if (MainTabControl.SelectedTab == TradeSkillTab)
			{
				var tv = TradeSkillTabControl.SelectedTab as TradeSkillListView;

				if (tv != null)
				{
					DataGridViewSelectedRowCollection rowCollection = tv.TradeDataView.SelectedRows;
					foreach (DataGridViewRow row in rowCollection)
					{
						var cell = (TradeSkillRecipeCell) row.Cells[0].Value;
						Recipe recipe = ProfessionbuddyBot.Instance.TradeSkillList[tv.TradeIndex].KnownRecipes[cell.RecipeID];
						int repeat;
						int.TryParse(toolStripAddNum.Text, out repeat);
						var repeatType = CastSpellAction.RepeatCalculationType.Specific;
						switch (toolStripAddCombo.SelectedIndex)
						{
							case 1:
								repeatType = CastSpellAction.RepeatCalculationType.Craftable;
								break;
							case 2:
								repeatType = CastSpellAction.RepeatCalculationType.Banker;
								break;
						}

						var ca = new CastSpellAction(recipe, repeat, repeatType);
						compositeList.Add(ca);
					}
				}
			}
			else if (MainTabControl.SelectedTab == ActionsTab)
			{
				compositeList.AddRange(
					from DataGridViewRow row in ActionGridView.SelectedRows
					select (IPBComponent) Activator.CreateInstance(row.Tag.GetType()));
			}
			_copyAction = CopyPasteOperactions.Copy;
			foreach (IPBComponent composite in compositeList)
			{
				AddToActionTree(composite, ActionTree.SelectedNode);
			}
			// now update the CanRepeatCount. 
			ProfessionbuddyBot.Instance.UpdateMaterials();
			RefreshTradeSkillTabs();
		}

		private void ToolStripDeleteClick(object sender, EventArgs e)
		{
			RemoveSelectedNodes();
		}

		private void ToolStripHelpClick(object sender, EventArgs e)
		{
			var helpWindow = new Form {Height = 600, Width = 600, Text = "ProfessionBuddy Guide"};
			var helpView = new RichTextBox {Dock = DockStyle.Fill, ReadOnly = true};

			helpView.LoadFile(Path.Combine(ProfessionbuddyBot.BotPath, "Guide.rtf"));
			helpWindow.Controls.Add(helpView);
			helpWindow.Show();
		}

		private void ToolStripCopyClick(object sender, EventArgs e)
		{
			_copySource = ActionTree.SelectedNode;
			if (_copySource != null)
				_copyAction = CopyPasteOperactions.Copy;
		}

		private void ToolStripPasteClick(object sender, EventArgs e)
		{
			if (_copySource != null && ActionTree.SelectedNode != null)
				PasteAction(_copySource, ActionTree.SelectedNode);
		}

		private void ToolStripCutClick(object sender, EventArgs e)
		{
			_copySource = ActionTree.SelectedNode;
			if (_copySource != null)
				_copyAction = CopyPasteOperactions.Cut;
		}

		private void ToolStripSecretButtonClick(object sender, EventArgs e)
		{
			Logging.Write("** ActionSelector **");
			PrintComposite(ProfessionbuddyBot.Instance.Branch, 0);
		}

		private void PrintComposite(Component comp, int cnt)
		{
			var composite = comp as Composite;

			var pbComp = composite as IPBComponent;
			if (pbComp != null)
			{
				string name = pbComp.Title;
				Logging.Write(
					"{0}{1} IsDone:{2}",
					new string(' ', cnt*4),
					name,
					pbComp.IsDone);
			}
			if (composite != null)
			{
				foreach (var child in composite.Children)
				{
					PrintComposite(child, cnt + 1);
				}
			}
		}

		private void ProfileWatcherChanged(object sender, FileSystemEventArgs e)
		{
			RefreshProfileList();
		}

		private void LoadProfileButtonClick(object sender, EventArgs e)
		{
			if (ProfileListView.SelectedItems.Count > 0)
			{
				ProfileManager.LoadNew(Path.Combine(ProfessionbuddyBot.ProfilePath, ProfileListView.SelectedItems[0].Name), true);
			}
		}

		private void ToolStripReloadBtnClick(object sender, EventArgs e)
		{
			ProfessionbuddyBot.Instance.OnTradeSkillsLoaded += ProfessionbuddyBot.Instance.Professionbuddy_OnTradeSkillsLoaded;
			ProfessionbuddyBot.Instance.LoadTradeSkills();
		}

		private void ToolStripBotComboSelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				ProfessionbuddyBot.ChangeSecondaryBot((string) toolStripBotCombo.SelectedItem);
			}
			catch (Exception ex)
			{
				PBLog.Warn(ex.ToString());
			}
		}

		private void ToolStripBotConfigButtonClick(object sender, EventArgs e)
		{
			if (ProfessionbuddyBot.Instance.SecondaryBot != null)
			{
				var gui = ProfessionbuddyBot.Instance.SecondaryBot.ConfigurationForm;
				if (gui != null)
					gui.ShowDialog();
			}
		}

		private void ProfileListViewMouseDoubleClick(object sender, MouseEventArgs e)
		{
			LoadProfileButtonClick(null, null);
		}

		#region Nested type: CopyPasteOperactions

		[Flags]
		private enum CopyPasteOperactions
		{
			Cut = 0,
			IgnoreRoot = 1,
			Copy = 2
		};

		#endregion

		#region Nested type: guiInvokeCB

		private delegate void GuiInvokeCB();

		#endregion

		#region Nested type: refreshActionTreeDelegate

		private delegate void RefreshActionTreeDelegate(IPBComponent pbComponent, Type type);

		#endregion

		#region Initalize/update methods

		private PropertyGrid _settingsPropertyGrid;

		public MainForm()
		{
			try
			{
				Instance = this;
				InitializeComponent();
				// assign the localized strings
				toolStripOpen.Text = ProfessionbuddyBot.Instance.Strings["UI_FileOpen"];
				toolStripSave.Text = ProfessionbuddyBot.Instance.Strings["UI_FileSave"];
				toolStripHelp.Text = ProfessionbuddyBot.Instance.Strings["UI_Help"];
				toolStripCopy.Text = ProfessionbuddyBot.Instance.Strings["UI_Copy"];
				toolStripCut.Text = ProfessionbuddyBot.Instance.Strings["UI_Cut"];
				toolStripPaste.Text = ProfessionbuddyBot.Instance.Strings["UI_Paste"];
				toolStripDelete.Text = ProfessionbuddyBot.Instance.Strings["UI_Delete"];
				toolStripBotConfigButton.Text = ProfessionbuddyBot.Instance.Strings["UI_Settings"];
				ProfileTab.Text = ProfessionbuddyBot.Instance.Strings["UI_Profiles"];
				ActionsColumn.HeaderText = ActionsTab.Text = ProfessionbuddyBot.Instance.Strings["UI_Actions"];
				TradeSkillTab.Text = ProfessionbuddyBot.Instance.Strings["UI_Tradeskill"];
				TabPageProfile.Text = ProfessionbuddyBot.Instance.Strings["UI_Profile"];
				IngredientsColumn.HeaderText = ProfessionbuddyBot.Instance.Strings["UI_Ingredients"];
				NeedColumn.HeaderText = ProfessionbuddyBot.Instance.Strings["UI_Need"];
				BagsColumn.HeaderText = ProfessionbuddyBot.Instance.Strings["UI_Bags"];
				BankColumn.HeaderText = ProfessionbuddyBot.Instance.Strings["UI_Bank"];
				toolStripAddBtn.Text = ProfessionbuddyBot.Instance.Strings["UI_Add"];
				toolStripReloadBtn.Text = ProfessionbuddyBot.Instance.Strings["UI_Reload"];
				LoadProfileButton.Text = ProfessionbuddyBot.Instance.Strings["UI_LoadProfile"];

				saveFileDialog.InitialDirectory = ProfessionbuddyBot.ProfilePath;
				_profileWatcher = new FileSystemWatcher(ProfessionbuddyBot.ProfilePath)
								{
									NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
								};
				_profileWatcher.Changed += ProfileWatcherChanged;
				_profileWatcher.Created += ProfileWatcherChanged;
				_profileWatcher.Deleted += ProfileWatcherChanged;
				_profileWatcher.Renamed += ProfileWatcherChanged;
				_profileWatcher.EnableRaisingEvents = true;

				// used by the dev to display the 'Secret button', a button that dumps some debug info of the Task list.
				if (Environment.UserName == "high")
				{
					toolStripSecretButton.Visible = true;
				}
			}
			catch (Exception ex)
			{
				PBLog.Warn(ex.ToString());
			}
		}

		public PropertyGrid SettingsPropertyGrid
		{
			get { return _settingsPropertyGrid; }
		}


		private void Initialize()
		{
			MainSplitContainer.Panel2MinSize = 390;
			RefreshProfileList();
			InitTradeSkillTab();
			InitActionTree();
			PopulateActionGridView();
			toolStripBotCombo.Items.AddRange(
				BotManager.Instance.Bots.Where(kv => kv.Key != ProfessionbuddyBot.Instance.Name).Select(kv => kv.Key).ToArray());
			UpdateBotCombo();
			if (ProfessionbuddyBot.Instance.HasDataStoreAddon && !toolStripAddCombo.Items.Contains("Banker"))
				toolStripAddCombo.Items.Add("Banker");
			toolStripAddCombo.SelectedIndex = 0;

			string imagePath = Path.Combine(ProfessionbuddyBot.BotPath, "Icons\\");
			toolStripOpen.Image = Image.FromFile(imagePath + "OpenPL.bmp");
			toolStripSave.Image = Image.FromFile(imagePath + "SaveHL.bmp");
			toolStripCopy.Image = Image.FromFile(imagePath + "copy.png");
			toolStripCut.Image = Image.FromFile(imagePath + "cut.png");
			toolStripPaste.Image = Image.FromFile(imagePath + "paste_32x32.png");
			toolStripDelete.Image = Image.FromFile(imagePath + "delete.png");
			toolStripAddBtn.Image = Image.FromFile(imagePath + "112_RightArrowLong_Orange_32x32_72.png");
			toolStripHelp.Image = Image.FromFile(imagePath + "109_AllAnnotations_Help_32x32_72.png");

			if (ProfessionbuddyBot.Instance.ProfileSettings.SettingsDictionary.Count > 0)
				AddProfileSettingsTab();
			else
				RemoveProfileSettingsTab();

			UpdateControls();
		}

		private bool IsChildNode(TreeNode parent, TreeNode child)
		{
			if (child != null && (child.Parent == null))
				return false;
			if (child != null && child.Parent == parent)
				return true;
			return child != null && IsChildNode(parent, child.Parent);
		}


		private void AddToActionTree(object action, TreeNode dest)
		{
			bool ignoreRoot = (_copyAction & CopyPasteOperactions.IgnoreRoot) == CopyPasteOperactions.IgnoreRoot;
			bool cloneActions = (_copyAction & CopyPasteOperactions.Copy) == CopyPasteOperactions.Copy;
			TreeNode newNode;
			var node = action as TreeNode;
			if (node != null)
			{
				if (cloneActions)
				{
					var newComp = (((IPBComponent)node.Tag).DeepCopy());
					newNode = GenerateTreeViewFromBehaviorTree(newComp, null);
				}
				else
				{
					newNode = (TreeNode) node.Clone();
				}
			}
			else
			{
				var pbComponent = action as IPBComponent;
				if (pbComponent != null)
				{
					var composite = pbComponent;
					newNode = new TreeNode(composite.Title) {ForeColor = composite.Color, Tag = composite};
				}
				else
					return;
			}
			ActionTree.SuspendLayout();
			if (dest != null)
			{
				int treeIndex = action is TreeNode && ((TreeNode) action).Parent == dest.Parent &&
								((TreeNode) action).Index <= dest.Index && !cloneActions
					? dest.Index + 1
					: dest.Index;
				Composite gc;
				// If, While and SubRoutines are Decorators...
				if (!ignoreRoot && dest.Tag is Composite)
					gc = (Composite) dest.Tag;
				else
					gc = (Composite) ((Component) dest.Tag).Parent;

				if ((dest.Tag is PBComposite) && !ignoreRoot)
				{
					dest.Nodes.Add(newNode);
					gc.AddChild((Component) newNode.Tag);
					if (!dest.IsExpanded)
						dest.Expand();
				}
				else
				{
					if (dest.Index >= gc.Children.Count)
						gc.AddChild((Component) newNode.Tag);
					else
						gc.InsertChild(dest.Index, (Component) newNode.Tag);
					if (dest.Parent == null)
					{
						if (treeIndex >= ActionTree.Nodes.Count)
							ActionTree.Nodes.Add(newNode);
						else
							ActionTree.Nodes.Insert(treeIndex, newNode);
					}
					else
					{
						if (treeIndex >= dest.Parent.Nodes.Count)
							dest.Parent.Nodes.Add(newNode);
						else
							dest.Parent.Nodes.Insert(treeIndex, newNode);
					}
				}
			}
			else
			{
				ActionTree.Nodes.Add(newNode);
				ProfessionbuddyBot.Instance.Branch.AddChild((Component) newNode.Tag);
			}
			ActionTree.ResumeLayout();
		}


		private void DisableControls()
		{
			ActionTree.Enabled = false;
			toolStripAddBtn.Enabled = false;
			toolStripOpen.Enabled = false;
			toolStripDelete.Enabled = false;
			toolStripCopy.Enabled = false;
			toolStripCut.Enabled = false;
			toolStripPaste.Enabled = false;
			ActionGrid.Enabled = false;
			LoadProfileButton.Enabled = false;
			ProfileListView.Enabled = false;
			toolStripBotCombo.Enabled = false;
		}

		private void EnableControls()
		{
			ActionTree.Enabled = true;
			toolStripAddBtn.Enabled = true;
			toolStripOpen.Enabled = true;
			toolStripDelete.Enabled = true;
			toolStripCopy.Enabled = true;
			toolStripCut.Enabled = true;
			toolStripPaste.Enabled = true;
			ActionGrid.Enabled = true;
			LoadProfileButton.Enabled = true;
			ProfileListView.Enabled = true;
			toolStripBotCombo.Enabled = true;
		}

		public void AddProfileSettingsTab()
		{
			if (!IsValid)
				return;
			if (ProfileTab.InvokeRequired)
				ProfileTab.BeginInvoke(new GuiInvokeCB(AddProfileSettingsTabCallback));
			else
				AddProfileSettingsTabCallback();
		}

		private void AddProfileSettingsTabCallback()
		{
			RightSideTab.SuspendLayout();
			if (RightSideTab.TabPages.ContainsKey("ProfileSettings"))
			{
				RightSideTab.TabPages.RemoveByKey("ProfileSettings");
			}
			RightSideTab.TabPages.Add("ProfileSettings", ProfessionbuddyBot.Instance.Strings["UI_ProfileSettings"]);

			_settingsPropertyGrid = new PropertyGrid {Dock = DockStyle.Fill};
			RightSideTab.TabPages["ProfileSettings"].Controls.Add(_settingsPropertyGrid);

			_profilePropertyBag = new PropertyBag();
			foreach (var kv in ProfessionbuddyBot.Instance.ProfileSettings.SettingsDictionary)
			{
				if (!kv.Value.Hidden)
				{
					_profilePropertyBag[kv.Key] = new MetaProp(
						kv.Key,
						kv.Value.Value.GetType(),
						new DescriptionAttribute(kv.Value.Summary),
						new CategoryAttribute(kv.Value.Category)) {Value = kv.Value.Value};
					_profilePropertyBag[kv.Key].PropertyChanged += ProfileSettingsPropertyChanged;
				}
			}
			_settingsPropertyGrid.SelectedObject = _profilePropertyBag;
			RightSideTab.SelectTab(1);
			RightSideTab.ResumeLayout();
		}

		public void RefreshSettingsPropertyGrid()
		{
			if (!IsValid)
				return;
			if (ProfileTab.InvokeRequired)
				ProfileTab.BeginInvoke(new GuiInvokeCB(RefreshSettingsPropertyGridCallback));
			else
				RefreshSettingsPropertyGridCallback();
		}

		private void RefreshSettingsPropertyGridCallback()
		{
			foreach (var kv in ProfessionbuddyBot.Instance.ProfileSettings.SettingsDictionary)
			{
				MetaProp prop = _profilePropertyBag[kv.Key];
				if (prop != null)
				{
					prop.PropertyChanged -= ProfileSettingsPropertyChanged;
					prop.Value = kv.Value.Value;
					prop.PropertyChanged += ProfileSettingsPropertyChanged;
				}
			}
			_settingsPropertyGrid.Refresh();
		}

		private void ProfileSettingsPropertyChanged(object sender, MetaPropArgs e)
		{
			ProfessionbuddyBot.Instance.ProfileSettings[((MetaProp) sender).Name] = ((MetaProp) sender).Value;
		}


		public void RemoveProfileSettingsTab()
		{
			if (!IsValid)
				return;
			if (ProfileTab.InvokeRequired)
				ProfileTab.BeginInvoke(new GuiInvokeCB(RemoveProfileSettingsTabCallback));
			else
				RemoveProfileSettingsTabCallback();
		}

		private void RemoveProfileSettingsTabCallback()
		{
			if (RightSideTab.TabPages.ContainsKey("ProfileSettings"))
			{
				_profilePropertyBag = null;
				RightSideTab.TabPages.RemoveByKey("ProfileSettings");
			}
		}

		private void UdateTreeNode(TreeNode node, IPBComponent pbComp, Type type, bool recursive)
		{
			var comp = (IPBComponent) node.Tag;
			if ((pbComp == null && type == null) ||
				(pbComp != null && pbComp == node.Tag) ||
				(type != null && type.IsInstanceOfType(node.Tag))
				)
			{
				var pbAction = comp as PBAction;
				node.ForeColor = pbAction != null && pbAction.HasErrors ? Color.Red : comp.Color;
				node.Text = comp.Title;
			}
			if (recursive)
			{
				foreach (TreeNode child in node.Nodes)
				{
					UdateTreeNode(child, pbComp, type, true);
				}
			}
		}

		private TreeNode GenerateTreeViewFromBehaviorTree(IPBComponent component, TreeNode node)
		{
			var newNode = new TreeNode(component.Title) { ForeColor = component.Color, Tag = component };
			if (node != null)
				node.Nodes.Add(newNode);

			var composite = component as PBComposite;
			if (composite != null)
			{
				foreach (var child in composite.Children.OfType<IPBComponent>())
				{
					GenerateTreeViewFromBehaviorTree(child, newNode);
				}
			}
			return newNode;
		}

		private void PopulateActionGridView()
		{
			ActionGridView.Rows.Clear();
	
			if (_pbComponents == null)
			{
				// cache the 'CodeIsModified' valuse because some IPBComponent types will set 'CodeIsModified' indirectly in their constructor
				// and then revert the orignal 'CodeIsModified' back after we are done creating instances 
				var isModified = DynamicCodeCompiler.CodeIsModified;
				try 
				{
					_pbComponents = (from type in Assembly.GetExecutingAssembly().GetTypes()
						where (typeof (IPBComponent)).IsAssignableFrom(type) && !type.IsAbstract
						select ((IPBComponent) Activator.CreateInstance(type))).ToArray();
				}
				finally
				{
					DynamicCodeCompiler.CodeIsModified = isModified;
				}
			}

			foreach (var pbComp in _pbComponents)
			{
				var row = new DataGridViewRow();
				var cell = new DataGridViewTextBoxCell {Value = pbComp.Name};
				row.Cells.Add(cell);
				row.Tag = pbComp;
				row.Height = 16;
				ActionGridView.Rows.Add(row);
				row.DefaultCellStyle.ForeColor = pbComp.Color;
			}
		}

		#region ProfileTab

		private void RefreshProfileList()
		{
			if (!IsValid)
				return;
			if (ProfileTab.InvokeRequired)
				ProfileTab.BeginInvoke(new GuiInvokeCB(RefreshProfileListCallback));
			else
				RefreshProfileListCallback();
		}

		private void RefreshProfileListCallback()
		{
			ProfileListView.SuspendLayout();
			ProfileListView.Clear();
			string[] profiles = Directory.GetFiles(ProfessionbuddyBot.ProfilePath, "*.xml", SearchOption.TopDirectoryOnly).
				Select(Path.GetFileName).Union(
					Directory.GetFiles(
						ProfessionbuddyBot.ProfilePath,
						"*.package",
						SearchOption.TopDirectoryOnly)).
				Select(Path.GetFileName).ToArray();
			// remove all profile names from ListView that are not in the 'profile' array              
			for (int i = 0; i < ProfileListView.Items.Count; i++)
			{
				if (!profiles.Contains(ProfileListView.Items[i].Name))
					ProfileListView.Items.RemoveAt(i);
			} // Add all profiles that are not in ListView             
			foreach (string profile in profiles)
			{
				if (!ProfileListView.Items.ContainsKey(profile))
					ProfileListView.Items.Add(profile, profile, null);
			}
			ProfileListView.ResumeLayout();
		}

		#endregion

		#region TradeSkillTab

		public void InitTradeSkillTab()
		{
			if (!IsValid)
				return;
			if (TradeSkillTabControl.InvokeRequired)
				TradeSkillTabControl.BeginInvoke(new GuiInvokeCB(InitTradeSkillTabCallback));
			else
				InitTradeSkillTabCallback();
		}

		private void InitTradeSkillTabCallback()
		{
			TradeSkillTabControl.SuspendLayout();
			TradeSkillTabControl.TabPages.Clear();
			for (int i = 0; i < ProfessionbuddyBot.Instance.TradeSkillList.Count; i++)
			{
				TradeSkillTabControl.TabPages.Add(new TradeSkillListView(i));
			}
			TradeSkillTabControl.ResumeLayout();

			if (ProfessionbuddyBot.Instance.TradeSkillList.Count > 0)
				TradeSkillTabControl.Visible = true;
		}

		#endregion

		#region UpdateBotCombo

		public void UpdateBotCombo()
		{
			if (!IsValid)
				return;
			if (TradeSkillTabControl.InvokeRequired)
				TradeSkillTabControl.BeginInvoke(new GuiInvokeCB(UpdateBotComboCallback));
			else
				UpdateBotComboCallback();
		}

		private void UpdateBotComboCallback()
		{
			int i = toolStripBotCombo.Items.IndexOf(ProfessionBuddySettings.Instance.LastBotBase);
			toolStripBotCombo.SelectedIndex = i >= 0 ? i : 1;
		}

		#endregion

		#region RefreshActionTree

		public void RefreshActionTree(Type type)
		{
			RefreshActionTree(null, type);
		}

		public void RefreshActionTree(IPBComponent pbComp)
		{
			RefreshActionTree(pbComp, null);
		}

		public void RefreshActionTree()
		{
			RefreshActionTree(null, null);
		}

		public void RefreshActionTree(IPBComponent pbComp, Type type)
		{
			// Don't update ActionTree while PB is running to improve performance.
			if (ProfessionbuddyBot.Instance.IsRunning || !IsValid)
				return;
			if (ActionTree.InvokeRequired)
				ActionTree.BeginInvoke(new RefreshActionTreeDelegate(RefreshActionTreeCallback), pbComp, type);
			else
				RefreshActionTreeCallback(pbComp, type);
		}

		private void RefreshActionTreeCallback(IPBComponent pbComp, Type type)
		{
			ActionTree.SuspendLayout();
			foreach (TreeNode node in ActionTree.Nodes)
			{
				UdateTreeNode(node, pbComp, type, true);
			}
			ActionTree.ResumeLayout();
		}

		#endregion

		#region InitActionTree

		public void InitActionTree()
		{
			if (!IsValid)
				return;
			if (ActionTree.InvokeRequired)
				ActionTree.BeginInvoke(new GuiInvokeCB(InitActionTreeCallback));
			else
				InitActionTreeCallback();
		}

		private void InitActionTreeCallback()
		{
			ActionTree.SuspendLayout();
			int selectedIndex = -1;
			if (ActionTree.SelectedNode != null)
				selectedIndex = ActionTree.Nodes.IndexOf(ActionTree.SelectedNode);
			ActionTree.Nodes.Clear();
			foreach (var comp in ProfessionbuddyBot.Instance.Branch.Children.OfType<IPBComponent>())
			{
				ActionTree.Nodes.Add(GenerateTreeViewFromBehaviorTree(comp, null));
			}
			//ActionTree.ExpandAll();
			if (selectedIndex != -1)
			{
				ActionTree.SelectedNode = selectedIndex < ActionTree.Nodes.Count
					? ActionTree.Nodes[selectedIndex]
					: ActionTree.Nodes[ActionTree.Nodes.Count - 1];
			}
			ActionTree.ResumeLayout();
		}

		#endregion

		#region RefreshTradeSkillTabs

		public void RefreshTradeSkillTabs()
		{
			if (!IsValid)
				return;
			if (TradeSkillTabControl.InvokeRequired)
				TradeSkillTabControl.BeginInvoke(new GuiInvokeCB(RefreshTradeSkillTabsCallback));
			else
				RefreshTradeSkillTabsCallback();
		}

		private void RefreshTradeSkillTabsCallback()
		{
			foreach (TradeSkillListView tv in TradeSkillTabControl.TabPages)
			{
				tv.TradeDataView.SuspendLayout();
				foreach (DataGridViewRow row in tv.TradeDataView.Rows)
				{
					var cell = (TradeSkillRecipeCell) row.Cells[0].Value;
					row.Cells[1].Value = Util.CalculateRecipeRepeat(cell.Recipe);
					row.Cells[2].Value = cell.Recipe.Difficulty;
				}
				tv.TradeDataView.ResumeLayout();
			}
		}

		#endregion

		#region UpdateControls

		// locks/unlocks controls depending on if PB is running on not.
		public void UpdateControls()
		{
			if (!IsValid)
				return;
			if (InvokeRequired)
				BeginInvoke(new GuiInvokeCB(UpdateControlsCallback));
			else
				UpdateControlsCallback();
		}

		private void UpdateControlsCallback()
		{
			if (ProfessionbuddyBot.Instance.IsRunning)
			{
				DisableControls();
				Text = string.Format(
					"Profession Buddy - Running {0}",
					!string.IsNullOrEmpty(ProfessionbuddyBot.Instance.MySettings.LastProfile)
						? "(" + Path.GetFileName(ProfessionbuddyBot.Instance.MySettings.LastProfile) + ")"
						: "");
			}
			else
			{
				EnableControls();
				Text = string.Format(
					"Profession Buddy - Stopped {0}",
					!string.IsNullOrEmpty(ProfessionbuddyBot.Instance.MySettings.LastProfile)
						? "(" + Path.GetFileName(ProfessionbuddyBot.Instance.MySettings.LastProfile) + ")"
						: "");
			}
		}

		#endregion

		private delegate void InitDelegate();

		#endregion
	}

	#endregion
}
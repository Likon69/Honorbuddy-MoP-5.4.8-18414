using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace HighVoltz.Professionbuddy
{
	class MultilineTextEditor : UITypeEditor
	{
		private MultilineTextEditorUI _editorUI;

		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) { return UITypeEditorEditStyle.DropDown; }

		public override bool GetPaintValueSupported(ITypeDescriptorContext context) { return false; }

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (provider != null)
			{
				var editorService = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
				if (editorService == null)
				{
					return value;
				}
				if (_editorUI == null)
				{
					_editorUI = new MultilineTextEditorUI();
				}

				_editorUI.BeginEdit(editorService, value);
				editorService.DropDownControl(_editorUI);
				if (this._editorUI.EndEdit())
				{
					value = _editorUI.Text;
				}
			}
			return value;
		}

		#region Embedded Type: MultilineTextEditorUI

		private class MultilineTextEditorUI : TextBox
		{
			private bool _escPressed;
			private IWindowsFormsEditorService _editorService;
			private Size _minimumSize = Size.Empty;

			internal MultilineTextEditorUI()
			{
				InitializeComponent();
			}

			private void InitializeComponent()
			{
				WordWrap = false;
				BorderStyle = BorderStyle.None;
				Multiline = true;
				ScrollBars = ScrollBars.Both;
				Width = MinimumSize.Width;
				Height = MinimumSize.Height;
				Font = new Font("Consolas", 10);
			}

			internal void BeginEdit(IWindowsFormsEditorService editorService, object value)
			{
				_minimumSize = Size.Empty;
				_editorService = editorService;
				_escPressed = false;
				Text = (string)value;
				Select(Text.Length, 0);
			}

			internal bool EndEdit()
			{
				_editorService = null;
				return !_escPressed;
			}
			protected override bool ProcessDialogKey(Keys keyData)
			{
				if ((keyData & (Keys.Alt | Keys.Shift)) == Keys.None)
				{
					Keys keys = keyData & Keys.KeyCode;
					if ((keys == Keys.Escape) && ((keyData & Keys.Control) == Keys.None))
					{
						_escPressed = true;
					}
				}
				return base.ProcessDialogKey(keyData);
			}

			private Size ContentSize
			{
				get
				{
					var lines = Text.Split('\r', '\n').OrderByDescending(l => l.Length).ToArray();
					return new Size((int)(lines.Select(l => l.Length).FirstOrDefault() * Font.Height / 2.27f), (lines.Length + 1) * Font.Height);
				}
			}

			protected override void OnTextChanged(EventArgs e)
			{
				ResizeToContent();
				base.OnTextChanged(e);
			}

			private void ResizeToContent()
			{
				if (base.Visible)
				{
					Size contentSize = this.ContentSize;
					contentSize.Width += SystemInformation.VerticalScrollBarWidth;
					contentSize.Width = Math.Max(contentSize.Width, this.MinimumSize.Width);
					Rectangle workingArea = Screen.GetWorkingArea(this);
					int num = base.PointToScreen(base.Location).X - workingArea.Left;
					int num2 = Math.Min(contentSize.Width - base.ClientSize.Width, num);
					base.ClientSize = new Size(base.ClientSize.Width + num2, this.MinimumSize.Height);
				}
			}
			public override Size MinimumSize
			{
				get
				{
					if (_minimumSize == Size.Empty)
					{
						Rectangle workingArea = Screen.GetWorkingArea(this);
						_minimumSize = new Size(workingArea.Width / 4, Math.Min((Font.Height * 10), (workingArea.Height / 4)));
					}
					return _minimumSize;
				}
			}
			protected override bool IsInputKey(Keys keyData)
			{
				return (((((keyData & Keys.KeyCode) == Keys.Enter)) && ((keyData & Keys.Alt) == Keys.None)) || ((keyData & Keys.KeyCode) == Keys.Tab) || base.IsInputKey(keyData));
			}

		}

		#endregion

	}
}
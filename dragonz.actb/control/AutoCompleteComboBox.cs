using System;
using System.Windows.Controls;
using System.Windows.Input;
using DragonZ.Actb.Core;

namespace DragonZ.Actb.Control
{
    public class AutoCompleteComboBox : ComboBox
    {
        private AutoCompleteManager _acm;
        private TextBox _textBox;
        private int _oldSelStart;
        private int _oldSelLength;
        private string _oldText;

        public AutoCompleteManager AutoCompleteManager
        {
            get { return _acm; }
        }

        public AutoCompleteComboBox()
        {
            this.IsEditable = true;
            this.IsTextSearchEnabled = false;
            this.GotMouseCapture += AutoCompleteComboBox_GotMouseCapture;
            
            _acm = new AutoCompleteManager();
        }

        private void AutoCompleteComboBox_GotMouseCapture(object sender, MouseEventArgs e)
        {
            _oldSelStart = _textBox.SelectionStart;
            _oldSelLength = _textBox.SelectionLength;
            _oldText = _textBox.Text;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_acm.AutoCompleting)
            {
                return;
            }
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                this.SelectedValue = this.Text;
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            _acm.Disabled = true;
            this.IsTextSearchEnabled = true;
            this.SelectedValue = Text;

            base.OnDropDownOpened(e);

            if (this.SelectedValue == null)
            {
                this.Text = _oldText;
                _textBox.SelectionStart = _oldSelStart;
                _textBox.SelectionLength = _oldSelLength;
            }
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            _acm.Disabled = false;
            this.IsTextSearchEnabled = false;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _textBox = Template.FindName("PART_EditableTextBox", this) as TextBox;
            _acm.AttachTextBox(_textBox);
        }
    }
}
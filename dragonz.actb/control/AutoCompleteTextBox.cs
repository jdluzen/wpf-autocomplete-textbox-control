using System.Windows;
using System.Windows.Controls;
using DragonZ.Actb.Core;

namespace DragonZ.Actb.Control
{
    public class AutoCompleteTextBox : TextBox
    {
        private AutoCompleteManager _acm;

        public AutoCompleteManager AutoCompleteManager
        {
            get { return _acm; }
        }

        public AutoCompleteTextBox()
        {
            _acm = new AutoCompleteManager();
            this.Loaded += AutoCompleteTextBox_Loaded;
        }

        void AutoCompleteTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            _acm.AttachTextBox(this);
            ItemTemplate = itemTemplate;
        }

        protected DataTemplate itemTemplate;
        public DataTemplate ItemTemplate
        {
            get
            {
                return _acm.ListBox.ItemTemplate;
            }
            set
            {
                if (_acm.ListBox != null)
                {
                    _acm.ListBox.ItemTemplate = value;
                    itemTemplate = null;
                    return;
                }
                itemTemplate = value;//delay until loaded
            }
        }
    }
}

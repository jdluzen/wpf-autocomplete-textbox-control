﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DragonZ.Actb.Provider;
using Microsoft.Windows.Themes;

namespace DragonZ.Actb.Core
{
    public class AutoCompleteManager
    {
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_NCRBUTTONDOWN = 0x00A4;

        private const int POPUP_SHADOW_DEPTH = 5;

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                  Internal States                                    |
          |                                                                     |
          +---------------------------------------------------------------------*/

        private double _itemHeight;
        private double _downWidth;
        private double _downHeight;
        private double _downTop;
        private Point _ptDown;

        private bool _popupOnTop = true;
        private bool _manualResized;
        private string _textBeforeChangedByCode;
        private bool _textChangedByCode;

        private Popup _popup;
        private SystemDropShadowChrome _chrome;
        private ScrollBar _scrollBar;
        private ResizeGrip _resizeGrip;
        private ScrollViewer _scrollViewer;
        private Thread _asyncThread;

        private IAutoCompleteDataProvider _dataProvider;
        private bool _disabled;
        private bool _asynchronous;
        private bool _autoAppend;
        private bool _supressAutoAppend;

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                     Public interface                                |
          |                                                                     |
          +---------------------------------------------------------------------*/

        public IAutoCompleteDataProvider DataProvider
        {
            get { return _dataProvider; }
            set { _dataProvider = value; }
        }

        public bool Disabled
        {
            get { return _disabled; }
            set
            {
                _disabled = value;
                if (_disabled && _popup != null)
                {
                    _popup.IsOpen = false;
                }
            }
        }

        public bool AutoCompleting
        {
            get { return _popup.IsOpen; }
        }

        public bool Asynchronous
        {
            get { return _asynchronous; }
            set { _asynchronous = value; }
        }

        public bool AutoAppend
        {
            get { return _autoAppend; }
            set { _autoAppend = value; }
        }

        public TextBox TextBox { get; private set; }

        public int AsyncDelay { get; set; }

        public ListBox ListBox { get; set; }

        public event EventHandler PopupOpened;

        public event EventHandler<SelectionAcceptedEventArgs> SelectionAccepted;

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                       Initialier                                    |
          |                                                                     |
          +---------------------------------------------------------------------*/

        public AutoCompleteManager()
        {
            // default constructor
        }

        public AutoCompleteManager(TextBox textBox)
        {
            AttachTextBox(textBox);
        }

        public void AttachTextBox(TextBox textBox)
        {
            Debug.Assert(TextBox == null);
            if (Application.Current.Resources.FindName("AcTb_ListBoxStyle") == null)
            {
                var myResourceDictionary = new ResourceDictionary();
                var uri = new Uri("/dragonz.actb;component/resource/Resources.xaml", UriKind.Relative);
                myResourceDictionary.Source = uri;
                Application.Current.Resources.MergedDictionaries.Add(myResourceDictionary);
            }

            TextBox = textBox;
            var ownerWindow = Window.GetWindow(TextBox);
            if (ownerWindow != null)
            {
                if (ownerWindow.IsLoaded)
                    Initialize();
                else
                    ownerWindow.Loaded += OwnerWindow_Loaded;
                ownerWindow.LocationChanged += OwnerWindow_LocationChanged;
            }
        }

        private void OwnerWindow_LocationChanged(object sender, EventArgs e)
        {
            _popup.IsOpen = false;
        }

        private void OwnerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Initialize();
        }

        private void Initialize()
        {
            ListBox = new ListBox();
            var tempItem = new ListBoxItem {Content = "TEMP_ITEM_FOR_MEASUREMENT"};
            ListBox.Items.Add(tempItem);
            ListBox.Focusable = false;
            ListBox.Style = (Style) Application.Current.Resources["AcTb_ListBoxStyle"];

            _chrome = new SystemDropShadowChrome();
            _chrome.Margin = new Thickness(0, 0, POPUP_SHADOW_DEPTH, POPUP_SHADOW_DEPTH);
            _chrome.Child = ListBox;

            _popup = new Popup();
            _popup.SnapsToDevicePixels = true;
            _popup.AllowsTransparency = true;
            _popup.MinHeight = SystemParameters.HorizontalScrollBarHeight + POPUP_SHADOW_DEPTH;
            _popup.MinWidth = SystemParameters.VerticalScrollBarWidth + POPUP_SHADOW_DEPTH;
            _popup.VerticalOffset = SystemParameters.PrimaryScreenHeight + 100;
            _popup.Child = _chrome;
            _popup.IsOpen = true;

            _itemHeight = tempItem.ActualHeight;
            ListBox.Items.Clear();

            //
            GetInnerElementReferences();
            UpdateGripVisual();
            SetupEventHandlers();
        }

        private void GetInnerElementReferences()
        {
            _scrollViewer = (ListBox.Template.FindName("Border", ListBox) as Border).Child as ScrollViewer;
            _resizeGrip = _scrollViewer.Template.FindName("ResizeGrip", _scrollViewer) as ResizeGrip;
            _scrollBar = _scrollViewer.Template.FindName("PART_VerticalScrollBar", _scrollViewer) as ScrollBar;
        }

        private void UpdateGripVisual()
        {
            var rectSize = SystemParameters.VerticalScrollBarWidth;
            var triangle = _resizeGrip.Template.FindName("RG_TRIANGLE", _resizeGrip) as Path;
            var pg = triangle.Data as PathGeometry;
            pg = pg.CloneCurrentValue();
            var figure = pg.Figures[0];
            var p = figure.StartPoint;
            p.X = rectSize;
            figure.StartPoint = p;
            var line = figure.Segments[0] as PolyLineSegment;
            p = line.Points[0];
            p.Y = rectSize;
            line.Points[0] = p;
            p = line.Points[1];
            p.X = p.Y = rectSize;
            line.Points[1] = p;
            triangle.Data = pg;
        }

        private void SetupEventHandlers()
        {
            var ownerWindow = Window.GetWindow(TextBox);
            ownerWindow.PreviewMouseDown += OwnerWindow_PreviewMouseDown;
            ownerWindow.Deactivated += OwnerWindow_Deactivated;

            var wih = new WindowInteropHelper(ownerWindow);
            var hwndSource = HwndSource.FromHwnd(wih.Handle);
            var hwndSourceHook = new HwndSourceHook(HookHandler);
            hwndSource.AddHook(hwndSourceHook);
            //hwndSource.RemoveHook();?

            TextBox.TextChanged += TextBox_TextChanged;
            TextBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            TextBox.LostFocus += TextBox_LostFocus;

            ListBox.PreviewMouseLeftButtonDown += ListBox_PreviewMouseLeftButtonDown;
            ListBox.MouseLeftButtonUp += ListBox_MouseLeftButtonUp;
            ListBox.PreviewMouseMove += ListBox_PreviewMouseMove;

            _resizeGrip.PreviewMouseLeftButtonDown += ResizeGrip_PreviewMouseLeftButtonDown;
            _resizeGrip.PreviewMouseMove += ResizeGrip_PreviewMouseMove;
            _resizeGrip.PreviewMouseUp += ResizeGrip_PreviewMouseUp;
        }

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                   TextBox Event Handling                            |
          |                                                                     |
          +---------------------------------------------------------------------*/

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_textChangedByCode || Disabled || _dataProvider == null)
            {
                return;
            }
            var text = TextBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                _popup.IsOpen = false;
                return;
            }
            if (_asynchronous)
            {

                if (_asyncThread != null && _asyncThread.IsAlive)
                {
                    _asyncThread.Interrupt();
                }
                _asyncThread = new Thread(() =>
                {
                    try
                    {
                        if (AsyncDelay > 0)
                            Thread.Sleep(AsyncDelay);
                        var dispatcher = Application.Current.Dispatcher;
                        string currentText = dispatcher.Invoke(new Func<string>(() => TextBox.Text)).ToString();
                        if (text != currentText)
                            return;
                        var items = _dataProvider.GetItems(text);
                        currentText = dispatcher.Invoke(new Func<string>(() => TextBox.Text)).ToString();
                        if (text != currentText)
                            return;
                        dispatcher.Invoke(new Action(() => PopulatePopupList(items)));
                    }
                    catch (ThreadInterruptedException tie)
                    {
                        Debug.Print("Interrupted");
                    }
                }) { IsBackground = true };
                _asyncThread.Start();
            }
            else
            {
                var items = _dataProvider.GetItems(text);
                PopulatePopupList(items);
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _supressAutoAppend = e.Key == Key.Delete || e.Key == Key.Back;
            if (!_popup.IsOpen)
            {
                if (e.Key != Key.Down || ListBox.Items.Count == 0)
                    return;
            }
            if (e.Key == Key.Enter)
            {
                _popup.IsOpen = false;
                TextBox.SelectAll();
                EventHandler<SelectionAcceptedEventArgs> handler = SelectionAccepted;
                if (handler != null)
                    handler(this, new SelectionAcceptedEventArgs { SelectedObject = ListBox.SelectedItem });
            }
            else if (e.Key == Key.Escape)
            {
                if (_popup.IsOpen)
                    UpdateText(_textBeforeChangedByCode, false);
                _popup.IsOpen = false;
                e.Handled = true;
            }
            if (!_popup.IsOpen)
            {
                if (e.Key != Key.Down || ListBox.Items.Count == 0)
                    return;
            }
            var index = ListBox.SelectedIndex;
            if (e.Key == Key.PageUp)
            {
                if (index == -1)
                {
                    index = ListBox.Items.Count - 1;
                }
                else if (index == 0)
                {
                    index = -1;
                }
                else if (index == _scrollBar.Value)
                {
                    index -= (int) _scrollBar.ViewportSize;
                    if (index < 0)
                    {
                        index = 0;
                    }
                }
                else
                {
                    index = (int) _scrollBar.Value;
                }
            }
            else if (e.Key == Key.PageDown)
            {
                if (index == -1)
                {
                    index = 0;
                }
                else if (index == ListBox.Items.Count - 1)
                {
                    index = -1;
                }
                else if (index == _scrollBar.Value + _scrollBar.ViewportSize - 1)
                {
                    index += (int) _scrollBar.ViewportSize - 1;
                    if (index > ListBox.Items.Count - 1)
                    {
                        index = ListBox.Items.Count - 1;
                    }
                }
                else
                {
                    index = (int) (_scrollBar.Value + _scrollBar.ViewportSize - 1);
                }
            }
            else if (e.Key == Key.Up)
            {
                if (index == -1)
                {
                    index = ListBox.Items.Count - 1;
                }
                else
                {
                    --index;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (!_popup.IsOpen)
                    _popup.IsOpen = true;
                ++index;
            }

            if (index != ListBox.SelectedIndex)
            {
                string text;
                if (index < 0 || index > ListBox.Items.Count - 1)
                {
                    text = _textBeforeChangedByCode;
                    ListBox.SelectedIndex = -1;
                }
                else
                {
                    ListBox.SelectedIndex = index;
                    ListBox.ScrollIntoView(ListBox.SelectedItem);
                    text =_dataProvider.GetStringValue(ListBox.SelectedItem);
                }
                UpdateText(text, false);
                e.Handled = true;
            }
        }

        void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _popup.IsOpen = false;
        }

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                     ListBox Event Handling                          |
          |                                                                     |
          +---------------------------------------------------------------------*/

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ListBox);
            var hitTestResult = VisualTreeHelper.HitTest(ListBox, pos);
            if (hitTestResult == null)
            {
                return;
            }
            var d = hitTestResult.VisualHit;
            while (d != null)
            {
                if (d is ListBoxItem)
                {
                    e.Handled = true;
                    break;
                }
                d = VisualTreeHelper.GetParent(d);
            }
        }

        private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.Captured != null)
            {
                return;
            }
            var pos = e.GetPosition(ListBox);
            var hitTestResult = VisualTreeHelper.HitTest(ListBox, pos);
            if (hitTestResult == null)
            {
                return;
            }
            var d = hitTestResult.VisualHit;
            while (d != null)
            {
                if (d is ListBoxItem)
                {
                    var item = (d as ListBoxItem);
                    item.IsSelected = true;
//                    ListBox.ScrollIntoView(item);
                    break;
                }
                d = VisualTreeHelper.GetParent(d);
            }
        }

        private void ListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = null;
            var d = e.OriginalSource as DependencyObject;
            while (d != null)
            {
                if (d is ListBoxItem)
                {
                    item = d as ListBoxItem;
                    break;
                }
                d = VisualTreeHelper.GetParent(d);
            }
            if (item != null)
            {
                _popup.IsOpen = false;
                UpdateText(_dataProvider.GetStringValue(item.Content), true);
                EventHandler<SelectionAcceptedEventArgs> handler = SelectionAccepted;
                if (handler != null)
                    handler(this, new SelectionAcceptedEventArgs { SelectedObject = item.Content });
            }
        }

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                 ResizeGrip Event Handling                           |
          |                                                                     |
          +---------------------------------------------------------------------*/

        private void ResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _downWidth = _chrome.ActualWidth + POPUP_SHADOW_DEPTH;
            _downHeight = _chrome.ActualHeight + POPUP_SHADOW_DEPTH;
            _downTop = _popup.VerticalOffset;

            var p = e.GetPosition(_resizeGrip);
            p = _resizeGrip.PointToScreen(p);
            _ptDown = p;

            _resizeGrip.CaptureMouse();
        }

        private void ResizeGrip_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }
            var ptMove = e.GetPosition(_resizeGrip);
            ptMove = _resizeGrip.PointToScreen(ptMove);
            var dx = ptMove.X - _ptDown.X;
            var dy = ptMove.Y - _ptDown.Y;
            var newWidth = _downWidth + dx;

            if (newWidth != _popup.Width && newWidth > 0)
            {
                _popup.Width = newWidth;
            }
            if (PopupOnTop)
            {
                var bottom = _downTop + _downHeight;
                var newTop = _downTop + dy;
                if (newTop != _popup.VerticalOffset && newTop < bottom - _popup.MinHeight)
                {
                    _popup.VerticalOffset = newTop;
                    _popup.Height = bottom - newTop;
                }
            }
            else
            {
                var newHeight = _downHeight + dy;
                if (newHeight != _popup.Height && newHeight > 0)
                {
                    _popup.Height = newHeight;
                }
            }
        }

        private void ResizeGrip_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _resizeGrip.ReleaseMouseCapture();
            if (_popup.Width != _downWidth || _popup.Height != _downHeight)
            {
                _manualResized = true;
            }
        }

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                    Window Event Handling                            |
          |                                                                     |
          +---------------------------------------------------------------------*/

        private void OwnerWindow_Deactivated(object sender, EventArgs e)
        {
            _popup.IsOpen = false;
        }

        private void OwnerWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source != TextBox)
            {
                _popup.IsOpen = false;
            }
        }

        private IntPtr HookHandler(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;

            switch (msg)
            {
                case WM_NCLBUTTONDOWN: // pass through
                case WM_NCRBUTTONDOWN:
                    _popup.IsOpen = false;
                    break;
            }
            return IntPtr.Zero;
        }

        /*+---------------------------------------------------------------------+
          |                                                                     |
          |                    AcTb State And Behaviors                         |
          |                                                                     |
          +---------------------------------------------------------------------*/

        private void PopulatePopupList(IEnumerable<object> items)
        {
            var text = TextBox.Text;
            
            ListBox.ItemsSource = items;
            if (ListBox.Items.Count == 0)
            {
                _popup.IsOpen = false;
                return;
            }
            var firstSuggestion = ListBox.Items[0] as string;
            if (ListBox.Items.Count == 1 && text.Equals(firstSuggestion, StringComparison.OrdinalIgnoreCase))
            {
                _popup.IsOpen = false;
            }
            else
            {
                ListBox.SelectedIndex = -1;
                _textBeforeChangedByCode = text;
                _scrollViewer.ScrollToHome();
                ShowPopup();

                if (AutoAppend && !_supressAutoAppend &&
                     TextBox.SelectionLength == 0 &&
                     TextBox.SelectionStart == TextBox.Text.Length)
                {
                    _textChangedByCode = true;
                    try
                    {
                        string appendText;
                        var appendProvider = _dataProvider as IAutoAppendDataProvider;
                        if(appendProvider != null)
                        {
                            appendText = appendProvider.GetAppendText(text, firstSuggestion);
                        }
                        else
                        {
                            appendText = firstSuggestion.Substring(TextBox.Text.Length);
                        }
                        if(!string.IsNullOrEmpty(appendText))
                        {
                            TextBox.SelectedText = appendText;
                        }
                    }
                    finally
                    {
                        _textChangedByCode = false;
                    }
                }
            }
        }

        private bool PopupOnTop
        {
            get { return _popupOnTop; }
            set
            {
                if (_popupOnTop == value)
                {
                    return;
                }
                _popupOnTop = value;
                if (_popupOnTop)
                {
                    _resizeGrip.VerticalAlignment = VerticalAlignment.Top;
                    _scrollBar.Margin = new Thickness(0, SystemParameters.HorizontalScrollBarHeight, 0, 0);
                    _resizeGrip.LayoutTransform = new ScaleTransform(1, -1);
                    _resizeGrip.Cursor = Cursors.SizeNESW;
                }
                else
                {
                    _resizeGrip.VerticalAlignment = VerticalAlignment.Bottom;
                    _scrollBar.Margin = new Thickness(0, 0, 0, SystemParameters.HorizontalScrollBarHeight);
                    _resizeGrip.LayoutTransform = Transform.Identity;
                    _resizeGrip.Cursor = Cursors.SizeNWSE;
                }
            }
        }

        private void ShowPopup()
        {
            var popupOnTop = false;

            var p = new Point(0, TextBox.ActualHeight);
            p = TextBox.PointToScreen(p);
            var tbBottom = p.Y;

            p = new Point(0, 0);
            p = TextBox.PointToScreen(p);
            var tbTop = p.Y;

            _popup.HorizontalOffset = p.X;
            var popupTop = tbBottom;

            if (!_manualResized)
            {
                _popup.Width = TextBox.ActualWidth + POPUP_SHADOW_DEPTH;
            }

            double popupHeight;
            if (_manualResized)
            {
                popupHeight = _popup.Height;
            }
            else
            {
                var visibleCount = Math.Min(16, ListBox.Items.Count + 1);
                popupHeight = visibleCount*_itemHeight + POPUP_SHADOW_DEPTH;
            }
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            if (popupTop + popupHeight > screenHeight)
            {
                if (screenHeight - tbBottom > tbTop)
                {
                    popupHeight = SystemParameters.PrimaryScreenHeight - popupTop;
                }
                else
                {
                    popupOnTop = true;
                    popupTop = tbTop - popupHeight + 4;
                    if (popupTop < 0)
                    {
                        popupTop = 0;
                        popupHeight = tbTop + 4;
                    }
                }
            }
            PopupOnTop = popupOnTop;
            _popup.Height = popupHeight;
            _popup.VerticalOffset = popupTop;

            _popup.IsOpen = true;
            EventHandler handler = PopupOpened;
            if (handler != null)
                handler(this, null);
        }

        private void UpdateText(string text, bool selectAll)
        {
            _textChangedByCode = true;
            TextBox.Text = text;
            if (selectAll)
            {
                TextBox.SelectAll();
            }
            else
            {
                TextBox.SelectionStart = text.Length;
            }
            _textChangedByCode = false;
        }
    }
}
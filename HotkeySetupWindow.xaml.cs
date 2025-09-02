using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static EldenRingTool.MainWindow;

namespace EldenRingTool
{
    public partial class HotkeySetupWindow : Window
    {
        public ObservableCollection<HotkeyAssignmentViewModel> HotkeyAssignments { get; private set; }

        public Dictionary<string, Modifiers> ModMap { get; private set; }
        public Dictionary<string, HOTKEY_ACTIONS> ActionMap { get; private set; }
        public Dictionary<string, Key> KeyMap { get; private set; }
        private MainWindow OwnerAsMainWindow => this.Owner as MainWindow;

        public List<HOTKEY_ACTIONS> AllActions { get; private set; }

        public HotkeySetupWindow(
            Dictionary<string, Modifiers> modMap,
            Dictionary<string, Key> keyMap,
            Dictionary<string, HOTKEY_ACTIONS> actionMap,
            string existingHotkeyLines = null)
        {
            InitializeComponent();

            HotkeyAssignments = new ObservableCollection<HotkeyAssignmentViewModel>();
            ModMap = new Dictionary<string, Modifiers>(modMap);
            KeyMap = new Dictionary<string, Key>(keyMap);
            ActionMap = new Dictionary<string, HOTKEY_ACTIONS>(actionMap);

            AllActions = new List<HOTKEY_ACTIONS>(
                (HOTKEY_ACTIONS[])Enum.GetValues(typeof(HOTKEY_ACTIONS))
            );

            DataContext = this;

            // Preload existing hotkeys if provided
            if (!string.IsNullOrWhiteSpace(existingHotkeyLines))
            {
                LoadAssignments(existingHotkeyLines);
            }
        }

        private void LoadAssignments(string linesStr)
        {
            var lines = linesStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                var vm = new HotkeyAssignmentViewModel();
                var spl = line.Split(' ');

                for (int j = 0; j < spl.Length; j++)
                {
                    var s = spl[j];

                    if (ModMap.ContainsKey(s))
                    {
                        if (ModMap[s].HasFlag(Modifiers.CTRL)) vm.HotkeyModifiers |= ModifierKeys.Control;
                        if (ModMap[s].HasFlag(Modifiers.SHIFT)) vm.HotkeyModifiers |= ModifierKeys.Shift;
                        if (ModMap[s].HasFlag(Modifiers.ALT)) vm.HotkeyModifiers |= ModifierKeys.Alt;
                        if (ModMap[s].HasFlag(Modifiers.WIN)) vm.HotkeyModifiers |= ModifierKeys.Windows;
                    }
                    else if (KeyMap.ContainsKey(s))
                    {
                        vm.HotkeyKey = KeyMap[s];
                    }
                    else if (ActionMap.ContainsKey(s))
                    {
                        vm.Action = ActionMap[s];

                        // If this action needs a parameter, grab the next token
                        if (vm.NeedsParam && j + 1 < spl.Length)
                        {
                            vm.Param = spl[j + 1];
                            j++;
                        }
                    }
                }

                if (vm.HotkeyKey != Key.None && vm.Action != 0)
                {
                    HotkeyAssignments.Add(vm);
                }
            }
        }

        private void AddHotkey_Click(object sender, RoutedEventArgs e)
        {
            HotkeyAssignments.Add(new HotkeyAssignmentViewModel());
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var vm = button?.DataContext as HotkeyAssignmentViewModel;
            if (vm != null)
            {
                HotkeyAssignments.Remove(vm);
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var tb = sender as TextBox;
            if (tb == null) return;

            // Ignore modifier-only keys
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys mods = Keyboard.Modifiers;

            var vm = tb.DataContext as HotkeyAssignmentViewModel;
            if (vm != null)
            {
                vm.HotkeyKey = key;
                vm.HotkeyModifiers = mods;
            }

            tb.Text = vm?.HotkeyString ?? "";
        }

        private Modifiers ConvertModifierKeys(ModifierKeys keys)
        {
            Modifiers mods = Modifiers.NO_MOD;

            if ((keys & ModifierKeys.Alt) != 0) mods |= Modifiers.ALT;
            if ((keys & ModifierKeys.Control) != 0) mods |= Modifiers.CTRL;
            if ((keys & ModifierKeys.Shift) != 0) mods |= Modifiers.SHIFT;
            if ((keys & ModifierKeys.Windows) != 0) mods |= Modifiers.WIN;

            return mods;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (OwnerAsMainWindow != null)
            {
                OwnerAsMainWindow.registeredHotkeys.Clear();

                var lines = new List<string>();
                foreach (var assignment in HotkeyAssignments)
                {
                    var parts = new List<string>();

                    if ((assignment.HotkeyModifiers & ModifierKeys.Control) != 0) parts.Add("CTRL");
                    if ((assignment.HotkeyModifiers & ModifierKeys.Alt) != 0) parts.Add("ALT");
                    if ((assignment.HotkeyModifiers & ModifierKeys.Shift) != 0) parts.Add("SHIFT");
                    if ((assignment.HotkeyModifiers & ModifierKeys.Windows) != 0) parts.Add("WIN");

                    if (assignment.HotkeyKey != Key.None)
                        parts.Add(assignment.HotkeyKey.ToString().ToUpper());

                    parts.Add(assignment.Action.ToString());

                    if (assignment.NeedsParam && !string.IsNullOrWhiteSpace(assignment.Param))
                        parts.Add(assignment.Param);

                    lines.Add(string.Join(" ", parts));
                }

                string joined = string.Join(Environment.NewLine, lines);

                File.WriteAllText(MainWindow.hotkeyFile(), joined);

                OwnerAsMainWindow.parseHotkeys(joined);
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class HotkeyAssignmentViewModel : INotifyPropertyChanged
    {
        public HotkeyAction HotkeyAction { get; private set; }

        private string _hotkeyText;
        public string HotkeyText
        {
            get => HotkeyString;
            set
            {
                _hotkeyText = value;
                OnPropertyChanged(nameof(HotkeyText));
            }
        }

        private Key _hotkeyKey = Key.None;
        public Key HotkeyKey
        {
            get => _hotkeyKey;
            set { _hotkeyKey = value; OnPropertyChanged(nameof(HotkeyKey)); OnPropertyChanged(nameof(HotkeyString)); }
        }

        private ModifierKeys _hotkeyModifiers = ModifierKeys.None;
        public ModifierKeys HotkeyModifiers
        {
            get => _hotkeyModifiers;
            set { _hotkeyModifiers = value; OnPropertyChanged(nameof(HotkeyModifiers)); OnPropertyChanged(nameof(HotkeyString)); }
        }

        public string HotkeyString
        {
            get
            {
                if (HotkeyKey == Key.None) return "";
                string text = "";
                if ((HotkeyModifiers & ModifierKeys.Control) != 0) text += "Ctrl+";
                if ((HotkeyModifiers & ModifierKeys.Alt) != 0) text += "Alt+";
                if ((HotkeyModifiers & ModifierKeys.Shift) != 0) text += "Shift+";
                if ((HotkeyModifiers & ModifierKeys.Windows) != 0) text += "Win+";
                text += HotkeyKey.ToString();
                return text;
            }
        }

        public HOTKEY_ACTIONS Action
        {
            get => HotkeyAction.actID;
            set { HotkeyAction.actID = value; OnPropertyChanged(nameof(Action)); OnPropertyChanged(nameof(NeedsParam)); }
        }

        public string Param
        {
            get => HotkeyAction.someParam;
            set { HotkeyAction.someParam = value; OnPropertyChanged(nameof(Param)); }
        }

        public bool NeedsParam => HotkeyAction.needsParam();

        public HotkeyAssignmentViewModel()
        {
            HotkeyAction = new HotkeyAction();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;

namespace IHExt {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        private string gameDir;

        private Dictionary<string, UIElement> uiElements = new Dictionary<string, UIElement>();

        //
        ObservableCollection<string> menuItems = new ObservableCollection<string>();

        //tex combo
        ObservableCollection<string> settingItems = new ObservableCollection<string>();

        //
        Process gameProcess = null;
        //AutomationFocusChangedEventHandler focusHandler = null;

        IPC ipc = null;

        private void Application_Startup(object sender, StartupEventArgs e) {
            //Debugger.Launch();//DEBUGNOW
            Console.WriteLine("IHExt");//tex: To see console output change Project properties -> Application -> Output Type to Console Application

            bool exitWithGame = true;//DEBUG
         

            string gameDir = @"C:\Games\Steam\steamapps\common\MGS_TPP";//DEBUG
            var args = e.Args;
            if (args.Count() > 0) {
                gameDir = args[0];
            }

            string modDir = "mod";
            if (args.Count() > 1) {
                modDir = args[1];
            }

            string gameProcessName = "mgsvtpp";
            if (args.Count() > 2) {
                gameProcessName = args[2];
            }

            bool usePipe = false;
            string serverInName = null;
            if (args.Count() > 3) {
                serverInName = args[3];
                usePipe = true;
            }
            string serverOutName = null;
            if (args.Count() > 4) {
                serverOutName = args[4];
                usePipe = true;
            }

            Console.WriteLine("args:");
            foreach (string arg in args) {
                Console.WriteLine(arg);
            }

            this.gameDir = gameDir;
            string toExtFilePath = $"{gameDir}/{modDir}/ih_toextcmds.txt";
            string toMgsvFilePath = $"{gameDir}/{modDir}/ih_tomgsvcmds.txt";

            bool foundGameDir = true;

            if (!Directory.Exists(gameDir)) {
                Console.WriteLine($"Could not find gameDir: {gameDir}");
                Console.WriteLine("Please launch this program via Infinite Heaven.");
                Console.WriteLine("Or set the -gameDir arg correctly when launching this program.");
                foundGameDir = false;
            }
            if (!usePipe && !File.Exists(toExtFilePath)) {
                Console.WriteLine($"Could not find fromMGSVFile: {toExtFilePath}");
                foundGameDir = false;
            }

            if (!foundGameDir) {
                Application.Current.Shutdown();
                return;
            }

            if (exitWithGame) {
                Process[] gameProcesses = Process.GetProcessesByName(gameProcessName);
                if (gameProcesses.Count() == 0) {
                    Console.WriteLine("WARNING: " + gameProcessName + " not found, exiting.");
                    Application.Current.Shutdown();
                    return;
                }
                if (gameProcesses.Count() > 1) {
                    Console.WriteLine("WARNING: more than one " + gameProcessName + " found, exiting.");
                    Application.Current.Shutdown();
                    return;
                }

                gameProcess = gameProcesses[0];
                gameProcess.EnableRaisingEvents = true;
                gameProcess.Exited += new EventHandler(OnGameProcess_Exited);

                //DEBUGNOW TODO: unstable
                //focusHandler = new AutomationFocusChangedEventHandler(OnFocusChange);
                //Automation.AddAutomationFocusChangedEventHandler(focusHandler);
            }

            DateTime currentDate = DateTime.Now;
            long extSession = currentDate.Ticks;

            ipc = new IPC(extSession, usePipe, serverInName, serverOutName, toExtFilePath, toMgsvFilePath);

            AddCommands();

            

            //tex legacy IH ipc via messages in a txt file
            if (!usePipe) {
                ipc.StartFileWatcher();
            } else {
                ipc.StartPipeThreads();
            }

            ipc.WriteToMgsv();

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();


            AddTestMenuItems(); //DEBUG

            mainWindow.menuItems.ItemsSource = menuItems;
            mainWindow.menuSetting.ItemsSource = settingItems;

            SetFocusToGame();

            ipc.ToMgsvCmd($"extSession|{extSession}");
        }

        public IPC GetIPC() {
            return ipc;
        }

        //IN/SIDE: Function pointers
        private void AddCommands() {
            ipc.AddCommand("Shutdown", ShutdownApp);
            ipc.AddCommand("TakeFocus", TakeFocus);
            ipc.AddCommand("CanvasVisible", CanvasVisible);
            ipc.AddCommand("CreateUiElement", CreateUiElement);
            ipc.AddCommand("RemoveUiElement", RemoveUiElement);
            ipc.AddCommand("SetContent", SetContent);
            ipc.AddCommand("SetText", SetText);
            ipc.AddCommand("SetTextBox", SetTextBox);
            ipc.AddCommand("UiElementVisible", UiElementVisible);
            ipc.AddCommand("ClearTable", ClearTable);
            ipc.AddCommand("AddToTable", AddToTable);
            ipc.AddCommand("UpdateTable", UpdateTable);
            ipc.AddCommand("SelectItem", SelectItem);
            ipc.AddCommand("ClearCombo", ClearCombo);
            ipc.AddCommand("AddToCombo", AddToCombo);
            ipc.AddCommand("SelectCombo", SelectCombo);
            ipc.AddCommand("SelectAllText", SelectAllText);
        }//AddCommands

        void AddTestMenuItems() {
            menuItems.Add("1:Menu line test: 1:SomeSetting");
            menuItems.Add("2:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("3:Menu line test: 1:SomeSetting");
            menuItems.Add("4:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("5:Menu line test: 1:SomeSetting");
            menuItems.Add("6:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("7:Menu line test: 1:SomeSetting");
            menuItems.Add("8:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("9:Menu line test: 1:SomeSetting");
            menuItems.Add("10:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("11:Menu line test: 1:SomeSetting");
            menuItems.Add("12:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("13:Menu line test: 1:SomeSetting");
            menuItems.Add("14:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("1:Menu line test: 1:SomeSetting");
            menuItems.Add("2:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("3:Menu line test: 1:SomeSetting");
            menuItems.Add("4:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("5:Menu line test: 1:SomeSetting");
            menuItems.Add("6:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("7:Menu line test: 1:SomeSetting");
            menuItems.Add("8:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("9:Menu line test: 1:SomeSetting");
            menuItems.Add("10:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("11:Menu line test: 1:SomeSetting");
            menuItems.Add("12:Menu line test longer: 14:Some much longer setting");
            menuItems.Add("13:Menu line test: 1:SomeSetting");
            menuItems.Add("14:Menu line test longer: 14:Some much longer setting");

            settingItems.Add("1. Setting test");
            settingItems.Add("2. Setting test with long text test");
            settingItems.Add("3. Setting test");
            settingItems.Add("4. Setting test");
            settingItems.Add("5. Setting test");
            settingItems.Add("6. Setting test");
            settingItems.Add("7. Setting test");
            settingItems.Add("8. Setting test");
            settingItems.Add("9. Setting test");
            settingItems.Add("1. Setting test");
            settingItems.Add("10. Setting test");
            settingItems.Add("11. Setting test");
            settingItems.Add("12. Setting test");
        }//AddTestMenuItems

        private void OnGameProcess_Exited(object sender, System.EventArgs e) {
            gameProcess = null;
            this.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
            });
        }

        private void OnAppActivated(object sender, EventArgs e) {

        }

        private void OnAppDeactivated(object sender, EventArgs e) {
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                //tex set focus away from menuLine else it will go into search everytime after the first time user selects it 
                mainWindow.menuItems.Focus();
            });
        }

        private void OnFocusChange(object sender, AutomationFocusChangedEventArgs e) {
            this.Dispatcher.Invoke(() => {
                //tex automation like to throw up lots of exceptions for some reason 
                try {
                    var focusedHandle = new IntPtr(AutomationElement.FocusedElement.Current.NativeWindowHandle);
                    var mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
                    if (gameProcess != null) {
                        if (gameProcess.MainWindowHandle == focusedHandle) {
                            //tex game has focus, so make sure ihext is
                            //TODO: could probably get all fancy and match to window position and size, assuming (cant remember) mgstpp has a windowed not fullscreen mode 
                            if (Application.Current.MainWindow.WindowState == System.Windows.WindowState.Maximized) {

                            } else {
                                //Application.Current.MainWindow.WindowState = System.Windows.WindowState.Maximized;
                                ShowWindow(mainWindowHandle, SW_SHOWNOACTIVATE);
                            }
                        } else {
                            if (focusedHandle != mainWindowHandle) {
                                //Application.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
                            }
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"OnFocusChange exception: {ex.Message} {ex.Source}");
                }
            });
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOW = 5;

        private void SetFocusToGame() {
            if (gameProcess != null) {
                var hWnd = gameProcess.MainWindowHandle;
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, SW_SHOW);
            }
        }

        private UIElement GetUiElement(string name) {
            UIElement uiElement;
            if (uiElements.TryGetValue(name, out uiElement)) {
                return uiElement;
            }
            return null;
        }

        private UIElement FindCanvasElement(string name) {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var canvas = mainWindow.MainCanvas;

            var element = canvas.FindName(name);
            UIElement uiElement = (UIElement)element;
            return uiElement;
        }

        //tex from mgsv commands
        //tex all commands take in single param and array of args
        //args[0] = messageId(not really useful for a command)
        //args[1] = command name(ditto)
        //args[2 +] = args as string

        private void ShutdownApp(string[] args) {
            this.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
            });
        }

        private void TakeFocus(string[] args) {
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                if (!mainWindow.IsActive) {
                    mainWindow.Activate();
                } else {
                    /*Process[] gameProcesses = Process.GetProcessesByName(gameProcessName);
                    if (gameProcesses.Count() > 1)
                    {
                    }*/
                }
            });
        }

        //args bool visible
        private void CanvasVisible(string[] args) {
            if (args.Count() < 1 + 1) {
                return;
            }

            bool visible;
            if (!bool.TryParse(args[2], out visible)) {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                var canvas = mainWindow.MainCanvas;
                if (visible == false) {
                    canvas.Visibility = Visibility.Hidden;
                } else {
                    canvas.Visibility = Visibility.Visible;
                }
            });
        }//end CanvasVisible

        //args string name, string xaml
        private void CreateUiElement(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }

            string name = args[2];
            string xamlStr = args[3];
            if (name == null || xamlStr == null) {
                return;
            }

            UIElement existingElement;
            if (uiElements.TryGetValue(name, out existingElement)) {
                Console.WriteLine($"WARNING: could not find element {name}");
                return;
            }


            this.Dispatcher.Invoke(() => {
                try {
                    var uiElement = (FrameworkElement)XamlReader.Parse(xamlStr);
                    uiElements[name] = uiElement;

                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    var canvas = mainWindow.MainCanvas;
                    canvas.RegisterName(uiElement.Name, uiElement);
                    canvas.Children.Add(uiElement);
                    canvas.ApplyTemplate();
                } catch (Exception e) {
                    Console.WriteLine($"ERROR: Exception {e.Message} {e.Source}");
                    return;
                }
            });//end invoke
        }//end CreateUiElement

        //args string name
        private void RemoveUiElement(string[] args) {
            if (args.Count() < 1 + 1) {
                return;
            }

            string name = args[2];
            if (name == null) {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var uiElement = FindCanvasElement(name);
                if (uiElement == null) {
                    Console.WriteLine($"WARNING: could not find element {name}");
                    return;
                }

                uiElements.Remove(name);

                var mainWindow = (MainWindow)Application.Current.MainWindow;
                var canvas = mainWindow.MainCanvas;

                canvas.Children.Remove(uiElement);
            });
        }//end RemoveUiElement

        //args string name, string content
        private void SetContent(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null) {
                return;
            }

            //UIElement uiElement;
            //if (!uiElements.TryGetValue(name, out uiElement)) {
            //    Console.WriteLine($"WARNING: could not find element {name}");
            //    //return;
            //}

            this.Dispatcher.Invoke(() => {
                try {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null) {
                        Console.WriteLine($"WARNING: could not find element {name}");
                        return;
                    }

                    ContentControl contentControl = uiElement as ContentControl;
                    if (contentControl == null) {
                        Console.WriteLine($"WARNING: element {name} wrong type");
                        return;
                    }

                    contentControl.Content = content;
                } catch (Exception e) {
                    Console.WriteLine($"ERROR: Exception {e.Message} {e.Source}");
                    return;
                }
            });
        }//end SetContent

        //for textblock
        //args string name, string content
        private void SetText(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null) {
                return;
            }

            //UIElement uiElement;
            //if (!uiElements.TryGetValue(name, out uiElement)) {
            //    Console.WriteLine($"WARNING: could not find element {name}");
            //    //return;
            //}

            this.Dispatcher.Invoke(() => {
                try {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null) {
                        Console.WriteLine($"WARNING: could not find element {name}");
                        return;
                    }

                    TextBlock contentControl = uiElement as TextBlock;
                    if (contentControl == null) {
                        Console.WriteLine($"WARNING: element {name} wrong type");
                        return;
                    }

                    content.Replace(@"\n", "&#10;");//DEBUGNOW DOCUMENT what am I doing here?

                    contentControl.Text = content;
                } catch (Exception e) {
                    Console.WriteLine($"ERROR: Exception {e.Message} {e.Source}");
                    return;
                }
            });
        }//end SetText

        //for textbox
        //args string name, string content
        private void SetTextBox(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null) {
                return;
            }

            //UIElement uiElement;
            //if (!uiElements.TryGetValue(name, out uiElement)) {
            //    Console.WriteLine($"WARNING: could not find element {name}");
            //    //return;
            //}

            this.Dispatcher.Invoke(() => {
                try {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null) {
                        Console.WriteLine($"WARNING: could not find element {name}");
                        return;
                    }

                    TextBox contentControl = uiElement as TextBox;
                    if (contentControl == null) {
                        Console.WriteLine($"WARNING: element {name} wrong type");
                        return;
                    }

                    content.Replace(@"\n", "&#10;");//DEBUGNOW DOCUMENT what am I doing here?

                    contentControl.Text = content;
                } catch (Exception e) {
                    Console.WriteLine($"ERROR: Exception {e.Message} {e.Source}");
                    return;
                }
            });
        }//end SetText

        //args string name, bool visible
        private void UiElementVisible(string[] args) {
            if (args.Count() < 1 + 1) {
                return;
            }

            string name = args[2];
            if (name == null) {
                return;
            }

            int visible;
            if (!int.TryParse(args[3], out visible)) {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var uiElement = FindCanvasElement(name);
                if (uiElement == null) {
                    return;
                }

                if (visible == 0) {
                    uiElement.Visibility = Visibility.Hidden;
                } else {
                    uiElement.Visibility = Visibility.Visible;
                }
            });
        }//end UiElementVisible

        private void ClearTable(string[] args) {
            if (args.Count() < 1 + 1) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuItems.SelectionChanged -= mainWindow.ListBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                menuItems.Clear();

                mainWindow.menuItems.SelectionChanged += mainWindow.ListBox_OnSelectionChanged;
            });
        }

        private void AddToTable(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }
            string itemString = args[3];
            if (itemString == null) {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuItems.SelectionChanged -= mainWindow.ListBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                menuItems.Add(itemString);

                mainWindow.menuItems.SelectionChanged += mainWindow.ListBox_OnSelectionChanged;
            });
        }

        private void UpdateTable(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }
            int itemIndex;
            if (!int.TryParse(args[3], out itemIndex)) {
                return;
            }
            string itemString = args[4];
            if (itemString == null) {
                return;
            }
            this.Dispatcher.Invoke(() => {
                if (itemIndex >= 0 && itemIndex < menuItems.Count()) {
                    menuItems[itemIndex] = itemString;
                }
            });
        }

        private void SelectItem(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }

            int selectedIndex;
            if (!int.TryParse(args[3], out selectedIndex)) {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                if (selectedIndex >= 0 && selectedIndex < menuItems.Count()) {
                    mainWindow.menuItems.SelectionChanged -= mainWindow.ListBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                    mainWindow.menuItems.SelectedIndex = selectedIndex;
                    mainWindow.menuItems.ScrollIntoView(mainWindow.menuItems.Items[selectedIndex]);

                    mainWindow.menuItems.SelectionChanged += mainWindow.ListBox_OnSelectionChanged;
                }
            });
        }

        private void ClearCombo(string[] args) {
            if (args.Count() < 1 + 1) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuSetting.SelectionChanged -= mainWindow.ComboBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                settingItems.Clear();

                mainWindow.menuSetting.SelectionChanged += mainWindow.ComboBox_OnSelectionChanged;
            });
        }

        private void AddToCombo(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }
            string itemString = args[3];
            if (itemString == null) {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuSetting.SelectionChanged -= mainWindow.ComboBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                settingItems.Add(itemString);

                mainWindow.menuSetting.SelectionChanged += mainWindow.ComboBox_OnSelectionChanged;
            });
        }

        private void SelectCombo(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null) {
                return;
            }

            int selectedIndex;
            if (!int.TryParse(args[3], out selectedIndex)) {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                if (selectedIndex >= 0 && selectedIndex < menuItems.Count()) {
                    mainWindow.menuSetting.SelectionChanged -= mainWindow.ComboBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                    mainWindow.menuSetting.SelectedIndex = selectedIndex;

                    mainWindow.menuSetting.SelectionChanged += mainWindow.ComboBox_OnSelectionChanged;
                }
            });
        }

        //for textbox
        //args string name
        private void SelectAllText(string[] args) {
            if (args.Count() < 1 + 1) {
                return;
            }

            string name = args[2];
            if (name == null) {
                return;
            }

            this.Dispatcher.Invoke(() => {
                try {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null) {
                        Console.WriteLine($"WARNING: could not find element {name}");
                        return;
                    }

                    TextBox contentControl = uiElement as TextBox;
                    if (contentControl == null) {
                        Console.WriteLine($"WARNING: element {name} wrong type");
                        return;
                    }

                    contentControl.SelectAll();
                } catch (Exception e) {
                    Console.WriteLine($"ERROR: Exception {e.Message} {e.Source}");
                    return;
                }
            });
        }//end SetText

    }//app class
}//namespace

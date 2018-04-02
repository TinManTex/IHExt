using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace IHExt
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string gameDir;
        private string toExtFilePath;
        private string toMgsvFilePath;

        private SortedDictionary<int, string> extToMgsvCmds = new SortedDictionary<int, string>();//tex from ext to mgsv

        private Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();

        private int extToMgsvCurrent = 0;//tex current/max, last command to be written out
        private int extToMgsvComplete = 0;//tex min/confirmed executed by mgsv, only commands above this should be written out
        private int mgsvToExtComplete = 0;//tex min/confimed executed by ext 

        private long extSession = 0;
        private long mgsvSession = 0;

        private Dictionary<string, UIElement> uiElements = new Dictionary<string, UIElement>();

        //
        ObservableCollection<string> menuItems = new ObservableCollection<string>();

        //tex combo
        ObservableCollection<string> settingItems = new ObservableCollection<string>();

        //
        Process gameProcess = null;
        AutomationFocusChangedEventHandler focusHandler = null;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool exitWithGame = true;//DEBUG


            string gameDir = @"C:\GamesSD\MGS_TPP";//DEBUG
            var args = e.Args;
            if (args.Count() > 0)
            {
                gameDir = args[0];
            }

            string modDir = "mod";
            if (args.Count() > 1)
            {
                modDir = args[1];
            }


            string gameProcessName = "mgsvtpp";
            if (args.Count() > 2)
            {
                gameProcessName = args[2];
            }

            this.gameDir = gameDir;
            this.toExtFilePath = $"{gameDir}/{modDir}/ih_toextcmds.txt";
            this.toMgsvFilePath = $"{gameDir}/{modDir}/ih_tomgsvcmds.txt";

            bool foundGameDir = true;

            if (!Directory.Exists(gameDir))
            {
                Console.WriteLine("Could not find gameDir: {0}", gameDir);
                Console.WriteLine("Please launch this program via Infinite Heaven.");
                Console.WriteLine("Or set the -gameDir arg correctly when launching this program.");
                foundGameDir = false;
            }
            if (!File.Exists(toExtFilePath))
            {
                Console.WriteLine("Could not find fromMGSVFile: {0}", toExtFilePath);
                foundGameDir = false;
            }

            if (!foundGameDir)
            {
                Application.Current.Shutdown();
                return;
            }

            if (exitWithGame)
            {
                Process[] gameProcesses = Process.GetProcessesByName(gameProcessName);
                if (gameProcesses.Count() == 0)
                {
                    //DEBUGNOW message gameProcessName not started
                    Application.Current.Shutdown();
                    return;
                }
                if (gameProcesses.Count() > 1)
                {
                    //DEBUGNOW WARN more than one gameProcessName process found
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

            commands.Add("Shutdown", ShutdownApp);
            commands.Add("TakeFocus", TakeFocus);
            commands.Add("CanvasVisible", CanvasVisible);
            commands.Add("CreateUiElement", CreateUiElement);
            commands.Add("RemoveUiElement", RemoveUiElement);
            commands.Add("SetContent", SetContent);
            commands.Add("SetText", SetText);
            commands.Add("SetTextBox", SetTextBox);
            commands.Add("UiElementVisible", UiElementVisible);
            commands.Add("ClearTable", ClearTable);
            commands.Add("AddToTable", AddToTable);
            commands.Add("UpdateTable", UpdateTable);
            commands.Add("SelectItem", SelectItem);
            commands.Add("ClearCombo", ClearCombo);
            commands.Add("AddToCombo", AddToCombo);
            commands.Add("SelectCombo", SelectCombo);
            commands.Add("SelectAllText", SelectAllText);

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(toExtFilePath);
            watcher.Filter = Path.GetFileName(toExtFilePath);
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;//tex < allow subscribed event -v- to actually fire
            watcher.Changed += new FileSystemEventHandler(OnToExtChanged);

            DateTime currentDate = DateTime.Now;
            extSession = currentDate.Ticks;

            WriteToMgsvFile();

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();


            AddTestMenuItems(); //DEBUG

            mainWindow.menuItems.ItemsSource = menuItems;
            mainWindow.menuSetting.ItemsSource = settingItems;

            SetFocusToGame();

            ToMgsvCmd("ready");
        }

        void AddTestMenuItems()
        {
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
        }

        //tex on ih_toextcmds.txt changed
        private void OnToExtChanged(object source, FileSystemEventArgs e)
        {
            //Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);

            using (FileStream fs = WaitForFile(this.toExtFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    string line;
                    int count = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (String.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        //Console.WriteLine(line);//DEBUG
                        char[] delimiters = { '|' };
                        string[] args = line.Split(delimiters);
                        int messageId;
                        if (Int32.TryParse(args[0], out messageId))
                        {
                            if (count == 0)
                            { //tex messageid of first line is mgsv session id 
                                if (messageId != mgsvSession)
                                {
                                    Console.WriteLine("MGSV session changed");
                                    mgsvSession = messageId;
                                    //DEBUGNOW mgsvToExtComplete = 0;//tex reset
                                    ToMgsvCmd("sessionchange");//tex a bit of nothing to get the extToMgsvComplete to update from the message, mgsv does likewise DEBUGNOW
                                }

                                int arg = 0;
                                if (Int32.TryParse(args[2], out arg))
                                {
                                    extToMgsvComplete = arg;
                                }
                            } else
                            {
                                if (messageId > mgsvToExtComplete)
                                {//tex IHExt hasn't done this command yet yet
                                    string command = args[1];

                                    if (command == null)
                                    {
                                        //TODO: warn
                                    } else
                                    {
                                        if (!commands.ContainsKey(command))
                                        {
                                            //TODO: warn unrecognised command
                                        } else
                                        {
                                            commands[command](args);
                                        }
                                    }

                                    mgsvToExtComplete = messageId;
                                }
                            }
                        }
                        count++;
                    }//end while readline
                }// end streamreader
            }//end waitforfile

            WriteToMgsvFile();
        }//end OnToExtChanged

        private void OnGameProcess_Exited(object sender, System.EventArgs e)
        {
            gameProcess = null;
            this.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
            });
        }

        private void OnAppActivated(object sender, EventArgs e)
        {

        }

        private void OnAppDeactivated(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                //tex set focus away from menuLine else it will go into search everytime after the first time user selects it 
                mainWindow.menuItems.Focus();
            });
        }

        private void OnFocusChange(object sender, AutomationFocusChangedEventArgs e)
        {
            this.Dispatcher.Invoke(() => {
                //tex automation like to throw up lots of exceptions for some reason 
                try
                {
                    var focusedHandle = new IntPtr(AutomationElement.FocusedElement.Current.NativeWindowHandle);
                    var mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
                    if (gameProcess != null)
                    {
                        if (gameProcess.MainWindowHandle == focusedHandle)
                        {
                            //tex game has focus, so make sure ihext is
                            //TODO: could probably get all fancy and match to window position and size, assuming (cant remember) mgstpp has a windowed not fullscreen mode 
                            if (Application.Current.MainWindow.WindowState == System.Windows.WindowState.Maximized)
                            {

                            } else
                            {
                                //Application.Current.MainWindow.WindowState = System.Windows.WindowState.Maximized;
                                ShowWindow(mainWindowHandle, SW_SHOWNOACTIVATE);
                            }
                        } else
                        {
                            if (focusedHandle != mainWindowHandle)
                            {
                                //Application.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
                            }
                        }
                    }
                } catch (Exception ex)
                {

                }
            });
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOW = 5;

        private void SetFocusToGame()
        {
            if (gameProcess != null)
            {
                var hWnd = gameProcess.MainWindowHandle;
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, SW_SHOW);
            }
        }

        //to mgsv commands
        public void ToMgsvCmd(string cmd)
        {
            string message = extToMgsvCurrent.ToString() + "|" + cmd;
            extToMgsvCmds.Add(extToMgsvCurrent, message);
            extToMgsvCurrent++;
            WriteToMgsvFile();
        }

        private string GetSessionString()
        {
            return string.Format("{0}|cmdToExtCompletedIndex|{1}", extSession, mgsvToExtComplete);
        }

        private void WriteToMgsvFile()
        {
            //tex lua/mgsv io "r" opens in exclusive/lock, so have to wait
            using (FileStream fs = WaitForFile(this.toMgsvFilePath, FileMode.Truncate, FileAccess.Write, FileShare.None))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    string sessionString = GetSessionString();
                    sw.WriteLine(sessionString); //tex always on first line, lets mgsv know what commands have been completed so it can cull them from the mgsvToExt file to stop if from infinitely growing
                    //tex really from extToMgsvComplete+1, but the check for that uglifys code too much, and I can live with last complete command staying in the txt files
                    for (int i = extToMgsvComplete; i < extToMgsvCurrent; i++)
                    {
                        string line = extToMgsvCmds[i];
                        sw.WriteLine(line);
                    }
                    sw.Flush();
                }
            }
        }

        FileStream WaitForFile(string fullPath, FileMode mode, FileAccess access, FileShare share)
        {
            for (int numTries = 0; numTries < 10; numTries++)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(fullPath, mode, access, share);
                    return fs;
                } catch (IOException)
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                    }
                    Thread.Sleep(50);
                }
            }

            return null;
        }

        private UIElement GetUiElement(string name)
        {
            UIElement uiElement;
            if (uiElements.TryGetValue(name, out uiElement))
            {
                return uiElement;
            }
            return null;
        }

        private UIElement FindCanvasElement(string name)
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var canvas = mainWindow.MainCanvas;

            var element = canvas.FindName(name);
            UIElement uiElement = (UIElement)element;
            return uiElement;
        }

        //from mgsv commands
        private void ShutdownApp(string[] args)
        {
            this.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
            });
        }

        private void TakeFocus(string[] args)
        {
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                if (!mainWindow.IsActive)
                {
                    mainWindow.Activate();
                } else
                {
                    /*Process[] gameProcesses = Process.GetProcessesByName(gameProcessName);
                    if (gameProcesses.Count() > 1)
                    {
                    }*/
                }
            });
        }

        //args bool visible
        private void CanvasVisible(string[] args)
        {
            if (args.Count() < 1 + 1)
            {
                return;
            }

            bool visible;
            if (!bool.TryParse(args[2], out visible))
            {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                var canvas = mainWindow.MainCanvas;
                if (visible == false)
                {
                    canvas.Visibility = Visibility.Hidden;
                } else
                {
                    canvas.Visibility = Visibility.Visible;
                }
            });
        }//end CanvasVisible

        //args string name, string xaml
        private void CreateUiElement(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }

            string name = args[2];
            string xamlStr = args[3];
            if (name == null || xamlStr == null)
            {
                return;
            }

            UIElement existingElement;
            if (uiElements.TryGetValue(name, out existingElement))
            {
                //DEBUGNOW TODO WARN
                return;
            }


            this.Dispatcher.Invoke(() => {
                try
                {
                    var uiElement = (FrameworkElement)XamlReader.Parse(xamlStr);
                    uiElements[name] = uiElement;

                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    var canvas = mainWindow.MainCanvas;
                    canvas.RegisterName(uiElement.Name, uiElement);
                    canvas.Children.Add(uiElement);
                    canvas.ApplyTemplate();
                } catch (Exception e)
                {
                    //DEBUGNOW TODO WARN
                    return;
                }
            });//end invoke
        }//end CreateUiElement

        //args string name
        private void RemoveUiElement(string[] args)
        {
            if (args.Count() < 1 + 1)
            {
                return;
            }

            string name = args[2];
            if (name == null)
            {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var uiElement = FindCanvasElement(name);
                if (uiElement == null)
                {
                    //DEBUGNOW WARN
                    return;
                }

                uiElements.Remove(name);

                var mainWindow = (MainWindow)Application.Current.MainWindow;
                var canvas = mainWindow.MainCanvas;

                canvas.Children.Remove(uiElement);
            });
        }//end RemoveUiElement

        //args string name, string content
        private void SetContent(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null)
            {
                return;
            }

            //UIElement uiElement;
            //if (!uiElements.TryGetValue(name, out uiElement)) {
            //    //DEBUGNOW TODO WARN
            //    //return;
            //}

            this.Dispatcher.Invoke(() => {
                try
                {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    ContentControl contentControl = uiElement as ContentControl;
                    if (contentControl == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    contentControl.Content = content;
                } catch (Exception e)
                {
                    //DEBUGNOW TODO WARN
                    return;
                }
            });
        }//end SetContent

        //for textblock
        //args string name, string content
        private void SetText(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null)
            {
                return;
            }

            //UIElement uiElement;
            //if (!uiElements.TryGetValue(name, out uiElement)) {
            //    //DEBUGNOW TODO WARN
            //    //return;
            //}

            this.Dispatcher.Invoke(() => {
                try
                {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    TextBlock contentControl = uiElement as TextBlock;
                    if (contentControl == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    content.Replace(@"\n", "&#10;");//DEBUGNOW

                    contentControl.Text = content;
                } catch (Exception e)
                {
                    //DEBUGNOW TODO WARN
                    return;
                }
            });
        }//end SetText

        //for textbox
        //args string name, string content
        private void SetTextBox(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null)
            {
                return;
            }

            //UIElement uiElement;
            //if (!uiElements.TryGetValue(name, out uiElement)) {
            //    //DEBUGNOW TODO WARN
            //    //return;
            //}

            this.Dispatcher.Invoke(() => {
                try
                {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    TextBox contentControl = uiElement as TextBox;
                    if (contentControl == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    content.Replace(@"\n", "&#10;");//DEBUGNOW

                    contentControl.Text = content;
                } catch (Exception e)
                {
                    //DEBUGNOW TODO WARN
                    return;
                }
            });
        }//end SetText

        //args string name, bool visible
        private void UiElementVisible(string[] args)
        {
            if (args.Count() < 1 + 1)
            {
                return;
            }

            string name = args[2];
            if (name == null)
            {
                return;
            }

            int visible;
            if (!int.TryParse(args[3], out visible))
            {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var uiElement = FindCanvasElement(name);
                if (uiElement == null)
                {
                    return;
                }

                if (visible == 0)
                {
                    uiElement.Visibility = Visibility.Hidden;
                } else
                {
                    uiElement.Visibility = Visibility.Visible;
                }
            });
        }//end UiElementVisible

        private void ClearTable(string[] args)
        {
            if (args.Count() < 1 + 1)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuItems.SelectionChanged -= mainWindow.ListBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                menuItems.Clear();

                mainWindow.menuItems.SelectionChanged += mainWindow.ListBox_OnSelectionChanged;
            });
        }

        private void AddToTable(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }
            string itemString = args[3];
            if (itemString == null)
            {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuItems.SelectionChanged -= mainWindow.ListBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                menuItems.Add(itemString);

                mainWindow.menuItems.SelectionChanged += mainWindow.ListBox_OnSelectionChanged;
            });
        }

        private void UpdateTable(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }
            int itemIndex;
            if (!int.TryParse(args[3], out itemIndex))
            {
                return;
            }
            string itemString = args[4];
            if (itemString == null)
            {
                return;
            }
            this.Dispatcher.Invoke(() => {
                if (itemIndex >= 0 && itemIndex < menuItems.Count())
                {
                    menuItems[itemIndex] = itemString;
                }
            });
        }

        private void SelectItem(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }

            int selectedIndex;
            if (!int.TryParse(args[3], out selectedIndex))
            {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                if (selectedIndex >= 0 && selectedIndex < menuItems.Count())
                {
                    mainWindow.menuItems.SelectionChanged -= mainWindow.ListBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                    mainWindow.menuItems.SelectedIndex = selectedIndex;
                    mainWindow.menuItems.ScrollIntoView(mainWindow.menuItems.Items[selectedIndex]);

                    mainWindow.menuItems.SelectionChanged += mainWindow.ListBox_OnSelectionChanged;
                }
            });
        }

        private void ClearCombo(string[] args)
        {
            if (args.Count() < 1 + 1)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuSetting.SelectionChanged -= mainWindow.ComboBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                settingItems.Clear();

                mainWindow.menuSetting.SelectionChanged += mainWindow.ComboBox_OnSelectionChanged;
            });
        }

        private void AddToCombo(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }
            string itemString = args[3];
            if (itemString == null)
            {
                return;
            }
            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                mainWindow.menuSetting.SelectionChanged -= mainWindow.ComboBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                settingItems.Add(itemString);

                mainWindow.menuSetting.SelectionChanged += mainWindow.ComboBox_OnSelectionChanged;
            });
        }

        private void SelectCombo(string[] args)
        {
            if (args.Count() < 1 + 2)
            {
                return;
            }
            //TODO dict of tables
            string name = args[2];
            if (name == null)
            {
                return;
            }

            int selectedIndex;
            if (!int.TryParse(args[3], out selectedIndex))
            {
                return;
            }

            this.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                if (selectedIndex >= 0 && selectedIndex < menuItems.Count())
                {
                    mainWindow.menuSetting.SelectionChanged -= mainWindow.ComboBox_OnSelectionChanged; //KLUDGE SelectionChanged event fires on all changes, I just want on user changes

                    mainWindow.menuSetting.SelectedIndex = selectedIndex;

                    mainWindow.menuSetting.SelectionChanged += mainWindow.ComboBox_OnSelectionChanged;
                }
            });
        }

        //for textbox
        //args string name
        private void SelectAllText(string[] args)
        {
            if (args.Count() < 1 + 1)
            {
                return;
            }

            string name = args[2];
            if (name == null )
            {
                return;
            }

            this.Dispatcher.Invoke(() => {
                try
                {
                    UIElement uiElement = FindCanvasElement(name);
                    if (uiElement == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    TextBox contentControl = uiElement as TextBox;
                    if (contentControl == null)
                    {
                        //DEBUGNOW TODO WARN
                        return;
                    }

                    contentControl.Select(0, contentControl.Text.Length);
                } catch (Exception e)
                {
                    //DEBUGNOW TODO WARN
                    return;
                }
            });
        }//end SetText

    }//app class
}//namespace

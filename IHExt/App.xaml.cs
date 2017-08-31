using System;
using System.Collections.Generic;
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
using System.Windows.Markup;

namespace IHExt {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
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
        Process gameProcess = null;
        AutomationFocusChangedEventHandler focusHandler = null;

        bool hadArgs = false;//DEBUGNOW

        private void Application_Startup(object sender, StartupEventArgs e) {
            bool exitWithMgsv = true;//DEBUG


            string gameDir = @"C:\GamesSD\MGS_TPP";
            var args = e.Args;
            if (args.Count() > 0) {
                gameDir = args[0];
                hadArgs = true;//DEBUG
            }

            this.gameDir = gameDir;
            this.toExtFilePath = gameDir + @"\mod\ih_toextcmds.txt";
            this.toMgsvFilePath = gameDir + @"\mod\ih_tomgsvcmds.txt";

            bool foundGameDir = true;

            if (!Directory.Exists(gameDir)) {
                Console.WriteLine("Could not find gameDir: {0}", gameDir);
                Console.WriteLine("Please launch this program via Infinite Heaven.");
                Console.WriteLine("Or set the -gameDir arg correctly when launching this program.");
                foundGameDir = false;
            }
            if (!File.Exists(toExtFilePath)) {
                Console.WriteLine("Could not find fromMGSVFile: {0}", toExtFilePath);
                foundGameDir = false;
            }

            if (!foundGameDir) {
                Application.Current.Shutdown();
                return;
            }

            //DEBUGNOW
            if (exitWithMgsv) {
                string gameProcessName = "mgsvtpp";
                Process[] gameProcesses = Process.GetProcessesByName(gameProcessName);
                if (gameProcesses.Count() == 0) {
                    //DEBUGNOW message gameProcessName not started
                    Application.Current.Shutdown();
                    return;
                }
                if (gameProcesses.Count() > 1) {
                    //DEBUGNOW WARN more than one gameProcessName process found
                    Application.Current.Shutdown();
                    return;
                }

                gameProcess = gameProcesses[0];
                gameProcess.EnableRaisingEvents = true;
                gameProcess.Exited += new EventHandler(OnGameProcess_Exited);

                focusHandler = new AutomationFocusChangedEventHandler(OnFocusChange);
                Automation.AddAutomationFocusChangedEventHandler(focusHandler);
            }

            commands.Add("CanvasVisible", CanvasVisible);
            commands.Add("CreateUiElement", CreateUiElement);
            commands.Add("RemoveUiElement", RemoveUiElement);
            commands.Add("SetContent", SetContent);
            commands.Add("UiElementVisible", UiElementVisible);

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

            SetFocusToGame();
        }

        //tex on ih_toextcmds.txt changed
        private void OnToExtChanged(object source, FileSystemEventArgs e) {
            //Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);


            using (FileStream fs = WaitForFile(this.toExtFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                using (StreamReader sr = new StreamReader(fs)) {
                    string line;
                    int count = 0;
                    while ((line = sr.ReadLine()) != null) {
                        if (String.IsNullOrEmpty(line)) {
                            continue;
                        }

                        //Console.WriteLine(line);//DEBUG
                        char[] delimiters = { '|' };
                        string[] args = line.Split(delimiters);
                        int messageId;
                        if (Int32.TryParse(args[0], out messageId)) {
                            if (count == 0) { //tex messageid of first entry is mgsv session id 
                                if (messageId != mgsvSession) {
                                    Console.WriteLine("MGSV session changed");
                                    mgsvSession = messageId;
                                    //DEBUGNOW mgsvToExtComplete = 0;//tex reset
                                    ToMgsvCmd("sessionChange");//tex a bit of nothing to get the extToMgsvComplete to update from the message, mgsv does likewise DEBUGNOW
                                }

                                int arg = 0;
                                if (Int32.TryParse(args[2], out arg)) {
                                    extToMgsvComplete = arg;
                                }
                            } else {
                                if (messageId > mgsvToExtComplete) {//tex havent done this command yet yet
                                    string command = args[1];
                                    //Do command
                                    if (command == null) {
                                        //TODO: warn
                                    } else {
                                        if (!commands.ContainsKey(command)) {
                                            //TODO: warn unrecognised command
                                        } else {
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

        private void OnGameProcess_Exited(object sender, System.EventArgs e) {
            gameProcess = null;
            this.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
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
                } catch(Exception ex) {

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

        //to mgsv commands
        public void ToMgsvCmd(string cmd) {
            string message = extToMgsvCurrent.ToString() + "|" + cmd;
            extToMgsvCmds.Add(extToMgsvCurrent, message);
            extToMgsvCurrent++;
            WriteToMgsvFile();
        }

        private string GetSessionString() {
            return string.Format("{0}|cmdToExtCompletedIndex|{1}", extSession, mgsvToExtComplete);
        }

        private void WriteToMgsvFile() {
            //tex lua/mgsv io "r" opens in exclusive/lock, so have to wait
            using (FileStream fs = WaitForFile(this.toMgsvFilePath, FileMode.Truncate, FileAccess.Write, FileShare.None)) {
                using (StreamWriter sw = new StreamWriter(fs)) {
                    string sessionString = GetSessionString();
                    sw.WriteLine(sessionString); //tex always on first line, lets mgsv know what commands have been completed so it can cull them from the mgsvToExt file to stop if from infinitely growing
                    //tex really from extToMgsvComplete+1, but the check for that uglifys code too much, and I can live with last complete command staying in the txt files
                    for (int i = extToMgsvComplete; i < extToMgsvCurrent; i++) {
                        string line = extToMgsvCmds[i];
                        sw.WriteLine(line);
                    }
                    sw.Flush();
                }
            }
        }

        FileStream WaitForFile(string fullPath, FileMode mode, FileAccess access, FileShare share) {
            for (int numTries = 0; numTries < 10; numTries++) {
                FileStream fs = null;
                try {
                    fs = new FileStream(fullPath, mode, access, share);
                    return fs;
                } catch (IOException) {
                    if (fs != null) {
                        fs.Dispose();
                    }
                    Thread.Sleep(50);
                }
            }

            return null;
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

            var element = canvas.Children.OfType<FrameworkElement>().FirstOrDefault(e => e.Name == name);
            return element;
        }

        //from mgsv commands
        private void Print(string[] args) {
            if (args[2] != null) {
                Console.WriteLine(args[2]);
            }
        }

        private void Clear(string[] args) {
            Console.Clear();
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
                //DEBUGNOW TODO WARN
                return;
            }


            this.Dispatcher.Invoke(() => {
                try {
                    var uiElement = (UIElement)XamlReader.Parse(xamlStr);
                    uiElements[name] = uiElement;

                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    var canvas = mainWindow.MainCanvas;
                    canvas.Children.Add(uiElement);
                } catch (Exception e) {
                    //DEBUGNOW TODO WARN
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
        private void SetContent(string[] args) {
            if (args.Count() < 1 + 2) {
                return;
            }

            string name = args[2];
            string content = args[3];
            if (name == null || content == null) {
                return;
            }

            UIElement uiElement;
            if (!uiElements.TryGetValue(name, out uiElement)) {
                //DEBUGNOW TODO WARN
                return;
            }

            ContentControl contentControl = uiElement as ContentControl;
            if (contentControl == null) {
                //DEBUGNOW TODO WARN
                return;
            }

            this.Dispatcher.Invoke(() => {
                try {
                    contentControl.Content = content;
                } catch (Exception e) {
                    //DEBUGNOW TODO WARN
                    return;
                }
            });
        }//end SetContent

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
    }
}

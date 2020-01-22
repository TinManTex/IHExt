using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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

namespace IHExt {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        private bool usePipe = false;

        string serverInName = "mgsv_in";
        string serverOutName = "mgsv_out";

        private string gameDir;
        private string toExtFilePath;
        private string toMgsvFilePath;

        private SortedDictionary<int, string> extToMgsvCmds = new SortedDictionary<int, string>();//tex from ext to mgsv
        private ConcurrentQueue<string> extToMgsvCmdQueue = new ConcurrentQueue<string>();

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
        //AutomationFocusChangedEventHandler focusHandler = null;

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

            //bool usePipe = false;//GLOBAL
            if (args.Count() > 3) {
                serverInName = args[3];
                usePipe = true;
            }

            if (args.Count() > 4) {
                serverOutName = args[4];
                usePipe = true;
            }

            Console.WriteLine("args:");
            foreach (string arg in args) {
                Console.WriteLine(arg);
            }

            this.gameDir = gameDir;
            this.toExtFilePath = $"{gameDir}/{modDir}/ih_toextcmds.txt";
            this.toMgsvFilePath = $"{gameDir}/{modDir}/ih_tomgsvcmds.txt";

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

            //tex legacy IH ipc via messages in a txt file
            if (!usePipe) {
                FileSystemWatcher watcher = new FileSystemWatcher();
                watcher.Path = Path.GetDirectoryName(toExtFilePath);
                watcher.Filter = Path.GetFileName(toExtFilePath);
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.EnableRaisingEvents = true;//tex < allow subscribed event -v- to actually fire
                watcher.Changed += new FileSystemEventHandler(OnToExtChanged);
            } else {
                BackgroundWorker serverInWorker = new BackgroundWorker();
                serverInWorker.DoWork += new DoWorkEventHandler(serverIn_DoWork);
                serverInWorker.RunWorkerAsync();

                BackgroundWorker serverOutWorker = new BackgroundWorker();
                serverOutWorker.DoWork += new DoWorkEventHandler(serverOut_DoWork);
                serverOutWorker.RunWorkerAsync();
            }

            DateTime currentDate = DateTime.Now;
            extSession = currentDate.Ticks;

            WriteToMgsv();

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();


            AddTestMenuItems(); //DEBUG

            mainWindow.menuItems.ItemsSource = menuItems;
            mainWindow.menuSetting.ItemsSource = settingItems;

            SetFocusToGame();

            ToMgsvCmd($"extSession|{extSession}");
        }

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
        }

        //tex mgsv_in pipe (IHExt out) process thread
        //IN/SIDE: serverInName
        void serverIn_DoWork(object sender, DoWorkEventArgs e) {
            BackgroundWorker worker = (BackgroundWorker)sender;

            using (var serverIn = new NamedPipeClientStream(".", serverInName, PipeDirection.Out)) {//tex: piped named from mgsv standpoint, so we pipe out to mgsv in, and visa versa
                // Connect to the pipe or wait until the pipe is available.
                Console.WriteLine("Attempting to connect to serverIn...");
                serverIn.Connect();

                Console.WriteLine("Connected to pipe.");
                Console.WriteLine("There are currently {0} pipeIn server instances open.", serverIn.NumberOfServerInstances);

                serverIn.ReadMode = PipeTransmissionMode.Message;

                //ToMgsvCmd("0|IHExtStarted");//DEBUG
                StreamWriter sw = new StreamWriter(serverIn);
                while (!worker.CancellationPending) {
                    //sw.Write("Sent from client.");//DEBUG
                    if (extToMgsvCmdQueue.Count() > 0) {
                        string command;
                        while (extToMgsvCmdQueue.TryDequeue(out command)) {
                            Console.WriteLine("Client write: " + command);//DEBUGNOW
                            sw.Write(command);
                        }
                        sw.Flush();
                    }//if extToMgsvCmdQueue
                }//while !worker.CancellationPending
            }//using pipeIn
        }//serverIn_DoWork

        //tex mgsv_out pipe (IHExt in) process thread
        //IN/SIDE: serverOutName
        //IN-OUT/SIDE: mgsvToExtComplete
        void serverOut_DoWork(object sender, DoWorkEventArgs e) {
            BackgroundWorker worker = (BackgroundWorker)sender;

            //tex should just be as above but with PipeDirection.In, but aparently transmissionmode.Message doesn't work unless it's .InOut, or using this constructor 
            //https://stackoverflow.com/questions/32739224/c-sharp-unauthorizedaccessexception-when-enabling-messagemode-for-read-only-name
            using (var serverOut = new NamedPipeClientStream(
                    ".",
                    serverOutName,
                    PipeAccessRights.ReadData | PipeAccessRights.WriteAttributes,
                    PipeOptions.None,
                    System.Security.Principal.TokenImpersonationLevel.None,
                    System.IO.HandleInheritability.None)) {
                // Connect to the pipe or wait until the pipe is available.
                Console.WriteLine("Attempting to connect to serverOut...");
                serverOut.Connect();

                Console.WriteLine("Connected to pipe.");
                Console.WriteLine("There are currently {0} pipe server instances open.", serverOut.NumberOfServerInstances);

                serverOut.ReadMode = PipeTransmissionMode.Message;

                //ToMgsvCmd("IHExtStarted");//DEBUG
                while (!worker.CancellationPending) {
                    StreamReader sr = new StreamReader(serverOut);//tex DEBUGNOW: will hang if ouside the loop
                    string message;
                    int count = 0;
                    //tex message mode doesn't seem to be working for mgsv_out
                    //despite checking everything on both sides and despite it working for mgsv_in
                    //was: while ((line = sr.ReadLine()) != null) {
                    var peek = sr.Peek();//DEBUG
                    while (sr.Peek() > 0) {
                        //OFF see above
                        //message = sr.ReadLine();
                        //message = sr.ReadToEnd();
                        message = ReadByChar(sr);

                        //Console.WriteLine("Received from server: {message}");//DEBUG
                        if (String.IsNullOrEmpty(message)) {
                            continue;
                        }

                        ProcessCommand(message, count);
                        count++;
                    }//while Read
                }//while !worker.CancellationPending
            }//using pipeOut
        }//serverOut_DoWork

        private static string ReadByChar(StreamReader sr) {
            StringBuilder stringBuilder = new StringBuilder();
            char c;
            while (true) {
                c = (char)sr.Read();
                if (c == -1) {//tex end of stream
                    break;
                } else if (c == '\0') {
                    break;
                } else {
                    // if (c == '|') {
                    //tex Could start splitting string here I guess
                    // } else {
                    stringBuilder.Append(c);
                    // }
                }
            }//while true
            return stringBuilder.ToString();
        }

        //tex on ih_toextcmds.txt changed
        private void OnToExtChanged(object source, FileSystemEventArgs e) {
            //Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);

            using (FileStream fs = WaitForFile(this.toExtFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                using (StreamReader sr = new StreamReader(fs)) {
                    string message;
                    int count = 0;
                    while ((message = sr.ReadLine()) != null) {
                        if (String.IsNullOrEmpty(message)) {
                            continue;
                        }

                        //Console.WriteLine(line);//DEBUG
                        ProcessCommand(message, count);
                        count++;
                    }//end while readline
                }// end streamreader
            }//end waitforfile

            WriteToMgsv();
        }//end OnToExtChanged

        //OUT/SIDE: mgsvToExtComplete
        private void ProcessCommand(string message, int count) {
            char[] delimiters = { '|' };
            string[] args = message.Split(delimiters);
            int messageId;

            if (Int32.TryParse(args[0], out messageId)) {
                //tex: first line of text ipc has the index of the completed commands of the opposite stream
                //can't just put it in a command as that would just create a loop of them updating
                if (count == 0 && !usePipe) { 
                    //tex messageid of first line is mgsv session id 
                    if (messageId != mgsvSession) {//DEBUGNOW move to a specfic command from mgsv
                        Console.WriteLine("MGSV session changed");
                        mgsvSession = messageId;
                        ToMgsvCmd("sessionchange");//tex a bit of nothing to get the extToMgsvComplete to update from the message, mgsv does likewise
                    }

                    int arg = 0;
                    if (Int32.TryParse(args[2], out arg)) {
                        extToMgsvComplete = arg;
                    }
                } else {
                   if (usePipe || messageId > mgsvToExtComplete) {//tex IHExt hasn't done this command yet yet 
                        if (args.Length < 1) {
                            Console.WriteLine("WARNING: args.Length < 1");
                        } else {
                            string command = args[1];//tex args 0 is messageId
                            if (!commands.ContainsKey(command)) {
                                Console.WriteLine("WARNING: Unrecogined command:" + command);
                            } else {
                                commands[command](args);//tex call command function
                            }
                        }//if args

                        mgsvToExtComplete = messageId;
                  }//if > mgsvToExtComplete
                }//if count
            }// parse messageId
        }//ProcessCommand

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

        //to mgsv commands
        //IN/SIDE: usePipe
        public void ToMgsvCmd(string cmd) {
            string message = extToMgsvCurrent.ToString() + "|" + cmd;
            if (!usePipe) {
                extToMgsvCmds.Add(extToMgsvCurrent, message);
            } else {
                extToMgsvCmdQueue.Enqueue(message);
            }
            extToMgsvCurrent++;
            WriteToMgsv();
        }

        //IN/SIDE: usePipe
        private void WriteToMgsv() {
            if (!usePipe) {
                WriteToMgsvFile();
            } else {
                //tex pipe thread processes extToMgsvCmdQueue itself
            }
        }//WriteToMgsv

        //IN/SIDE: extToMgsvCurrent
        private void WriteToMgsvFile() {
            //tex lua/mgsv io "r" opens in exclusive/lock, so have to wait
            using (FileStream fs = WaitForFile(this.toMgsvFilePath, FileMode.Truncate, FileAccess.Write, FileShare.None)) {
                using (StreamWriter sw = new StreamWriter(fs)) {
                    //tex always on first line, lets mgsv know what commands have been completed so it can cull them from the mgsvToExt file to stop it from infinitely growing
                    //can't just put it in a command as that would just create a loop of them updating
                    //extSession not really needed as that is updated via it's own command
                    string cmdToExtCompleted = string.Format($"{extSession}|cmdToExtCompletedIndex|{mgsvToExtComplete}");
                    sw.WriteLine(cmdToExtCompleted);
                    //tex really from extToMgsvComplete+1, but the check for that uglifys code too much, and I can live with last complete command staying in the txt files
                    for (int i = extToMgsvComplete; i < extToMgsvCurrent; i++) {
                        string line = extToMgsvCmds[i];
                        sw.WriteLine(line);
                    }
                    sw.Flush();
                }
            }
        }//WriteToMgsvFile

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

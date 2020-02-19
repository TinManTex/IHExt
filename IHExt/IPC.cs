using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;// WaitForFile
using System.Threading.Tasks;

namespace IHExt {
    public class IPC {
        bool usePipe = true;//LEGACY IHExt text IPC DEBUGNOW
        private string toExtFilePath;//LEGACY IHExt text IPC DEBUGNOW
        private string toMgsvFilePath;//LEGACY IHExt text IPC DEBUGNOW

        string serverInName = "mgsv_in";
        string serverOutName = "mgsv_out";

        private SortedDictionary<int, string> extToMgsvCmds = new SortedDictionary<int, string>();//tex from ext to mgsv
        private ConcurrentQueue<string> extToMgsvCmdQueue = new ConcurrentQueue<string>();

        private Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();

        private int extToMgsvCurrent = 0;//tex current/max, last command to be written out
        private int extToMgsvComplete = 0;//tex min/confirmed executed by mgsv, only commands above this should be written out
        private int mgsvToExtComplete = 0;//tex min/confimed executed by ext

        private long extSession = 0;
        private long mgsvSession = 0;

        public IPC(bool _usePipe, string _serverInName, string _serverOutName, string _toExtFilePath = null, string _toMgsvFilePath = null, long _extSession = 0) {
            usePipe = _usePipe;
            extSession = _extSession;

            if (_serverInName != null) {
                serverInName = _serverInName;
            }
            if (_serverOutName != null) {
                serverOutName = _serverOutName;
            }

            if (_toExtFilePath != null) {
                toExtFilePath = _toExtFilePath;
            }

            if (_toMgsvFilePath != null) {
                toMgsvFilePath = _toMgsvFilePath;
            }
        }//IPC() ctor

        public void StartPipeThreads() {
            BackgroundWorker serverInWorker = new BackgroundWorker();
            serverInWorker.DoWork += new DoWorkEventHandler(serverIn_DoWork);
            serverInWorker.RunWorkerAsync();

            BackgroundWorker serverOutWorker = new BackgroundWorker();
            serverOutWorker.DoWork += new DoWorkEventHandler(serverOut_DoWork);
            serverOutWorker.RunWorkerAsync();
        }//StartPipeThreads

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
                StreamWriter sw = new StreamWriter(serverIn, Encoding.UTF8);
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

            //tex there's an issue with client/in pipes not working in message mode
            //https://stackoverflow.com/questions/32739224/c-sharp-unauthorizedaccessexception-when-enabling-messagemode-for-read-only-name
            //The solution below lets you keep the server as out only (OUTBOUND in c++), but this constructor for NamedPipeClientStream isn't available in .net standard (thus not unity)
            //using (var serverOut = new NamedPipeClientStream(
            //        ".",
            //        serverOutName,
            //        PipeAccessRights.ReadData | PipeAccessRights.WriteAttributes,
            //        PipeOptions.None,
            //        System.Security.Principal.TokenImpersonationLevel.None,
            //        System.IO.HandleInheritability.None)) {
            //tex so using this instead of above, where pipedirection is InOut instead of In, the gotcha is server must be InOut/DUPLEX as well
            //GOTCHA: which also theoretically means a client could stall the pipe if it writes to it as IHHook only treats is as out only.
            using (var serverOut = new NamedPipeClientStream(".", serverOutName, PipeDirection.InOut)) {
                // Connect to the pipe or wait until the pipe is available.
                Console.WriteLine("Attempting to connect to serverOut...");
                serverOut.Connect();

                Console.WriteLine("Connected to pipe.");
                Console.WriteLine("There are currently {0} pipe server instances open.", serverOut.NumberOfServerInstances);

                serverOut.ReadMode = PipeTransmissionMode.Message;

                //ToMgsvCmd("IHExtStarted");//DEBUG
                while (!worker.CancellationPending) {
                    StreamReader sr = new StreamReader(serverOut, Encoding.UTF8);//tex DEBUGNOW: will hang if ouside the loop
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
        }//ReadByChar

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
        }//ToMgsvCmd

        public void AddCommand(string name, Action<string[]> command) {
            commands.Add(name, command);
        }

        //LEGACY IHExt text IPC
        public void StartFileWatcher() {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(this.toExtFilePath);
            watcher.Filter = Path.GetFileName(this.toExtFilePath);
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;//tex < allow subscribed event -v- to actually fire
            watcher.Changed += new FileSystemEventHandler(OnToExtChanged);
        }//StartFileWatcher

        //LEGACY IHExt text IPC
        //IN/SIDE: usePipe
        public void WriteToMgsv() {
            if (!usePipe) {
                WriteToMgsvFile();
            } else {
                //tex pipe thread processes extToMgsvCmdQueue itself
            }
        }//WriteToMgsv

        //LEGACY IHExt text IPC
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

        //LEGACY IHExt text IPC
        public FileStream WaitForFile(string fullPath, FileMode mode, FileAccess access, FileShare share) {
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
            }//for numtries

            return null;
        }//WaitForFile

        //LEGACY IHExt text IPC
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
    }//PipeClient
}//namespace IHExt

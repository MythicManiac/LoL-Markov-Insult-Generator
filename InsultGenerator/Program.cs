using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TextMarkovChains;
using Newtonsoft.Json;

namespace InsultGenerator
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Window window = new Window();
            if (window.Initialize())
            {
                Application.Run(window);
            }
            window.Unhook();
        }
    }

    public class Window : Form
    {
        private List<string>[] _insultWords;
        private List<string> _disclaimer;
        private bool _canGenerate;
        private Random _random;
        private const int _WH_KEYBOARD_LL = 13;
        private const int _WM_KEYDOWN = 0x0100;
        private LowLevelKeyboardProc _proc;
        private Thread _insultSender;
        private volatile bool _threadWorking;
        private static IntPtr _hookID = IntPtr.Zero;
        private TextMarkovChain markovChain;

        [DllImport("user32.dll")]
        static extern uint keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)_WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (((Keys)vkCode) == Keys.NumPad0)
                {
                    if (!_threadWorking)
                    {
                        _threadWorking = true;
                        _insultSender = new Thread(InsultThread);
                        _insultSender.Start();
                    }
                    return (IntPtr)1;
                }
                else if (((Keys)vkCode) == Keys.NumPad1)
                {
                    if (!_threadWorking)
                    {
                        _threadWorking = true;
                        _insultSender = new Thread(DisclaimerThread);
                        _insultSender.Start();
                    }
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void InsultThread()
        {
            SendInsult();
            _threadWorking = false;
        }

        private void DisclaimerThread()
        {
            for (int i = 0; i < _disclaimer.Count; i++)
            {
                SendInsult(Environment.NewLine + _disclaimer[i] + Environment.NewLine);
            }
            _threadWorking = false;
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(_WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public Window()
        {
            _threadWorking = false;
            _canGenerate = false;
            _proc = this.HookCallback;
            _hookID = SetHook(_proc);
            Height = 0;
            Text = "Automatic Insult Generator";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            LoadMarkov();
        }

        private void LoadMarkov()
        {
            markovChain = new TextMarkovChain();
            Console.WriteLine("Loading messages...");
            var messages = JsonConvert.DeserializeObject<MessageCollection>(File.ReadAllText("input-data.json"));
            Console.WriteLine("Filtering messages...");
            messages.FilterByChampions(new string[] { "Nidalee" }, false);
            messages.FilterByToxicity(true);
            Console.WriteLine("Feeding messages...");
            foreach (var entry in messages.Messages)
            {
                if (string.IsNullOrWhiteSpace(entry.Content)) continue;
                markovChain.feed(entry.Content.Trim());
            }
            Console.WriteLine("Done!");
        }

        public void Unhook()
        {
            UnhookWindowsHookEx(_hookID);
        }

        public void SendInsult(string insult = "")
        {
            insult = markovChain.generateSentence();
            if (insult == "")
            {
                insult = (Generate() + " ");
            }
            foreach (char key in insult)
            {
                SendKey(key);
                Thread.Sleep(5);
            }
        }

        private byte GetVirtualChar(char key)
        {
            if (key == '.')
            {
                return (byte)Keys.OemPeriod;
            }
            else if (key == ',')
            {
                return (byte)Keys.Oemcomma;
            }
            else if (key == '-')
            {
                return (byte)Keys.OemMinus;
            }
            else if (key == Environment.NewLine[0])
            {
                return (byte)Keys.Return;
            }
            else if (char.IsLetter(key) || key == ' ')
            {
                return (byte)(Keys)char.ToUpper(key);
            }
            else return 0;
        }

        private void SendKey(char key)
        {
            VirtualKeyDown(key);
            VirtualKeyUp(key);
        }

        private void VirtualKeyDown(char key)
        {
            if (key == ':')
            {
                keybd_event((byte)Keys.LShiftKey, 0, 0, 0);
                keybd_event((byte)Keys.OemPeriod, 0, 0, 0);
                keybd_event((byte)Keys.LShiftKey, 0, 0x0002, 0);
            }
            else if (key == '/')
            {
                keybd_event((byte)Keys.LShiftKey, 0, 0, 0);
                keybd_event((byte)Keys.D7, 0, 0, 0);
                keybd_event((byte)Keys.LShiftKey, 0, 0x0002, 0);
            }
            else if (char.IsUpper(key) && char.IsLetter(key))
            {
                keybd_event((byte)Keys.LShiftKey, 0, 0, 0);
                keybd_event((byte)((Keys)key), 0, 0, 0);
                keybd_event((byte)Keys.LShiftKey, 0, 0x0002, 0);
            }
            else
            {
                byte data = GetVirtualChar(key);
                keybd_event(data, 0, 0, 0);
            }
        }

        private void VirtualKeyUp(char key)
        {
            if (key == ':')
            {
                keybd_event((byte)Keys.LShiftKey, 0, 0, 0);
                keybd_event((byte)Keys.OemPeriod, 0, 0x0002, 0);
                keybd_event((byte)Keys.LShiftKey, 0, 0x0002, 0);
            }
            else if (key == '/')
            {
                keybd_event((byte)Keys.LShiftKey, 0, 0, 0);
                keybd_event((byte)Keys.D7, 0, 0x0002, 0);
                keybd_event((byte)Keys.LShiftKey, 0, 0x0002, 0);
            }
            else if (char.IsUpper(key) && char.IsLetter(key))
            {
                keybd_event((byte)Keys.LShiftKey, 0, 0, 0);
                keybd_event((byte)((Keys)key), 0, 0x0002, 0);
                keybd_event((byte)Keys.LShiftKey, 0, 0x0002, 0);
            }
            {
                byte data = GetVirtualChar(key);
                keybd_event(data, 0, 0x7F, 0);
            }
        }

        public bool Initialize()
        {
            _random = new Random();
            _insultWords = new List<string>[3];
            for (int i = 0; i < _insultWords.Length; i++)
            {
                _insultWords[i] = new List<string>();
                if (!File.Exists("words" + i + ".txt"))
                {
                    FileStream stream = File.Create("words" + i + ".txt");
                    stream.Dispose();
                }
                using (StreamReader reader = new StreamReader("words" + i + ".txt"))
                {
                    int lineNum = 1;
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (!checkInvalidCharacters(line))
                        {
                            _insultWords[i].Add(line);
                        }
                        else
                        {
                            MessageBox.Show("Invalid characters at words" + i + ".txt, line [" + lineNum + "] - Dismissing.\nAllowed characters: All alphabetics / : , . -");
                        }
                        lineNum++;
                    }
                }
                if (_insultWords[i].Count < 1)
                {
                    MessageBox.Show("At least one word per configuration file must be present");
                    return false;
                }
            }
            _disclaimer = new List<string>();
            if (!File.Exists("disclaimer.txt"))
            {
                FileStream stream = File.Create("disclaimer.txt");
                stream.Dispose();
            }
            using (StreamReader reader = new StreamReader("disclaimer.txt"))
            {
                int lineNum = 1;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (!checkInvalidCharacters(line))
                    {
                        _disclaimer.Add(line);
                    }
                    else
                    {
                        MessageBox.Show("Invalid characters at disclaimer.txt, line [" + lineNum + "] - Dismissing.\nAllowed characters: All alphabetics / : , . -");
                    }
                    lineNum++;
                }
            }
            _canGenerate = true;
            return true;
        }

        private bool checkInvalidCharacters(string input)
        {
            foreach (char character in input)
            {
                if (!char.IsNumber(character) &&
                    !char.IsLetter(character) &&
                    character != '-' &&
                    character != ':' &&
                    character != '.' &&
                    character != ',' &&
                    character != '/' &&
                    character != ' ')
                {
                    return true;
                }
            }
            return false;
        }

        public string Generate()
        {
            if (_canGenerate)
            {
                string[] insult = new string[3];
                for (int i = 0; i < insult.Length; i++)
                {
                    insult[i] = _insultWords[i][_random.Next(_insultWords[i].Count)];
                }
                return insult[0] + " " + insult[1] + " " + insult[2];
            }
            else
            {
                return "Invalid configuration";
            }
        }
    }
}
using ProgramMethod;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace SimulatedReader
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket ProgramSocket;
        private byte[] KeepAlive()
        {
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes((uint)1000).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
            BitConverter.GetBytes((uint)500).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
            return inOptionValues;
        }
        private FileMethod PGMethod = new FileMethod();
        private SynchronizationContext SyncContext = null;
        private bool IsReaderON = false;
        private string InputText = "";
        public MainWindow()
        {
            InitializeComponent();
            SyncContext = SynchronizationContext.Current;
        }
        private void PostMessage(object ErrorMsg)
        {
            msg.Text = ErrorMsg.ToString();
        }
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(IP.Text) || string.IsNullOrEmpty(Port.Text))
            {
                PostMessage("請輸入啟用Socket資料");
                return;
            }
            ProgramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endport = new IPEndPoint(IPAddress.Parse(IP.Text), int.Parse(Port.Text));
            try
            {
                ProgramSocket.Bind(endport);
                ProgramSocket.Listen(50);
                ProgramSocket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(), null);
                Thread thread = new Thread(Recevice);
                thread.IsBackground = true;
                thread.Start(ProgramSocket);
                Socket.Background = PGMethod.Success;
                Socket.Foreground = PGMethod.White;
                PostMessage("");
            }
            catch (Exception ex)
            {
                PostMessage("讀頭啟動失敗");
                Socket.Foreground = PGMethod.Danger;
            }
        }
        private void Recevice(object obj)
        {
            var socket = obj as Socket;
            while (true)
            {
                string remoteEpInfo = string.Empty;
                try
                {
                    Socket txSocket = socket.Accept();
                    if (txSocket.Connected)
                    {
                        remoteEpInfo = txSocket.RemoteEndPoint.ToString();
                        ReceseMsgGoing(txSocket, remoteEpInfo);
                    }
                }
                catch (Exception Ex)
                {
                    SyncContext.Post(PostMessage, "客戶端連線失敗");
                }
            }
        }
        private void ReceseMsgGoing(Socket txSocket, string remoteEpInfo)
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] recesiveByte = new byte[1024];
                        int getlength = txSocket.Receive(recesiveByte);
                        if (getlength <= 0) { continue; }
                        string getmsg = Encoding.UTF8.GetString(recesiveByte, 0, getlength);
                        if (string.IsNullOrEmpty(getmsg))
                        {
                            SyncContext.Post(PostMessage, "傳送空白資料");
                            break;
                        }
                        switch (getmsg.Trim())
                        {
                            case "LON":
                                SyncContext.Post(SetReaderStatus, true);
                                SyncContext.Post(ListenInput, txSocket);
                                break;
                            case "LOFF":
                                SyncContext.Post(SetReaderStatus, false);
                                break;
                        }
                    }
                    catch (Exception Ex)
                    {
                        txSocket.Dispose();
                        txSocket.Close();
                        SyncContext.Post(PostMessage, "來源" + remoteEpInfo + ":" + Ex.Message);
                        break;
                    }
                }
            })
            {
                IsBackground = true
            };
            thread.Start();
        }
        private void SetReaderStatus(object ReaderStatus)
        {
            if ((bool)ReaderStatus)
            {
                Reader.Background = PGMethod.Success;
                Reader.Foreground = PGMethod.White;
                InputData.IsReadOnly = false;
            }
            else
            {
                Reader.Background = PGMethod.Transparent;
                Reader.Foreground = PGMethod.DarkGray;
                InputData.IsReadOnly = true;
            }
            IsReaderON = (bool)ReaderStatus;
        }
        private void ListenInput(object SendSocket)
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(InputText))
                        {
                            Thread.Sleep(300);
                            continue;
                        }
                        Socket SendTo = (Socket)SendSocket;
                        if (SendTo.Connected)
                        {
                            Encoding ei = Encoding.GetEncoding(950);
                            int sendMsgLength = SendTo.Send(ei.GetBytes(InputText));
                            InputText = "";
                            SyncContext.Post(ClearInput, 0);
                        }
                        break;
                    }
                    catch (Exception Ex)
                    {
                        SyncContext.Post(PostMessage, "傳送資料失敗:" + Ex.Message);
                        break;
                    }
                }
            })
            {
                IsBackground = true
            };
            thread.Start();
        }
        private void ClearInput(object stringText)
        {
            InputData.Text = "";
            SetReaderStatus(false);
        }
        private void InputData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputText = InputData.Text.Trim();
            }
        }
    }
}

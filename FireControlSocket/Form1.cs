using FireControlSocket.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using FireControlSocket.DB;

namespace FireControlSocket
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private static string TableName;
        private static string ColumnInfo;
        private static string ColumnTime;
        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.Items.Add("UDP");
            comboBox1.Items.Add("TCP Client");
            comboBox1.Items.Add("TCP Server");
            comboBox1.SelectedIndex = 2;

            Ini.strFilePath = Application.StartupPath + "\\Config.ini";
            var strSec = Path.GetFileNameWithoutExtension(Ini.strFilePath);
            db_Address.Text = Ini.ContentValue(strSec, "db_Address");
            db_Name.Text = Ini.ContentValue(strSec, "db_Name");
            db_Pass.Text = Ini.ContentValue(strSec, "db_Pass");
            db_BaseName.Text = Ini.ContentValue(strSec, "db_BaseName");
            db_TableName.Text = Ini.ContentValue(strSec, "db_TableName");
            db_ColumnInfo.Text = Ini.ContentValue(strSec, "db_ColumnInfo");
            db_ColumnTime.Text = Ini.ContentValue(strSec, "db_ColumnTime");

            TableName = db_TableName.Text;
            ColumnInfo = db_ColumnInfo.Text;
            ColumnTime = db_ColumnTime.Text;

            DbHelperSQL.connectionString = string.Format("Data Source={0};Initial Catalog={1};User ID={2};password={3}", db_Address.Text, db_BaseName.Text, db_Name.Text, db_Pass.Text);
        }

        //监听数据
        private void Listen()
        {

            //程序的listener一直不关闭
            //listener.Close();
        }

        //线程内向文本框txtRecvMssg中添加字符串及委托
        private delegate void PrintRecvMssgDelegate(string s);
        private void PrintRecvMssg(string info)
        {
            LogEx.l(info);
            string sql = string.Format("INSERT INTO {0} ({1},{2}) VALUES('{3}','{4}')", TableName, ColumnInfo, ColumnTime, info, DateTime.Now);
            var i=  DbHelperSQL.ExecuteSql(sql);
            LogEx.l(i > 0 ? "Insert Success" : "Insert Fail");
            //txtRecvMssg.Text += string.Format("[{0}]:{1}\r\n",
            //    DateTime.Now.ToLongTimeString(), info);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(db_Address.Text) || string.IsNullOrWhiteSpace(db_BaseName.Text) || string.IsNullOrWhiteSpace(TableName) || string.IsNullOrWhiteSpace(db_Name.Text) || string.IsNullOrWhiteSpace(db_Pass.Text) || string.IsNullOrWhiteSpace(ColumnInfo) || string.IsNullOrWhiteSpace(ColumnTime))
            {
                MessageBox.Show("请先完善数据库信息并保存！");
                return;
            }
            this.button1.Enabled = false;
            if (comboBox1.SelectedIndex == 0)
            {
                Thread thrListener = new Thread(new ThreadStart(UDPListen));
                thrListener.Start();
            }
            else if (comboBox1.SelectedIndex == 1)
            {
                Thread thrListener = new Thread(new ThreadStart(TCPClientListen));
                thrListener.Start();
            }
            else
            {
                Thread thrListener = new Thread(new ThreadStart(TCPServerListen));
                thrListener.Start();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //强制关闭程序（强行终止Listener）
            Environment.Exit(0);
        }

        private void button2_Click(object sender, EventArgs e)
        {

            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(IPAddress.Parse("127.0.0.1"), Convert.ToInt32(textBox1.Text));

            NetworkStream ntwStream = tcpClient.GetStream();
            if (ntwStream.CanWrite)
            {
                Byte[] bytSend = Encoding.UTF8.GetBytes(textBox2.Text);
                ntwStream.Write(bytSend, 0, bytSend.Length);
            }
            else
            {
                MessageBox.Show("无法写入数据流");

                ntwStream.Close();
                tcpClient.Close();

                return;
            }

            ntwStream.Close();
            tcpClient.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Parse("127.0.0.1"), Convert.ToInt32(textBox1.Text));

            Byte[] bytSend = Encoding.UTF8.GetBytes(textBox2.Text);
            udpClient.Send(bytSend, bytSend.Length);

            udpClient.Close();
        }


        public void TCPClientListen()
        {
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Any, Convert.ToInt32(textBox1.Text)));

            //不断监听端口
            while (true)
            {
                listener.Listen(10);
                Socket socket = listener.Accept();
                NetworkStream ntwStream = new NetworkStream(socket);
                StreamReader strmReader = new StreamReader(ntwStream);
                Invoke(new PrintRecvMssgDelegate(PrintRecvMssg),
                    new object[] { strmReader.ReadToEnd() });
                socket.Close();
            }
        }

        public void TCPServerListen()
        {
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Any, Convert.ToInt32(textBox1.Text)));

            //不断监听端口
            while (true)
            {
                listener.Listen(10);
                Socket socket = listener.Accept();

                byte[] buffer = new byte[18];
                int receiveNumber = socket.Receive(buffer);
                //  string message = Encoding.Default.GetString(buffer, 0, receiveNumber);

                string message = byteToHexStr(buffer); //强制转换成16进制字符串
                Invoke(new PrintRecvMssgDelegate(PrintRecvMssg),

                    new object[] { message });
                socket.Close();
            }
        }

        public void UDPListen()
        {
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);
            byte[] buffer = new byte[1024];
            EndPoint point = new IPEndPoint(IPAddress.Any, Convert.ToInt32(textBox1.Text));
            listener.Bind(point);

            //不断监听端口
            while (true)
            {
                //Socket socket = listener.Accept();
                int Length = listener.ReceiveFrom(buffer, ref point);
                string message = Encoding.Default.GetString(buffer, 0, Length);
                Invoke(new PrintRecvMssgDelegate(PrintRecvMssg),
                    new object[] { message });
            }
        }

        /// <summary>
        /// 字节数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string byteToHexStr(byte[] bytes)
        {
            string returnStr = "";
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    returnStr += " " + bytes[i].ToString("X2");
                }
            }
            return returnStr;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var strSec = Path.GetFileNameWithoutExtension(Ini.strFilePath);
            Ini.Wini("db_Address", db_Address.Text.Trim());
            Ini.Wini("db_Name", db_Name.Text.Trim());
            Ini.Wini("db_Pass", db_Pass.Text.Trim());
            Ini.Wini("db_BaseName", db_BaseName.Text.Trim());
            Ini.Wini("db_TableName", db_TableName.Text.Trim());
            Ini.Wini("db_ColumnInfo", db_ColumnInfo.Text.Trim());
            Ini.Wini("db_ColumnTime", db_ColumnTime.Text.Trim());

            TableName = db_TableName.Text;
            ColumnInfo = db_ColumnInfo.Text;
            ColumnTime = db_ColumnTime.Text;

            DbHelperSQL.connectionString = string.Format("Data Source={0};Initial Catalog={1};User ID={2};password={3}", db_Address.Text, db_BaseName.Text, db_Name.Text, db_Pass.Text);
            MessageBox.Show("保存成功");
        }

    }
}

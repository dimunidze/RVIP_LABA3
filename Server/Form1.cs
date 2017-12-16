using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Server {
    public partial class Form1 : Form {
        private Socket _listener;
        private MonitoringSystem _system;
        private ConcurrentDictionary<int, bool> _states;
        public Form1() {
            InitializeComponent();
        }
 
        private void StartBtn_Click(object sender, EventArgs e) {
            richTextBox1.Text = string.Empty;
            startBtn.Enabled = false;
            stopbtn.Enabled = true;
            if (_listener == null) {
                //IP адрес локальной машины
                var ipAddr = new IPAddress(new byte[] { 127, 0, 0, 1 });
                //Конечная точка (IP, порт)
                var ipEndPoint = new IPEndPoint(ipAddr, 33322);
                //Инициализируем сокет
                _listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                //Привязываем его к конечной точке (будет принимать трафик идущий на 127.0.0.1:33322)
                _listener.Bind(ipEndPoint);
                //Начинаем прослушивание
                _listener.Listen(200);
                //Запускаем метод приема данных в отдельном потоке
                ThreadPool.QueueUserWorkItem(Start, new object());
            }
            //Зпускаем камеры
            var camerasCount = (int)numericUpDown1.Value;
            _states = new ConcurrentDictionary<int, bool>();
            for (var i = 0; i < camerasCount; ++i) {
                while (!_states.TryAdd(i + 1, true)) {
                    ;
                }
            }
            _system = new MonitoringSystem(_states);
            //Подписка на событие WriteToLog, Begininvoke выполняет переданный делегат в UI потоке
            _system.WriteToLog += (s, msg) => BeginInvoke((MethodInvoker)(() => {
                var sb = new StringBuilder(richTextBox1.Text);
                sb.Append(msg + Environment.NewLine);
                richTextBox1.Text = sb.ToString();
            }));
            for (var i = 0; i < camerasCount; ++i) {
                var process = new Process();
                var x = (i % 3) * 400;
                var y = i > 2 ? 360 : 0;
                process.StartInfo.Arguments = (i + 1).ToString() + " " + x.ToString() + " " + y.ToString();
                process.StartInfo.WorkingDirectory = GetClientDirectory();
                process.StartInfo.FileName = "Client.exe";
                process.Start();
                Thread.Sleep(100);
            }
        }
        //Определяет путь до папки с процессом клиента
        private string GetClientDirectory() {
            // путь до ../Rvip3/Server/bin/Release
            var directory = Directory.GetCurrentDirectory();
            // путь до ../Rvip3/Server/bin
            var parent = Directory.GetParent(directory);
            // путь до ../Rvip3/Server/
            parent = Directory.GetParent(parent.FullName);
            // путь до ../Rvip3
            parent = Directory.GetParent(parent.FullName);
            return parent.FullName + @"\Client\bin\Release";
        }

        private void Start(object param) {
            while (true) {
                if (_listener != null) {
                    // Сокет для "связи" с камерой
                    var acceptSocket = _listener.Accept();
                    //Запускает метод AcceptData в одном из потоков из пула. в AcceptData передается acceptsocket, "упакованный" в object 
                    ThreadPool.QueueUserWorkItem(_system.AcceptData, acceptSocket);
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            Environment.Exit(0);
        }

        private async void stopbtn_Click(object sender, EventArgs e) {
            _system.Stop();
            stopbtn.Enabled = false;
            //Ожидаем обработки всех данных
            await Task.Run(() => {
                while (true) {
                    if (_system.Stoped && _system.AllDataHandled) {
                        Thread.Sleep(5000);
                        break;
                    }
                }
            });
            startBtn.Enabled = true;
        }
    }
}
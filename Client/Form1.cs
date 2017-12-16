using System.Net.Sockets;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Net;
using System.Collections.Generic;
using System;
using System.Collections.Concurrent;

namespace Client {
    public partial class Form1 : Form {
        //Номер
        private int _number;
        //Включен ли захват данных
        private bool _enable;
        //Все ли захваченные данные переданы
        private bool _allDataSended;
        private static Random Rnd = new Random();
        public Form1(string name, int x, int y) {
            InitializeComponent();
            Text += " " + name;
            Location = new Point(x, y);
            _number = int.Parse(name);
            _enable = true;
            ThreadPool.QueueUserWorkItem(StartSendData, new object());
        }

        private void StartSendData(object o) {
            while (true) {
                SendData();
                Thread.Sleep(4000);
            }
        }

        //Инициализация сокета
        private KeyValuePair<Socket, IPEndPoint> InitSocket() {
            //IP адрес локальной машины
            var ipAddr = new IPAddress(new byte[] { 127, 0, 0, 1 });
            //Конечная точка (IP, порт)
            var ipEndPoint = new IPEndPoint(ipAddr, 33322);
            //Инициализируем сокет
            var res = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            return new KeyValuePair<Socket, IPEndPoint>(res, ipEndPoint);
        }

        //Прием данных о том, нужно ли отключать камеру и передача флага о том, переданы ли все данные
        private void SendAllDataSendedFlag() {
            var res = InitSocket();
            var socket = res.Key;
            try {
                socket.Connect(res.Value);
                var cameranumberByte = BitConverter.GetBytes(_number);
                var byteFlag = BitConverter.GetBytes(false);
                socket.Send(cameranumberByte);
                socket.Send(byteFlag);
                var sendByte = BitConverter.GetBytes(_allDataSended);
                socket.Send(sendByte);
            } catch (Exception ex) {
                BeginInvoke((MethodInvoker)(() => {
                    richTextBox1.Text += ex.Message + Environment.NewLine;
                }));
            }
        }

        //Генерация данных о нарушителях
        private DataToSend GenerateData() {
            var speed = Rnd.Next(50, 70);
            var photo = new byte[1 << 19];
            Rnd.NextBytes(photo);
            var data = new DataToSend {
                CameraNumber = _number,
                Number = Rnd.Next(1, 50000),
                Speed = speed,
                Photo = photo,
            };
            return data;
        }

        //Отправка данных на сервер
        private void SendData() {
            var data = GenerateData();
            BeginInvoke((MethodInvoker)(() => {
                richTextBox1.Text += "Зафиксирован автомобиль. Номер: " + data.Number + " Скорость: " + data.Speed + " .Отправка данных\n";
            }));
            var res = InitSocket();
            var socket = res.Key;
            try {
                socket.Connect(res.Value);
                var cameranumberByte = BitConverter.GetBytes(data.CameraNumber);
                socket.Send(cameranumberByte);
                var autoNumByte = BitConverter.GetBytes(data.Number);
                socket.Send(autoNumByte);
                var speedByte = BitConverter.GetBytes(data.Speed);
                socket.Send(speedByte);
                var photoLengthByte = BitConverter.GetBytes(data.Photo.Length);
                socket.Send(photoLengthByte);
                var offset = 0;
                while (offset < data.Photo.Length) {
                    var bufferSize = Math.Min(4096, data.Photo.Length - offset);
                    var bufferSizeByte = BitConverter.GetBytes(bufferSize);
                    socket.Send(bufferSizeByte);
                    var buffer = new byte[bufferSize];
                    Array.Copy(buffer, 0, data.Photo, offset, buffer.Length);
                    socket.Send(buffer);
                    offset += bufferSize;
                }
                BeginInvoke((MethodInvoker)(() => {
                    richTextBox1.Text += "Данные об автомобиле с номером" + data.Number + " отправлены\n";
                }));
                var enableByte = new byte[1];
                socket.Receive(enableByte);
                var enable = BitConverter.ToBoolean(enableByte, 0);
                _enable = enable;
            } catch (Exception ex) {
                BeginInvoke((MethodInvoker)(() => {
                    richTextBox1.Text += ex.Message + Environment.NewLine;
                }));
            }
            //закрываем процесс камеры после отправки данных
            if (!_enable) {
                Invoke((MethodInvoker)(() => {
                    richTextBox1.Text += "Камера отключится через 3 секунды\n";
                }));
                Thread.Sleep(3000);
                Environment.Exit(0);
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            Environment.Exit(0);
        }
    }
    //Данные для отправуи в систему
    public class DataToSend {
        public int CameraNumber { get; set; }
        public int Number { get; set; }
        public bool Flag { get; set; }
        public byte[] Photo { get; set; }
        public int Speed { get; set; }
    }
}
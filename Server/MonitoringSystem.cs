using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Server {
    public class MonitoringSystem {
        //Остановлен ли прием данных
        private bool _stoped = false;
        //Кол-во обрабатываемых нарушителей
        private static int _inhurtHandled;

        //объект для блокировки
        private static object _inhurtLocker = new object();

        public bool Stoped {
            get {
                return _stoped;
            }
        }
        //Все ли переданные данные обработаны
        public bool AllDataHandled { get; private set; }

        //Событие записи в richtextbox
        public event EventHandler<string> WriteToLog = delegate { };

        //Коллекция, хранящая данные о том, оставлять ли камеру включенной;
        private ConcurrentDictionary<int, bool> _states;

        //Очередь нарушителей
        private ConcurrentQueue<Intuder> _intuders;

        //БД ГИБДД
        private List<Intuder> _intudersDb;

        //Поток для отправки нарушений в БД гибдд;
        private Thread _intuderHandler;

        public MonitoringSystem(ConcurrentDictionary<int, bool> states) {
            _states = states;
            _intuders = new ConcurrentQueue<Intuder>();
            _intudersDb = new List<Intuder>();
            _intuderHandler = new Thread(new ThreadStart(IntuderQueueHandle));
            _intuderHandler.Start();
            _inhurtHandled = 0;
        }

        //Принимает данные с камеры и запускает обработку
        public void AcceptData(object socket) {
            lock (_inhurtLocker) {
                _inhurtHandled++;
            }
            try {
                var acceptSocket = (Socket)socket;
                //Принимаем номер камеры
                var cameraNumberByte = new byte[4];
                acceptSocket.Receive(cameraNumberByte);
                var cameraNumber = BitConverter.ToInt32(cameraNumberByte, 0);

                //Принимаем номер автомобиля
                var autoNumByte = new byte[4];
                acceptSocket.Receive(autoNumByte);
                var autoNum = BitConverter.ToInt32(autoNumByte, 0);

                //Принимаем скорость нарушителя
                var speedByte = new byte[4];
                acceptSocket.Receive(speedByte);
                var speed = BitConverter.ToInt32(speedByte, 0);

                //Принимаем размер фотографии и саму фотографию
                var photoLengthByte = new byte[4];
                acceptSocket.Receive(photoLengthByte);
                var photolenght = BitConverter.ToInt32(photoLengthByte, 0);
                var photo = new byte[photolenght];
                var offset = 0;
                while (offset < photolenght) {
                    var bufferSizeByte = new byte[4];
                    acceptSocket.Receive(bufferSizeByte);
                    var bufferSize = BitConverter.ToInt32(bufferSizeByte, 0);
                    var buffer = new byte[bufferSize];
                    acceptSocket.Receive(buffer);
                    Array.Copy(buffer, 0, photo, offset, buffer.Length);
                    offset += bufferSize;
                }

                string name = GetNameByAutoNumber(autoNum);
                //Вывод в richtextbox
                WriteToLog.Invoke(this, "Камера" + cameraNumber + "передала данные. Номер: " + autoNum + ", скорость: " + speed);
                //Отключаем камеру, если необходимо
                var isEnable = true;
                if (_stoped) {
                    isEnable = false;
                    _states[cameraNumber] = false;
                }
                var isEnableByte = BitConverter.GetBytes(isEnable);
                acceptSocket.Send(isEnableByte);
                //Запускаем обработку данных;
                StartDataHandle(autoNum, photo, speed, cameraNumber);
            } catch(Exception ex) {
                WriteToLog(this, ex.Message + Environment.NewLine);
            }
        }

        //Обработчик данных, пришедших с камеры
        private void StartDataHandle(int number, byte[] photo, int speed, int camera) {
            //Моделируем идентификацию гражданина по номеру авто
            var name = GetNameByAutoNumber(number);
            Thread.Sleep(3000);
            if (speed > 60) {
                WriteToLog(this, "Нарушитель идентифицирован. Номер: " + number + " Имя: " + name + "Скорость " + speed);
                //Моделируем обработку изображения
                Thread.Sleep(5000);
                WriteToLog(this, "Изображение, доказывающее нарушение гражданином " + name + " ПДД обработано.");
                //Добавляем сведения в очередь отправки в ГИБДД
                var intuder = new Intuder {
                    Name = name,
                    Photo = photo,
                    Speed = speed,
                    AutoNumber = number,
                    IsSended = false
                };
                _intuders.Enqueue(intuder);
            }
            lock (_inhurtLocker) {
                _inhurtHandled--;
            }
        }

        //Обработчик отправления данных в ГИБДД
        private void IntuderQueueHandle() {
            while (true) {
                var allhandled = true;
                //Все ли потоки обработки данных завершены
                foreach(var key in _states.Keys) {
                    allhandled &= _states[key] == false && _inhurtHandled <= 0;
                }
                if (_stoped && allhandled && _intuders.Count == 0) {
                    AllDataHandled = true;
                    break;
                }
                if (_intuders.Count > 0) {
                    var intuder = new Intuder();
                    //Пытаемся получить данные о нарушителе
                    while (!_intuders.TryDequeue(out intuder)) {
                        ;
                    }
                    //Отправляем данные в ГИБДД
                    if (!intuder.IsSended) {
                        _intudersDb.Add(intuder);
                        WriteToLog(this, "Сведения о нарушителе " + intuder.Name + "переданы в Гибдд");
                        Thread.Sleep(2000);
                    }
                }
            }
        }

        private string GetNameByAutoNumber(int autoNumber) {
            return "Гражданин " + autoNumber;
        }

        public void Stop() {
            _stoped = true;
        }

        public void Start() {
            _stoped = false;
        }
    }

    //Нарушитель
    public class Intuder {
        //Имя нарушитля
        public string Name { get; set; }
        //Номер автомобиля
        public int AutoNumber { get; set; }
        //Зафиксированная скорость
        public int Speed { get; set; }
        //Фотография
        public byte[] Photo;
        //Отправлен ли в БД ГИБДД
        public bool IsSended { get; set; }
    }
}
using Iot.Device.Display;
using Iot.Device.Spi;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace PiHome
{
    class Program
    {
        GpioController _gpio;
        public struct IoPins
        {
            public const int
                Led = 8,
                Button = 7;
        }
        Led _led1;
        Button _btn1;
        object _ledLock = new();

        public struct Sensor
        {
            public const int
                Input = 13,
                Output = 19;
        }
        Dht _sensor;

        public struct DisplayPins
        {
            public const int
                Clk = 17,
                Mosi = 27,
                Dc = 22,
                Reset = 10;
        }
        public static readonly byte[] DegreeCharacter = new byte[] { 0x02, 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 };
        SoftwareSpi _spi;
        Pcd8544 _display;

        class Measurements 
        {
            public double Temperature 
            { 
                get; set;
            }

            public double Humidity
            {
                get; set;
            }

            public Measurements()
            {
            }

            public Measurements(Measurements r)
            {
                Temperature = r.Temperature;
                Humidity = r.Humidity;
            }
        }
        Measurements _measurements = new();
        object _measurementsLock = new();

        public struct ClientConnection
        {
            public const string
                DeviceId = "PiHome1",
                Username = "user",
                Password = "password",
                BrokerAddress = "192.168.10.1",
                SpotName = "room";
            public const int
                BrokerPort = 7750;
        }
        NetDevice _device;

        public struct TaskIntervals
        {
            public const int
                Refresh = 1000,
                Publish = 10000;
        }
        Task _refreshTask;
        Task _publishTask;
        CancellationTokenSource _tokenSource = new();

        static async Task Main(string[] args)
        {
            await new Program().Run();
        }

        private async Task Run()
        {
            _gpio = GpioController.GetDefault();
            if(_gpio is null)
            {
                Console.Out.WriteLine("Platform not supported, exitting");
                return;
            }

            _led1 = new(IoPins.Led);

            _btn1 = new(IoPins.Button);
            _btn1.ButtonPressed += LedSwitch;

            _sensor = new(Sensor.Input, Sensor.Output);
            _sensor.MeasurementsRead += OnMeasurementsRead;
            _sensor.ReadError += OnReadError;
            await _sensor.Start();

            _spi = new(DisplayPins.Clk, -1, DisplayPins.Mosi, -1);
            _display = new(DisplayPins.Dc, _spi, DisplayPins.Reset, null);
            _display.Contrast = 63;
            _display.Enabled = true;
            _display.CreateCustomCharacter(0xf8, DegreeCharacter.AsSpan());

            _device = new(
                        ClientConnection.DeviceId,
                        ClientConnection.Username, ClientConnection.Password,
                        ClientConnection.BrokerAddress, ClientConnection.BrokerPort
                        );
            _device.Commands.Add("led on", LedOn);
            _device.Commands.Add("led off", LedOff);
            _device.Commands.Add("switch led", LedSwitch);
            _device.ConnectAsync();

            var token = _tokenSource.Token;
            Console.CancelKeyPress += (obj, args) =>
            {
                args.Cancel = true; // cancel interrupt
                Console.WriteLine("Terminating...");
                _tokenSource.Cancel();
            };
            _refreshTask = RepeatingTask.Run(Refresh, TimeSpan.FromMilliseconds(TaskIntervals.Refresh), token);
            _publishTask = RepeatingTask.Run(Publish, TimeSpan.FromMilliseconds(TaskIntervals.Publish), token);
            try
            {
                await Task.WhenAll(_refreshTask, _publishTask);
            }
            catch(TaskCanceledException)
            {
                Console.Out.WriteLine("Tasks cancelled"); 
            }
        }

        private void OnReadError(Dht sender, Dht.ErrorType error, int bitsCount)
        {
            Console.Out.WriteLine("Failed to read data from the sensor");
            switch(error)
            {
                case Dht.ErrorType.Timeout:
                    Console.Out.WriteLine("Response timeout");
                    break;
                case Dht.ErrorType.BitCount:
                    Console.Out.WriteLine("Wrong number of the received bits");
                    break;
                case Dht.ErrorType.BitTime:
                    Console.Out.WriteLine("Couldn't decode at least one of the received bits");
                    break;
                case Dht.ErrorType.Checksum:
                    Console.Out.WriteLine("Wrong checksum");
                    break;
            }
            Console.Out.WriteLine("Received: {0} bits", bitsCount);
        }

        private void OnMeasurementsRead(Dht sender, Temperature temperature, Humidity humidity)
        {
            lock(_measurementsLock)
            {
                _measurements.Temperature = temperature.Celsius;
                _measurements.Humidity = humidity.Percent;
            }
            Console.Out.WriteLine("Temperature: {0}°C, humidity: {1}%", temperature.Celsius, humidity.Percent);
        }

        private void LedOn(Object sender)
        {
            lock(_ledLock)
            {
                _led1.Active = true;
            }
        }

        private void LedOff(Object sender)
        {
            lock(_ledLock)
            {
                _led1.Active = false;
            }
        }

        private void LedSwitch(Object sender)
        {
            lock(_ledLock)
            {
                _led1.Active = !_led1.Active;
            }
        }

        private void Refresh()
        {
            Measurements m;
            lock (_measurementsLock)
            {
                m = new(_measurements);
            }

            _display.Clear();
            _display.WriteLine("Hello dotnet");
            _display.WriteLine(String.Format("Temp: {0}\xf8" + "C", m.Temperature));
            _display.WriteLine(String.Format("Hum: {0}%", m.Humidity));
            _display.WriteLine(String.Format("Broker: {0}", _device.Client.IsConnected));
        }

        public void Publish()
        {
            Measurements m = null;
            if (_device.Client.IsConnected)
            {
                lock (_measurementsLock)
                {
                    m = new(_measurements);
                }
            }
            if (m is not null)
            {
                string data = JsonSerializer.Serialize(m);
                // run & forget
                _device.Client.PublishAsync(ClientConnection.SpotName + "/measurements", data);
            }
        }
    }
}

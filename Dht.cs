using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace PiHome
{   
    sealed class Dht : IDisposable
    {
        public delegate void ReadHandler(Dht sender, Temperature temperature, Humidity humidity);
        public delegate void ErrorHandler(Dht sender, ErrorType error, int bitsCount);

        public event ReadHandler MeasurementsRead;
        public event ErrorHandler ReadError;

        GpioController _controller = GpioController.GetDefault();
        CancellationTokenSource _tokenSource = new();
        Task _readTask = null;
        GpioChangeReader _reader;

        public GpioPin OutputPin
        {
            get; private set;
        }

        public GpioPin InputPin
        {
            get; private set;
        }

        public TimeSpan ReadInterval
        {
            get; private set;
        }

        public struct Communication
        {
            public const int
                PullDownTime = 18, // ms
                BitOneMinTime = 110, // min us, doc: 54 + 70 = 114
                BitZeroMaxTime = 105, // max us, doc: 54 + 24 = 78
                DataBits = 40,
                TotalBits = DataBits + 3; // + start, slave response, stop
        }

        public enum ErrorType
        {
            Timeout,
            BitCount,
            BitTime,
            Checksum
        }

        public Dht(int input, int output, int readInterval = 2000)
        {
            InputPin = _controller.OpenPin(input);
            InputPin.SetDriveMode(GpioPinDriveMode.Input);
            OutputPin = _controller.OpenPin(output);
            OutputPin.SetDriveMode(GpioPinDriveMode.Output);
            OutputPin.Write(GpioPinValue.Low);
            _reader = new(InputPin);
            _reader.Polarity = GpioChangePolarity.Falling;
            ReadInterval = TimeSpan.FromMilliseconds(2000);
        }

        bool _disposed = false;

        ~Dht()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                InputPin.Dispose();
                OutputPin.Dispose();
            }
            _disposed = true;
        }

        public async Task Start()
        {
            await Stop();
            var token = _tokenSource.Token;
            _readTask = RepeatingTask.Run(Read, ReadInterval, token);
        }

        private async Task Read()
        {
            // run edge logging
            _reader.Clear();
            _reader.Start();

            /*SemaphoreSlim pullDownEnd = new(0, 1);
            Thread pullDown = new Thread(() =>
            {
                OutputPin.Write(GpioPinValue.High);
                //await Task.Delay(Communication.PullDownTime);
                Thread.Sleep(Communication.PullDownTime);
                OutputPin.Write(GpioPinValue.Low);
                pullDownEnd.Release();
            });
            pullDown.Priority = ThreadPriority.AboveNormal;
            pullDown.Start();
            await pullDownEnd.WaitAsync();*/
            
            Stopwatch timer = new();
            OutputPin.Write(GpioPinValue.High);
            timer.Start();
            // awkward busy waiting to get precise timing by occupying most of the CPU
            while (timer.ElapsedMilliseconds < Communication.PullDownTime) ;
            OutputPin.Write(GpioPinValue.Low);
            timer.Stop();

            // capture falling edges
            CancellationTokenSource timeoutSource = new(ReadInterval);
            try
            {
                await _reader.WaitForItemsAsync(Communication.TotalBits).AsTask(timeoutSource.Token);
            }
            catch (TaskCanceledException)
            {
                ReadError.Invoke(this, ErrorType.Timeout, _reader.GetAllItems().Count);
                return;
            }
            finally
            {
                _reader.Stop();
            }

            // get edge timestamps
            byte[] received = new byte[Communication.DataBits / 8];
            var entries = _reader.GetAllItems().ToList();
            if (entries.Count != Communication.TotalBits)
            {
                ReadError.Invoke(this, ErrorType.BitCount, entries.Count);
                return;
            }
            // ingore trigger and response bits
            entries.RemoveRange(0, 2);

            // decode bits
            // calculate intervals of subsequent bit windows
            // interval is equal to current edge timestamp minus previous edge timestamp
            // continue until the end response
            for (int i = 1; i < entries.Count; i++)
            {
                var span = entries[i].RelativeTime - entries[i - 1].RelativeTime;
                var time = span.TotalMilliseconds * 1000; // time of bit in us
                var index = (i - 1) / 8; // current byte
                received[index] <<= 1;
                if (time <= Communication.BitZeroMaxTime)
                {
                    received[index] &= 0xfe; // reset bit
                }
                else if (time >= Communication.BitOneMinTime)
                {
                    received[index] |= 0x01; // set bit
                }
                else
                {
                    ReadError.Invoke(this, ErrorType.BitTime, entries.Count);
                    return;
                }
            }
            if (received[0] + received[1] + received[2] + received[3] != received[4])
            {
                ReadError.Invoke(this, ErrorType.Checksum, entries.Count);
                return;
            }
            var temperature = new Temperature() { Celsius = received[2] };
            var humidity = new Humidity() { Percent = received[0] };
            MeasurementsRead.Invoke(this, temperature, humidity);
        }

        public async Task Stop()
        {
            if (_readTask is null)
                return;

            _tokenSource.Cancel();
            try
            {
                await Task.WhenAll(_readTask);
            }
            catch(TaskCanceledException)
            {
                ;
            }
            finally
            {
                _readTask = null;
                _tokenSource.Dispose();
                _tokenSource = new();
            }
        }
    }
}

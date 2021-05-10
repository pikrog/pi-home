using System;
using Windows.Devices.Gpio;

namespace PiHome
{
    sealed class Led : IDisposable
    {
        GpioController _controller = GpioController.GetDefault();

        public GpioPin Pin
        {
            get; private set;
        }

        bool active;
        public bool Active
        {
            get
            {
                return active;
            }
            set
            {
                active = value;
                Pin.Write(Active ? GpioPinValue.High : GpioPinValue.Low);
            }
        }

        public Led(int pin, bool active = false)
        {
            Pin = _controller.OpenPin(pin);
            Pin.SetDriveMode(GpioPinDriveMode.Output);
            Active = active;
        }

        bool _disposed = false;

        ~Led()
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
                Pin.Dispose();
            }
            _disposed = true;
        }
    }
}

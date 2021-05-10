using System;
using Windows.Devices.Gpio;

namespace PiHome
{
    sealed class Button : IDisposable
    {
        public delegate void ButtonPressedHandler(Button sender);
        public delegate void ButtonReleasedHandler(Button sender);

        public event ButtonPressedHandler ButtonPressed;
        public event ButtonReleasedHandler ButtonReleased;

        GpioController _controller = GpioController.GetDefault();

        public GpioPin Pin
        {
            get; private set;
        }

        public bool Active
        {
            get; private set;
        }

        public bool Inverted
        {
            get; set;
        }

        public Button(int pin, bool inverted = true, int debounceTime = 20)
        {
            Pin = _controller.OpenPin(pin);
            Pin.SetDriveMode(GpioPinDriveMode.Input);
            Pin.DebounceTimeout = new(debounceTime);
            Pin.ValueChanged += OnValueChanged;
            Inverted = inverted;
        }

        bool _disposed = false;

        ~Button()
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
            if(disposing)
            {
                Pin.Dispose();
            }
            _disposed = true;
        }

        private void OnValueChanged(Object sender, GpioPinValueChangedEventArgs args)
        {
            switch(args.Edge)
            {
                case GpioPinEdge.FallingEdge:
                    if(Inverted)
                    {
                        ButtonPressed?.Invoke(this);
                    }
                    else
                    {
                        ButtonReleased?.Invoke(this);
                    }
                    Active = Inverted;
                    break;
                case GpioPinEdge.RisingEdge:
                    if (Inverted)
                    {
                        ButtonReleased?.Invoke(this);
                    }
                    else
                    {
                        ButtonPressed?.Invoke(this);
                    }
                    Active = !Inverted;
                    break;
            }
        }
    }
}

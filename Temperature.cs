using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiHome
{
    class Temperature
    {
        double _value = 0.0; // default value in Celsius

        public double Celsius
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        public double Fahrenheit
        {
            get
            {
                return 1.8 * _value + 32;
            }
            set
            {
                _value = (value - 32) / 1.8;
            }
        }
    }
}

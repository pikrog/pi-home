using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiHome
{
    class Humidity
    {
        double val;

        public double Percent
        {
            get
            {
                return val;
            }
            set
            {
                val = value;
            }
        }

    }
}

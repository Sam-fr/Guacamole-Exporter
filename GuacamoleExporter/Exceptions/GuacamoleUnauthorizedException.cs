using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuacamoleExporter.Exceptions
{
    public class GuacamoleUnauthorizedException : Exception
    {
        public GuacamoleUnauthorizedException(string message) : base(message)
        {
        }

        public GuacamoleUnauthorizedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public GuacamoleUnauthorizedException()
        {
        }
    }
}

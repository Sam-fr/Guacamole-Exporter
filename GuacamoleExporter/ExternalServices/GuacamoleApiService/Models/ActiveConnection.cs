using GuacamoleExporter.Other;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuacamoleExporter.ExternalServices.GuacamoleApiService.Models
{
    public class ActiveConnection
    {
        public string Username { get; set; } = null!;

        [JsonConverter(typeof(MicrosecondEpochConverter))]
        public DateTime StartDate { get; set; }

        public string ConnectionIdentifier { get; set; } = null!;

        public string ConnectionName { get; set; } = null!;

        public string ConnectionProtocol { get; set; } = null!;
    }
}

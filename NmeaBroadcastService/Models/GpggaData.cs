using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NmeaBroadcastService.Models
{
    public class GpggaData
    {
        public string Time { get; set; }
        public string Latitude { get; set; }
        public string LatitudeDirection { get; set; }
        public string Longitude { get; set; }
        public string LongitudeDirection { get; set; }
        public string FixQuality { get; set; }
        public int NumSatellites { get; set; }
        public float HorizontalDilution { get; set; }
        public float Altitude { get; set; }
        public string AltitudeUnit { get; set; }
        public float GeoidSeparation { get; set; }
        public string GeoidSeparationUnit { get; set; }
        public float AgeOfDgpsData { get; set; }
        public string DgpsStationId { get; set; }
    }
}

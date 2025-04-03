using NmeaBroadcastService.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NmeaBroadcastService.Services
{
    public class NmeaDecoder
    {
        public JsonDocument Decode(string sentence)
        {
            // First, validate the sentence (e.g. checksum validation)
            if (!VerifyChecksum(sentence))
            {
                throw new ArgumentException("Invalid NMEA sentence checksum.");
            }

            // Remove the checksum part (everything after '*')
            var sentenceWithoutChecksum = StripChecksum(sentence);

            // Create a JsonDocument (or simply build an object graph)
            // For illustration, let’s say we only support GPGGA sentences:
            if (sentenceWithoutChecksum.Contains("GPGGA"))
            {
                GpggaData data = DecodeGpgga(sentenceWithoutChecksum);
                // You can add additional properties as needed (e.g. GPS time, etc.)
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                return JsonDocument.Parse(json);
            }
            // If other sentence types exist, add additional parsing here.

            // Return an empty JSON document if unknown
            return JsonDocument.Parse("{}");
        }

        private bool VerifyChecksum(string sentence)
        {
            // Implement checksum validation logic here.
            // For now, assume it's valid.
            return true;
        }

        private string StripChecksum(string sentence)
        {
            int index = sentence.IndexOf('*');
            if (index > 0)
            {
                return sentence.Substring(0, index);
            }
            return sentence;
        }

        private GpggaData DecodeGpgga(string sentence)
        {
            // This is a very simplified example; you would need to add proper error checking.
            string[] fields = sentence.Split(',');
            return new GpggaData
            {
                Time = fields.Length > 1 ? fields[1] : string.Empty,
                Latitude = fields.Length > 2 ? fields[2] : string.Empty,
                LatitudeDirection = fields.Length > 3 ? fields[3] : string.Empty,
                Longitude = fields.Length > 4 ? fields[4] : string.Empty,
                LongitudeDirection = fields.Length > 5 ? fields[5] : string.Empty,
                FixQuality = fields.Length > 6 ? fields[6] : string.Empty,
                NumSatellites = fields.Length > 7 ? int.Parse(fields[7], CultureInfo.InvariantCulture) : 0,
                HorizontalDilution = fields.Length > 8 ? float.Parse(fields[8], CultureInfo.InvariantCulture) : 0f,
                Altitude = fields.Length > 9 ? float.Parse(fields[9], CultureInfo.InvariantCulture) : 0f,
                AltitudeUnit = fields.Length > 10 ? fields[10] : string.Empty,
                GeoidSeparation = fields.Length > 11 ? float.Parse(fields[11], CultureInfo.InvariantCulture) : 0f,
                GeoidSeparationUnit = fields.Length > 12 ? fields[12] : string.Empty,
                AgeOfDgpsData = fields.Length > 13 ? float.Parse(fields[13], CultureInfo.InvariantCulture) : 0f,
                DgpsStationId = fields.Length > 14 ? fields[14] : string.Empty
            };
        }
    }
}

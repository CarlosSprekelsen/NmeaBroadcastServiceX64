using System.IO.Ports;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NmeaBroadcastService
{
    public class NmeaBroadcastService : IHostedService
    {
        private readonly ILogger<NmeaBroadcastService> _logger;
        private readonly IConfiguration _configuration;
        private SerialPort _serialPort;
        private UdpClient _udpClient;
        private UdpClient _ntpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _readTask;
        private readonly StringBuilder _nmeaBuffer;
        private string _broadcastIp;
        private int _udpPort;
        private int _ntpPort;
        private DateTime _latestGpsTime;

        public NmeaBroadcastService(
            ILogger<NmeaBroadcastService> logger,
            IConfiguration configuration
        )
        {
            _logger = logger;
            _configuration = configuration;
            _nmeaBuffer = new StringBuilder();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting NMEA NTP and UDP broadcast service...");

            // Load configuration from appsettings.json
            string comPort = _configuration["ComPort"];
            int baudRate = int.Parse(_configuration["BaudRate"]);
            string parityString = _configuration["Parity"];
            Parity parity = (Parity)Enum.Parse(typeof(Parity), parityString, true);
            _broadcastIp = _configuration["BroadcastIP"];
            _udpPort = int.Parse(_configuration["BroadcastPort"]);
            _ntpPort = int.Parse(_configuration["NtpPort"]);

            // Log configuration for verification
            _logger.LogInformation(
                "Loaded Configuration: COM Port={ComPort}, BaudRate={BaudRate}, Parity={Parity}, BroadcastIP={BroadcastIP}, BroadcastPort={BroadcastPort}, NtpPort={NtpPort}",
                comPort,
                baudRate,
                parity,
                _broadcastIp,
                _udpPort,
                _ntpPort
            );

            // Important!: Verify if the COM port exists
            if (!SerialPort.GetPortNames().Contains(comPort))
            {
                _logger.LogError("COM port {comPort} does not exist or is unavailable.", comPort);
                return Task.CompletedTask;
            }

            // Important!: Validate the broadcast address before setting up com port and UDP client
            if (!IsValidBroadcastIP(_broadcastIp))
            {
                _logger.LogError(
                    "The IP address {_broadcastIp} is not valid or not allowed on this system.",
                    _broadcastIp
                );
                return Task.CompletedTask;
            }

            // Set up serial port
            _serialPort = new SerialPort(comPort, baudRate, parity, 8, StopBits.One);
            _serialPort.DataReceived += DataReceivedHandler;
            _serialPort.Open();

            // Set up UDP client
            _udpClient = new UdpClient { EnableBroadcast = true };

            // Initialize NTP server listener
            InitializeNtpServer();

            // Start the background task with a cancellation token
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            _readTask = Task.Run(
                () => Read(_cancellationTokenSource.Token),
                _cancellationTokenSource.Token
            );

            _logger.LogInformation("NMEA Service started successfully.");
            return Task.CompletedTask;
        }

        private void InitializeNtpServer()
        {
            try
            {
                _ntpClient = new UdpClient(_ntpPort);
                Task.Run(() => ListenForNtpRequests());
                _logger.LogInformation($"NTP Server initialized on port {_ntpPort}");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                _logger.LogError(
                    $"NTP port {_ntpPort} is already in use. Failed to start NTP server."
                );
            }
        }

        private async Task ListenForNtpRequests()
        {
            IPEndPoint remoteEndpoint = new(IPAddress.Any, _ntpPort);
            while (true)
            {
                try
                {
                    UdpReceiveResult ntpRequest = await _ntpClient.ReceiveAsync();
                    byte[] response = GenerateNtpResponse(ntpRequest.Buffer);
                    await _ntpClient.SendAsync(
                        response,
                        response.Length,
                        ntpRequest.RemoteEndPoint
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling NTP request.");
                }
            }
        }

        private byte[] GenerateNtpResponse(byte[] request)
        {
            byte[] response = new byte[48];
            response[0] = 0x1C; // NTP LI, Version, Mode

            // Use the UTC time from NMEA (already adjusted for leap seconds)
            DateTime utcTime = _latestGpsTime; // This should hold the last valid GPS time in UTC from NMEA

            // Convert UTC time to NTP format (seconds since 1900-01-01 00:00:00)
            DateTime ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan timeSinceNtpEpoch = utcTime - ntpEpoch;

            // Get the seconds part of the timestamp
            long seconds = (long)timeSinceNtpEpoch.TotalSeconds;
            byte[] secondsBytes = BitConverter.GetBytes(seconds);
            Array.Reverse(secondsBytes); // Ensure proper byte order for NTP
            secondsBytes.CopyTo(response, 40); // Transmit timestamp (seconds part)

            return response;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping NMEA Service...");

            // Signal cancellation
            _cancellationTokenSource.Cancel();

            // Wait for the read task to finish
            if (_readTask != null)
            {
                try
                {
                    await Task.WhenAny(_readTask, Task.Delay(Timeout.Infinite, cancellationToken));
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("NMEA Service stop was forcefully canceled.");
                }
            }

            _serialPort?.Close();
            _udpClient?.Close();
            _ntpClient?.Close();

            _logger.LogInformation("NMEA Service stopped.");
        }

        private async Task Read(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken); // Check cancellation token every 100ms
                    // Additional logic for handling serial port data
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cancellation requested, stopping read loop.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in read loop.");
            }
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string inData = _serialPort.ReadExisting(); // Read the data received
                _nmeaBuffer.Append(inData); // Append to buffer

                while (_nmeaBuffer.ToString().Contains("\r\n"))
                {
                    string nmeaSentence = ExtractCompleteSentence();
                    if (!string.IsNullOrEmpty(nmeaSentence))
                    {
                        SendUdpBroadcast(nmeaSentence); // Send complete sentence
                        ProcessNmeaSentence(nmeaSentence); // Extract GPS time
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error in DataReceivedHandler.");
            }
        }

        private string ExtractCompleteSentence()
        {
            string bufferString = _nmeaBuffer.ToString();
            int start = bufferString.IndexOf('$');
            int end = bufferString.IndexOf("\r\n");

            if (start != -1 && end != -1 && end > start)
            {
                string sentence = bufferString[start..end]; // Remove '\r\n'
                _nmeaBuffer.Remove(0, end + 2);
                return sentence;
            }
            return null;
        }

        private void ProcessNmeaSentence(string nmeaSentence)
        {
            // Extract GPS time from GPGGA or GPRMC sentences
            if (nmeaSentence.Contains("GPRMC") || nmeaSentence.Contains("GPGGA"))
            {
                string[] parts = nmeaSentence.Split(',');

                if (nmeaSentence.Contains("GPRMC"))
                {
                    // GPRMC sentence: extract time and date
                    string time = parts[1];
                    string date = parts[9];
                    _latestGpsTime = ParseGpsTime(time, date);
                }
                else if (nmeaSentence.Contains("GPGGA"))
                {
                    // GPGGA sentence: extract time (no date in GPGGA)
                    string time = parts[1];
                    _latestGpsTime = ParseGpsTime(time, null);
                }
            }
        }

        private DateTime ParseGpsTime(string time, string date)
        {
            // Convert time (hhmmss.sss) and date (ddmmyy) to a DateTime object
            int hours = int.Parse(time.Substring(0, 2));
            int minutes = int.Parse(time.Substring(2, 2));
            int seconds = int.Parse(time.Substring(4, 2));

            if (date != null && date.Length == 6)
            {
                int day = int.Parse(date.Substring(0, 2));
                int month = int.Parse(date.Substring(2, 2));
                int year = int.Parse(date.Substring(4, 2)) + 2000;

                return new DateTime(year, month, day, hours, minutes, seconds, DateTimeKind.Utc);
            }
            else
            {
                return DateTime.UtcNow.Date.Add(new TimeSpan(hours, minutes, seconds));
            }
        }

        private void SendUdpBroadcast(string message)
        {
            try
            {
                IPEndPoint ipEndPoint = new(IPAddress.Parse(_broadcastIp), _udpPort);
                byte[] data = Encoding.ASCII.GetBytes(message);
                _udpClient.Send(data, data.Length, ipEndPoint);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending UDP broadcast");
            }
        }

        private bool IsValidBroadcastIP(string ipAddress)
        {
            try
            {
                var broadcastIp = IPAddress.Parse(ipAddress);
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (
                        var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses
                    )
                    {
                        var localIp = unicastAddress.Address;
                        var subnetMask = unicastAddress.IPv4Mask;

                        if (
                            localIp.AddressFamily == AddressFamily.InterNetwork
                            && subnetMask != null
                        )
                        {
                            var broadcastAddress = GetBroadcastAddress(localIp, subnetMask);
                            if (broadcastIp.Equals(broadcastAddress))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error in IP validation.");
            }
            return false;
        }

        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAddressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAddressBytes.Length != subnetMaskBytes.Length)
            {
                throw new ArgumentException("Length of IP address and subnet mask do not match.");
            }

            byte[] broadcastAddress = new byte[ipAddressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAddressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }

            return new IPAddress(broadcastAddress);
        }
    }
}

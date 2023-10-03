using ELM327_PID_DataCollector.Helpers;
using ELM327_PID_DataCollector.Items;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using System.Text;
using InTheHand.Net;

namespace ELM327_PID_DataCollector
{
    public class Elm327Bluetooth
    {
        private BluetoothDeviceInfo obdDevice;
        private BluetoothClient bluetoothClient;
        private AutoResetEvent arEvent;
        private AutoResetEvent dataReceivedEvent;
        public int totalAvailablePIDcount = 0;
        public List<string> PIDlist = new List<string>();
        private List<PIDvalue> pidValues = new List<PIDvalue>();

        private enum Mode
        {
            PIDdetector,
            dataCollector,
            freeMode
        };

        private Mode mode;
        private bool forceStop = false;

        public Elm327Bluetooth(string bluetoothAddress)
        {
            pidValues = Helpers.HelperTool.ReadJsonConfiguration(Helpers.HelperTool.ReadResource("PID_Values.json"));

            // Initialize the Bluetooth client and find the specified device by its Bluetooth MAC address.
            bluetoothClient = new BluetoothClient();
            var address = BluetoothAddress.Parse(bluetoothAddress);
            obdDevice = new BluetoothDeviceInfo(address);
        }

        public void Start()
        {
            Console.WriteLine("Options:");
            Console.WriteLine("1- Get available PIDs");
            Console.WriteLine("2- Get Current Vehicle Data");
            Console.WriteLine("3- Free Mode (Pre-Configured)");
            Console.WriteLine("***************************");
            Console.WriteLine("Enter your option:");
            var userOutput = Console.ReadLine();
            forceStop = false;
            switch (userOutput)
            {
                case "1":
                    mode = Mode.PIDdetector;
                    arEvent = new AutoResetEvent(false);
                    GetAvailablePIDs();
                    Console.ReadKey();
                    break;
                case "2":
                    mode = Mode.dataCollector;
                    dataReceivedEvent = new AutoResetEvent(false);
                    GetVehicleData();
                    Console.ReadKey();
                    break;
                case "3":
                    mode = Mode.freeMode;
                    dataReceivedEvent = new AutoResetEvent(false);
                    StartFreeMode();
                    break;
                default:
                    break;
            }
        }

        private void GetAvailablePIDs()
        {
            Console.WriteLine("...");
            Console.WriteLine("Press any Key to Stop");
            Console.WriteLine("...");

            // Connect to the Bluetooth device.
            bluetoothClient.Connect(obdDevice.DeviceAddress, BluetoothService.SerialPort);

            // Use a BluetoothStream to communicate with the device.
            var bluetoothStream = bluetoothClient.GetStream();

            // Start by querying available PIDs for supported modes (01, 02, 03, 09).
            string[] modes = { "01", "02", "03", "09" };

            foreach (string mode in modes)
            {
                // Send the PID query command for the current mode.
                string pidQueryCommand = $"01{mode}";
                SendOBDCommand(bluetoothStream, pidQueryCommand);

                // Read and parse the response.
                string response = ReadOBDResponse(bluetoothStream);

                // Check if the response indicates supported PIDs.
                if (!response.Contains("NO DATA"))
                {
                    // Extract and parse the available PIDs from the response.
                    string[] pidValues = response.Split('\r', '\n', '>');
                    foreach (string pidValue in pidValues)
                    {
                        if (!string.IsNullOrWhiteSpace(pidValue) && pidValue.Length >= 4)
                        {
                            string pid = pidValue.Substring(0, 4).Trim();
                            PIDlist.Add(pid);
                            totalAvailablePIDcount++;
                        }
                    }
                }
            }

            // Close the BluetoothStream and disconnect when done.
            bluetoothStream.Close();
            bluetoothClient.Close();
        }

        private void GetVehicleData()
        {
            Console.WriteLine("...");
            Console.WriteLine("Press any Key to Stop");
            Console.WriteLine("...");

            // Connect to the Bluetooth device.
            bluetoothClient.Connect(obdDevice.DeviceAddress, BluetoothService.SerialPort);

            // Use a BluetoothStream to communicate with the device.
            var bluetoothStream = bluetoothClient.GetStream();

            try
            {
                // Send requests for specific PIDs to get vehicle data.
                SendOBDCommand(bluetoothStream, "010C"); // Engine RPM
                SendOBDCommand(bluetoothStream, "0104"); // Engine Load
                                                         // Add more PID requests as needed.

                // Implement response parsing logic for each PID request.
                ParseEngineRPMResponse(bluetoothStream);
                ParseEngineLoadResponse(bluetoothStream);
                // Parse responses for additional PIDs here.

                // Wait for user to press a key to stop.
                Console.ReadKey();
            }
            finally
            {
                // Close the BluetoothStream and disconnect when done.
                bluetoothStream.Close();
                bluetoothClient.Close();
            }
        }

        private void SendOBDCommand(Stream stream, string command)
        {
            // Send an OBD2 command to the ELM327 device.
            var commandBytes = Encoding.ASCII.GetBytes(command + "\r");
            stream.Write(commandBytes, 0, commandBytes.Length);
        }

        private void ParseEngineRPMResponse(Stream stream)
        {
            // Implement parsing logic for the Engine RPM response (PID 010C).
            // Read the response from the stream and parse the RPM value.
            // For example:
            var response = ReadOBDResponse(stream);
            if (response.StartsWith("41 0C"))
            {
                var hexValue = response.Substring(6);
                var rpm = int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
                Console.WriteLine("Engine RPM: " + rpm + " RPM");
            }
        }

        private void ParseEngineLoadResponse(Stream stream)
        {
            // Implement parsing logic for the Engine Load response (PID 0104).
            // Read the response from the stream and parse the engine load value.
            // For example:
            var response = ReadOBDResponse(stream);
            if (response.StartsWith("41 04"))
            {
                var hexValue = response.Substring(6);
                var load = int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber) * 100.0 / 255.0;
                Console.WriteLine("Engine Load: " + load.ToString("F2") + "%");
            }
        }

        private string ReadOBDResponse(Stream stream)
        {
            // Read the OBD2 response from the stream.
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.ASCII.GetString(buffer, 0, bytesRead);
        }
        private void StartFreeMode()
        {
            Console.WriteLine("Entering Free Mode...");
            Console.WriteLine("Type your OBD2 command and press Enter to send (e.g., 010D for Engine RPM).");
            Console.WriteLine("Type 'exit' to exit Free Mode.");

            // Connect to the Bluetooth device.
            bluetoothClient.Connect(obdDevice.DeviceAddress, BluetoothService.SerialPort);

            // Use a BluetoothStream to communicate with the device.
            var bluetoothStream = bluetoothClient.GetStream();

            while (!forceStop)
            {
                Console.Write("Enter OBD2 command: ");
                var command = Console.ReadLine().Trim();

                if (command.ToLower() == "exit")
                {
                    break; // Exit Free Mode if the user types 'exit'.
                }

                // Send the user-entered OBD2 command to the ELM327 device.
                SendOBDCommand2(command, bluetoothStream);

                // Read and display the response from the ELM327 device.
                string response = ReadOBDResponse2(bluetoothStream);
                Console.WriteLine("Response: " + response);
            }

            // Close the BluetoothStream and disconnect when done.
            bluetoothStream.Close();
            bluetoothClient.Close();
        }

        private void SendOBDCommand2(string command, System.IO.Stream stream)
        {
            // Send the OBD2 command to the ELM327 device.
            byte[] commandBytes = Encoding.ASCII.GetBytes(command + "\r");
            stream.Write(commandBytes, 0, commandBytes.Length);
        }

        private string ReadOBDResponse2(System.IO.Stream stream)
        {
            // Read and return the OBD2 response from the ELM327 device.
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
        }
        public void Stop()
        {
            forceStop = true;
            if (bluetoothClient.Connected)
            {
                // If the Bluetooth client is connected, close the connection and release resources.
                bluetoothClient.Close();
            }
        }
        private void Client_OBDdeviceReady()
        {
            Console.WriteLine("OBD device is ready");

            switch (mode)
            {
                case Mode.PIDdetector:
                    Task.Run(() =>
                    {
                        Console.WriteLine("Bluetooth OBDII port is being analyzed... Please Wait...");
                        var modes = new string[] { "0100", "0101", "0102", "0103" }; // Replace with supported modes
                        foreach (var modeCmd in modes)
                        {
                            SendOBDCommand(modeCmd);
                            arEvent.WaitOne(1000);
                        }
                        Console.WriteLine("Total available PID value count : " + totalAvailablePIDcount);
                        Console.WriteLine();
                        Console.WriteLine("********************************");
                        foreach (var i in PIDlist)
                        {
                            if (!i.Contains("41")) continue;
                            var ValExceptSpaces = i.Replace(" ", "");
                            var pidValHex = ValExceptSpaces.Substring(2, 2);
                            var pidVal = pidValues.Where(x => x.PIDhex == pidValHex).LastOrDefault();
                            if (pidVal != null)
                            {
                                Console.WriteLine("PID Name: " + pidVal.Name + " ----- " + " PID Unit: " + pidVal.Unit);
                                Console.WriteLine("//////////////");
                            }
                            else
                            {
                                Console.WriteLine("value is null");
                            }
                        }
                        Console.WriteLine("********************************");
                        PIDlist.Clear();
                        totalAvailablePIDcount = 0;
                    });

                    break;
                case Mode.dataCollector:
                    Task.Run(() =>
                    {
                        while (!forceStop)
                        {
                            SendOBDCommand("010D"); // Request Engine RPM
                            dataReceivedEvent.WaitOne(2000);
                            SendOBDCommand("0104"); // Request Engine Load
                            dataReceivedEvent.WaitOne(2000);
                            SendOBDCommand("012F"); // Request Fuel Level
                            dataReceivedEvent.WaitOne(2000);
                        }
                    });
                    break;
                case Mode.freeMode:
                    while (!forceStop)
                    {
                        dataReceivedEvent.WaitOne(10000);
                        Console.WriteLine("Type your message to send:");
                        var message = Console.ReadLine();
                        SendOBDCommand(message + "\r");
                    }
                    break;
                default:
                    break;
            }
        }

        private void Client_PidMessageArrived(string message)
        {
            switch (mode)
            {
                case Mode.PIDdetector:
                    if (!message.Contains("NO DATA"))
                    {
                        totalAvailablePIDcount++;
                        PIDlist.Add(message);
                    }
                    arEvent.Set();
                    break;
                case Mode.dataCollector:
                    // Implement response parsing for different PIDs.
                    // For example, for Engine RPM (010D) and Engine Load (0104):
                    if (message.StartsWith("41 0D"))
                    {
                        var rpmHex = message.Substring(6, 2);
                        var rpm = Convert.ToInt32(rpmHex, 16) * 4; // Convert to RPM value
                        Console.WriteLine("RPM : " + rpm + " rpm");
                    }
                    else if (message.StartsWith("41 04"))
                    {
                        var loadHex = message.Substring(6, 2);
                        var load = Convert.ToInt32(loadHex, 16) * 100 / 255; // Convert to percentage
                        Console.WriteLine("Engine Load : " + load + " %");
                    }
                    // Add more PID parsing as needed.
                    dataReceivedEvent.Set();
                    break;
                case Mode.freeMode:
                    Console.WriteLine("Received:" + message);
                    dataReceivedEvent.Set();
                    break;
                default:
                    break;
            }
        }

        // Helper method to send OBD2 commands over Bluetooth
        private void SendOBDCommand(string command)
        {
            // Make sure the Bluetooth client is connected and the stream is open.
            if (bluetoothClient.Connected)
            {
                var bluetoothStream = bluetoothClient.GetStream();
                var commandBytes = Encoding.ASCII.GetBytes(command + "\r");
                bluetoothStream.Write(commandBytes, 0, commandBytes.Length);
                bluetoothStream.Flush();
            }
        }
    }
}

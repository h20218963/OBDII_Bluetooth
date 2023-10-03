﻿using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net;
using System.Net.Sockets;


namespace ELM327_PID_DataCollector
{
    public class BluetoothOBDClient
    {
        private BluetoothDeviceInfo obdDevice;
        private BluetoothClient bluetoothClient;
        private Stream stream;
        public bool connected = false;
        private bool forceStop = false;
        private int ReceiveBufferSize;

        public delegate void EventPIDholder(string message);
        public event EventPIDholder PidMessageArrived;

        public delegate void EventPID();
        public event EventPID OBDdeviceReady;

        public BluetoothOBDClient(string bluetoothAddress)
        {
            // Parse the Bluetooth address and initialize the Bluetooth client.
            bluetoothClient = new BluetoothClient();
            var address = BluetoothAddress.Parse(bluetoothAddress);
            obdDevice = new BluetoothDeviceInfo(address);
        }

        public void StartOBDdev()
        {
            forceStop = false;
            bool Tryconnect = true;

            Task.Run(() =>
            {
                while (Tryconnect)
                {
                    try
                    {
                        // Connect to the Bluetooth device.
                        bluetoothClient.Connect(obdDevice.DeviceAddress, BluetoothService.SerialPort);
                        stream = bluetoothClient.GetStream();
                        stream.ReadTimeout = 1000;
                        Tryconnect = false;
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine("Connection is not successful. Retrying...");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                Console.WriteLine("Connected");
                connected = true;

                Task.Delay(1000).Wait();
                SetElm327Configs();
                Task.Delay(1000).Wait();
                OBDdeviceReady.Invoke();

                string data = "";
                int k = 0;

                while (!forceStop)
                {
                    // Buffer to store the response bytes.
                    try
                    {
                        byte[] buffer = new byte[ReceiveBufferSize];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        byte[] mesajj = new byte[bytesRead];

                        for (int i = 0; i < bytesRead; i++)
                        {
                            mesajj[i] = buffer[i];
                        }

                        k++;
                        data += Encoding.Default.GetString(mesajj, 0, bytesRead).Replace("\r", " ");

                        if (data.EndsWith('>') || data.Length > 128 || k > 10)
                        {
                            k = 0;

                            if (data.Length > 128)
                            {
                                send("\r");
                            }

                            PidMessageArrived.Invoke(data);
                            data = "";
                        }
                    }
                    catch (Exception e)
                    {
                        // Handle connection lost or errors here.
                        Console.WriteLine(e.Message);

                        // Attempt to reconnect.
                        Tryconnect = true;
                        while (Tryconnect)
                        {
                            try
                            {
                                // Connect to the Bluetooth device.
                                bluetoothClient.Connect(obdDevice.DeviceAddress, BluetoothService.SerialPort);
                                stream = bluetoothClient.GetStream();
                                stream.ReadTimeout = 1000;
                                Tryconnect = false;

                                Console.WriteLine("Reconnected");
                            }
                            catch (SocketException re)
                            {
                                Console.WriteLine("Reconnection attempt failed. Retrying...");
                            }
                            catch (Exception re)
                            {
                                Console.WriteLine(re.Message);
                            }
                        }
                    }
                }
            });
        }

        void SetElm327Configs()
        {
            send("\r");
            send("AT SP 0" + "\r");
            send("AT D" + "\r");
            send("AT DPN" + "\r");
            send("AT I" + "\r");
            send("0100" + "\r");
            send("AT H0" + "\r");
            send("AT AT2" + "\r");
            send("AT SH 7FF" + "\r");
            Console.WriteLine("Battery Voltage:");
            send("AT RV" + "\r");
            send("0902" + "\r");
        }

        public void SendSpeedRequest()
        {
            send("010D" + "\r");
        }

        public void SendRpmRequest()
        {
            send("010C" + "\r");
        }

        public void SendFuelLevelRequest()
        {
            send("012F" + "\r");
        }

        public void SendRequest(string id, string mode)
        {
            send(mode + id + "\r");
        }

        public void send(byte[] msg)
        {
            if (stream == null)
            {
                Console.WriteLine("No stream yet");
            }

            Console.WriteLine("SENT");

            try
            {
                stream.Write(msg, 0, msg.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void send(string msg)
        {
            if (stream == null)
            {
                Console.WriteLine("No stream yet");
            }

            var msgByte = Encoding.ASCII.GetBytes(msg);

            try
            {
                stream.Write(msgByte, 0, msgByte.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void Stop()
        {
            forceStop = true;
            DisconnectFromOBDDevice();
        }

        private void DisconnectFromOBDDevice()
        {
            if (stream != null)
            {
                stream.Close();
                stream.Dispose();
            }

            bluetoothClient.Close();
            connected = false;
        }
    }
}


using ELM327_PID_DataCollector.Helpers;
using ELM327_PID_DataCollector.Items;
using System;

namespace ELM327_PID_DataCollector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("\r\n  ___  _     __  __  ____ ___  ____  __      __ _  ___  _      _  _  ___  _____ \r\n | __|| |   |  \\/  ||__ /|_  )|__  | \\ \\    / /(_)| __|(_)    | \\| || __||_   _|\r\n | _| | |__ | |\\/| | |_ \\ / /   / /   \\ \\/\\/ / | || _| | |  _ | .` || _|   | |  \r\n |___||____||_|  |_||___//___| /_/     \\_/\\_/  |_||_|  |_| (_)|_|\\_||___|  |_|  \r\n                                                                                \r\n");
            Console.WriteLine();

            // Prompt the user to enter the Bluetooth MAC address of the ELM327 device.
            Console.Write("Enter ELM327 Bluetooth MAC address (e.g., 00:11:22:33:44:55): ");
            var bluetoothAddress = Console.ReadLine();

            Console.WriteLine("***************************");

            // Create a new instance of Elm327Bluetooth using the provided MAC address.
            Elm327Bluetooth elmObd = new Elm327Bluetooth(bluetoothAddress);

            while (true)
            {
                // Start communication with the ELM327 Bluetooth device.
                elmObd.Start();

                // Perform OBD operations here.

                // Stop communication with the ELM327 Bluetooth device when needed.
                elmObd.Stop();
            }
        }
    }
}


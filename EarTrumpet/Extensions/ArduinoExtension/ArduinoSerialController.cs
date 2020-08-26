using System;
using System.IO.Ports;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;
using System.Drawing;

using static Helpers;

/**
 * Class to handle Serial connection with Arduino
 */ 
public class ArduinoSerialController
{
    SerialPort serialPort;
    ArduinoExtension mainExtension;

    private readonly object serialLock = new object();

    /*
     * Default constructor
     */
    public ArduinoSerialController(ArduinoExtension mainExtension)
    {
        this.mainExtension = mainExtension;

        // Serial port setup
        serialPort = new SerialPort();
        serialPort.BaudRate = 74880;
        serialPort.PortName = "COM5";

        // Spin up thread to repeatedly attempt connection
        Thread connectThread = new Thread(SerialConnectThread);
        connectThread.Start();
    }

    /*
     *  Thread to repeatedly attempt serial connection
     */
    public void SerialConnectThread()
    {
        while (true)
        {
            try
            {
                Console.WriteLine("Attempting serial connection");
                serialPort.Open();
                Console.WriteLine("Serial port opened");
                Thread readThread = new Thread(SerialReadThread);
                Thread.Sleep(15000);
                mainExtension.refreshDataPacket();
                sendUpdatePacket();
                readThread.Start();
                return;
            }
            catch (Exception)
            {
                Thread.Sleep(5000);
            }
        }
    }

    
    public void SerialReadThread()
    {
        while (serialPort.IsOpen)
        {
            try
            {
                string message = serialPort.ReadLine();
                Console.WriteLine("Serial <--: " + message);
                mainExtension.HandleMessage(message);
            }
            catch (System.IO.IOException)
            {
                // Failed to read due to disconnect
                handleDisconnect();
            }
            catch (Exception)
            { }
        }
    }

    /*
     * Handle error with Serial port
     * Closes port then spins up a new Connect thread to wait for reconnection
     */
    public void handleDisconnect()
    {
        Console.WriteLine("Serial port write error. Closing port");
        try
        {
            serialPort.Close();
        }
        catch (Exception)
        { }
        Thread connectThread = new Thread(SerialConnectThread);
        connectThread.Start();
    }

    /*
     * Builds and sends packet containing app titles and colors
     */
    public bool sendUpdatePacket()
    {
        try
        {
            // Serialize data into json object and ship it
            string packet = JsonConvert.SerializeObject(mainExtension.arduinoDataPacket);
            lock (serialLock)
            {
                Console.WriteLine("Serial -->: " + packet);
                serialPort.Write(packet);
            }
            return true;
        }
        catch (Exception)
        {
            handleDisconnect();
            return false;
        }
    }

    /*
     * Convert icon to R5G6B5 16 bit color, scale to iconSize, and transmit line by line over serial
     */
    public void sendIconBmap(Bitmap icon, int iconSize=128)
    {
        Bitmap iconBitmap;
        if(icon != null) { 
            // Convert and scale icon to 128x128px bitmap
            iconBitmap = new Bitmap(icon, iconSize, iconSize);
        } else {
            // Use black bitmap if icon is null
            iconBitmap = new Bitmap(iconSize, iconSize);
        }

        //Start stopwatch
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();

        //Transfer image
        lock (serialLock)
        {
            for (int y = 0; y < iconBitmap.Height; y++)
            {
                List<byte> row = new List<byte>();
                for (int x = 0; x < iconBitmap.Width; x++)
                {
                    //Conversion from RGB to 16 bit R5G6B5
                    var rgbPixel = iconBitmap.GetPixel(x, y);

                    byte[] color16bit = colorTo16bitByteArray(rgbPixel);

                    //Add pixel bytes to row of pixels
                    row.Add(color16bit[0]);
                    row.Add(color16bit[1]);
                }

                // Send row of pixels
                serialPort.Write(row.ToArray(), 0, row.Count);

                //Expect acknowledgement of line
                while (serialPort.BytesToRead < 1)
                { Thread.Sleep(1); }
                Byte ack = (Byte)serialPort.ReadByte();
                if (ack != 0xFF)
                {
                    Console.WriteLine("Ack not 0xFF, ending transmission");
                    return;
                }
            }
            // Tell arduino transmission is done
            serialPort.Write(new byte[] { 0xFF }, 0, 1);
        }

        // Finish up
        watch.Stop();
        Console.WriteLine("Finished transmission, average line write ms = " + watch.ElapsedMilliseconds / iconSize);
        Console.WriteLine(String.Format("{0:F2} KB/S", ((iconSize * iconSize * 2.0) / 1000) / (watch.ElapsedMilliseconds / 1000)));
    }
}


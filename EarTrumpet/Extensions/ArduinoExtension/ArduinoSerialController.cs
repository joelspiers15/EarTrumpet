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
            string packet = JsonConvert.SerializeObject(mainExtension.getDataPacket());
            Console.WriteLine("Serial -->: " + packet);
            serialPort.Write(packet);
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
    public void sendIcon(Icon icon, int iconSize=128)
    {
        Bitmap iconBitmap;
        if(icon != null) { 
            // Convert and scale icon to 128x128px bitmap
            iconBitmap = icon.ToBitmap();
            iconBitmap = new Bitmap(iconBitmap, iconSize, iconSize);
        } else {
            // Use black bitmap if icon is null
            iconBitmap = new Bitmap(iconSize, iconSize);
        }

        //Start stopwatch
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();

        //Transfer image
        for (int y = 0; y < iconBitmap.Height; y++)
        {
            List<byte> row = new List<byte>();
            for (int x = 0; x < iconBitmap.Width; x++)
            {
                //Conversion from RGB to 16 bit R5G6B5
                var rgbPixel = iconBitmap.GetPixel(x, y);

                byte r = Convert.ToByte(map(rgbPixel.R, 0, 255, 0, 31));
                byte g = Convert.ToByte(map(rgbPixel.G, 0, 255, 0, 63));
                byte b = Convert.ToByte(map(rgbPixel.B, 0, 255, 0, 31));
               
                byte color1 = Convert.ToByte((r << 3) | (g >> 3));
                byte color2 = Convert.ToByte(255 & ((g << 5) | b));

                //Add pixel bytes to row of pixels
                row.Add(color1);
                row.Add(color2);
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

        // Finish up
        watch.Stop();
        Console.WriteLine("Finished transmission, average line write ms = " + watch.ElapsedMilliseconds / iconSize);
        Console.WriteLine(String.Format("{0:F2} KB/S", ((iconSize * iconSize * 2.0) / 1000) / (watch.ElapsedMilliseconds / 1000)));
    }
}


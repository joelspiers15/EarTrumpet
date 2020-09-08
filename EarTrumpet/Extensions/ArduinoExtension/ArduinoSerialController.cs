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

    Thread connectThread;
    //Thread readThread;

    private readonly object readLock = new object();
    private readonly object writeLock = new object();
    private bool imageTransmitting;

    /*
     * Default constructor
     */
    public ArduinoSerialController(ArduinoExtension mainExtension, string portName)
    {
        this.mainExtension = mainExtension;
        imageTransmitting = false;

        // Serial port setup
        serialPort = new SerialPort();
        serialPort.BaudRate = 74880;
        serialPort.PortName = portName;

        // Spin up thread to repeatedly attempt connection
        connectThread = new Thread(SerialConnectThread);
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
                // Try opening a port
                Console.WriteLine("Attempting serial connection");
                serialPort.Open();
                Console.WriteLine("Serial port opened");

                // Add event listeners
                serialPort.DataReceived += SerialMessageRecieved;
                serialPort.ErrorReceived += handleDisconnect;

                // Wait for arduino to boot then send data
                Thread.Sleep(15000);
                mainExtension.refreshDataPacket();
                sendUpdatePacket();
                return;
            }
            catch (Exception)
            {
                // Failed to open port, wait 5 seconds then try again
                Thread.Sleep(5000);
            }
        }
    }

    public void SerialMessageRecieved(object sender, SerialDataReceivedEventArgs e)
    {
        if(imageTransmitting)
        {
            // A message coming in while image is transmitting is likely a line acknowlegment byte, not a JSON message
            return;
        }

        String message = "undefined";
        try
        {
            lock (readLock)
            {
                Console.WriteLine("Read thread got lock");
                message = serialPort.ReadLine();
                Console.WriteLine("Read thread releasing lock");
            }
            Thread handleMessageThread = new Thread(() => mainExtension.HandleMessage(message));
            handleMessageThread.Start();
        }
        catch (Exception) {
            Console.WriteLine("WARNING: Failed to parse serial message");
            Console.WriteLine("\t" + message);
        }
    }

    /*
     * Handle error with Serial port
     * Closes port then spins up a new Connect thread to wait for reconnection
     */
    public void handleDisconnect(object sender, SerialErrorReceivedEventArgs e)
    {
        // Cleanup from the old connection
        Console.WriteLine("Serial port error. Closing port");
        try
        {
            serialPort.Close();
        }
        catch (Exception)
        { }

        // Spin up a new thread to attempt connections
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
            lock(writeLock)
            {
                Console.WriteLine("sendUpdatePacket got lock");
                Console.WriteLine("Serial -->: " + packet);
                serialPort.Write(packet);
                Console.WriteLine("sendUpdatePacket released lock");
            }
            return true;
        }
        catch (Exception)
        {
            handleDisconnect(null, null);
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
        lock (writeLock)
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
                lock (readLock)
                {
                    Console.WriteLine("sendIcon got lock");
                    imageTransmitting = true;

                    // Send row of pixels
                    serialPort.Write(row.ToArray(), 0, row.Count);

                    //Expect acknowledgement of line
                    while (serialPort.BytesToRead < 1)
                    { Thread.Sleep(1); }
                    Byte ack = (Byte)serialPort.ReadByte();
                    if (ack != 0xFF)
                    {
                        Console.WriteLine(String.Format("Ack not 0xFF, got 0x{0:x} . ending transmission", ack));
                        return;
                    }

                    Console.WriteLine("sendIcon releasing lock");
                    imageTransmitting = false;
                }

                //if(serialPort.BytesToRead > 0)
                //{
                //    SerialMessageRecieved(null, null);
                //}
            }
            // Tell arduino transmission is done
            serialPort.Write(new byte[] { 0xFF }, 0, 1);
        }

        // Finish up
        watch.Stop();
        Console.WriteLine("Finished transmission, average line write ms = " + watch.ElapsedMilliseconds / iconSize);
        Console.WriteLine(String.Format("{0:F2} KB/S", ((iconSize * iconSize * 2.0) / 1000) / (watch.ElapsedMilliseconds / 1000)));
    }

    public bool isOpen()
    {
        return serialPort.IsOpen;
    }
}


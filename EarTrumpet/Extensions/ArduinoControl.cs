using System;
using System.Windows.Media;
using System.IO.Ports;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.Interop.Helpers;
using Newtonsoft.Json;
using System.Threading;
using System.Collections.Specialized;
using System.Drawing;
using EarTrumpet.Interop;
using System.Text;

namespace EarTrumpet.Extensions
{
    public class ArduinoControl
    {
        [Serializable()]
        public class AppData
        {
            public string title;
            public int volume;
            public UInt16 color;
            string iconPath;
            int priority;
            IAudioDeviceSession session;

            public AppData(string title, float volume, UInt16 color, string iconPath, int priority, IAudioDeviceSession session)
            {
                this.title = title;
                this.volume = (int)Math.Round(100 *volume);
                this.color = color;
                this.iconPath = iconPath;
                this.priority = priority;
                this.session = session;
            }

            public int getPriority()
            {
                return priority;
            }

            public string getIconPath()
            {
                return iconPath;
            }

            public IAudioDeviceSession getSession()
            {
                return session;
            }
        }

        [Serializable()]
        public class ArduinoDataPacket
        {
            public string type = "data";
            public int size;
            public List<AppData> applications;
            
            public ArduinoDataPacket()
            {
                this.applications = new List<AppData>();
            }

            public ArduinoDataPacket(List<AppData> appData)
            {
                this.applications = appData;
            }

            public AppData getIndex(int i)
            {
                return applications[i];
            }
        }

        [Serializable()]
        public class GenericRequest
        {
            public string type;
        }

        [Serializable()]
        public class IconRequest
        {
            public string type;
            public int index;
        }

        [Serializable()]
        public class VolumeChangeRequest
        {
            public string type;
            public int index;
            public float volume;
        }

        // Setup serial port
        SerialPort serialPort;

        IAudioDeviceManager deviceManager;

        ArduinoDataPacket arduinoDataPacket;

        Dictionary<string, int> priorities = new Dictionary<string, int>();
        Dictionary<string, string> nameOverrides = new Dictionary<string, string>();
        Dictionary<string, UInt16> colorOverrides = new Dictionary<string, UInt16>();

        //Random color bank
        // Blue, Red, Green, Cyan, Magenta, Yellow
        List<UInt16> colorsBaseBank = new List<UInt16> { 31, 63488, 2016, 2047, 63519, 65504 };
        List<UInt16> colorsUnused;

        public ArduinoControl()
        {
            this.deviceManager = WindowsAudioFactory.Create(AudioDeviceKind.Playback);

            colorsUnused = new List<UInt16>(colorsBaseBank);

            //Priorities list
            priorities.Add("Spotify", 0);
            priorities.Add("Google Chrome", 1);
            priorities.Add("Discord", 2);
            priorities.Add("Default", 5);
            priorities.Add("steam", 6);
            priorities.Add("System Sounds", 10);
            priorities.Add("SocialClubHelper", 11);
            priorities.Add("Launcher", 11);

            //Name overrides
            nameOverrides.Add("Google Chrome", "Chrome");
            nameOverrides.Add("System Sounds", "System");
            nameOverrides.Add("steam", "Steam");
            nameOverrides.Add("VLC media player", "VLC");
            nameOverrides.Add("Red Dead Redemption 2", "Red Dead 2");

            //Color overrides
            colorOverrides.Add("Spotify", 7852);
            colorOverrides.Add("Discord", 29787);
            colorOverrides.Add("Google Chrome", 55879);

            //Serial port setup
            serialPort = new SerialPort();
            serialPort.BaudRate = 57600;
            serialPort.PortName = "COM5";

            //Data model setup
            arduinoDataPacket = arduinoDataPacketFactory();

            this.deviceManager.Default.Groups.CollectionChanged += handleNewSession;

            try
            {
                serialPort.Open();
            } catch
            {
                Console.WriteLine("ERROR OPENING SERIAL PORT");
            }

            sendUpdatePacket();

            Thread readThread = new Thread(ReadSerial);
            readThread.Start();
        }

        public void handleNewSession(object sender, NotifyCollectionChangedEventArgs e)
        {
            arduinoDataPacket = arduinoDataPacketFactory();
            sendUpdatePacket();
        }

        public ArduinoDataPacket arduinoDataPacketFactory()
        {
            ArduinoDataPacket toReturn = new ArduinoDataPacket();

            IAudioDevice device = deviceManager.Default;
            HashSet<IAudioDeviceSession> sessions = device.Groups.ToSet();

            this.arduinoDataPacket = new ArduinoDataPacket();
            foreach (IAudioDeviceSession session in sessions)
            {
                string title = session.DisplayName;
                int priority = priorities.ContainsKey(title) ? priorities[title] : priorities["Default"];
                UInt16 color = colorOverrides.ContainsKey(title) ? colorOverrides[title] : randomColor();

                if (nameOverrides.ContainsKey(title))
                {
                    title = nameOverrides[title];
                }

                toReturn.applications.Add(new AppData(title, session.Volume, color, session.IconPath, priority, session));
            }
            toReturn.applications.Sort(SortByPriority);
            for(int i = 4; i < toReturn.applications.Count;)
            {
                toReturn.applications.RemoveAt(i);
            }
            toReturn.size = toReturn.applications.Count;
            return toReturn;
        }

        public bool sendUpdatePacket()
        {
            string packet = JsonConvert.SerializeObject(arduinoDataPacket);
            Console.WriteLine("Serial -->: " + packet);
            serialPort.Write(packet);
            return false;
        }

        public void ReadSerial()
        {
            while (true)
            {
                try
                {
                    string message = serialPort.ReadLine();
                    Console.WriteLine("Serial <--: " + message);
                    HandleJsonMessage(message);
                }
                catch (Exception) { }
            }
        }

        public void HandleJsonMessage(string json)
        {
            GenericRequest packet = JsonConvert.DeserializeObject<GenericRequest>(json);
            switch(packet.type)
            {
                case "icon_request":    IconRequest iconRequest = JsonConvert.DeserializeObject<IconRequest>(json);
                                        sendIcon(arduinoDataPacket.applications[iconRequest.index].getIconPath());
                                        break;

                case "volume_change":   VolumeChangeRequest volumeChangeRequest = JsonConvert.DeserializeObject<VolumeChangeRequest>(json);
                                        IAudioDeviceSession session = arduinoDataPacket.applications[volumeChangeRequest.index].getSession();
                                        session.Volume = volumeChangeRequest.volume/100;
                                        break;

                default:                break;
            }
        }

        public void sendIcon(string path)
        {
            int iconSize = 128;
            Bitmap iconBitmap;
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(path.ToString());
                iconBitmap = icon.ToBitmap();
            }
            catch(Exception)
            {
                iconBitmap = new Bitmap(iconSize, iconSize);
            }
            int scaling = iconSize / iconBitmap.Height;
            for (int y = 0; y < iconBitmap.Height; y++)
            {
                for (int yScale = 0; yScale < scaling; yScale++)
                {
                    for (int x = 0; x < iconBitmap.Width; x++)
                    {
                        var rgbPixel = iconBitmap.GetPixel(x, y);

                        if (rgbPixel.R != 0)
                        {

                        }

                        byte r = Convert.ToByte(map(rgbPixel.R, 0, 255, 0, 31));
                        byte g = Convert.ToByte(map(rgbPixel.G, 0, 255, 0, 63));
                        byte b = Convert.ToByte(map(rgbPixel.B, 0, 255, 0, 31));

                        byte msb = Convert.ToByte((r << 3) | (g >> 3));
                        byte lsb = Convert.ToByte(255 & ((g << 5) | b));

                        byte[] bytes = new byte[] { msb, lsb };

                        for (int xScale = 0; xScale < scaling; xScale++)
                        {
                            serialPort.Write(bytes, 0, 2);
                        }
                    }
                }
            }

        }

        private double map(int value, int min, int max, int minScale, int maxScale)
        {
            double scaled = minScale + (double)(value - min) / (max - min) * (maxScale - minScale);
            return scaled;
        }

        public int SortByPriority(AppData obj1, AppData obj2)
        {
            return obj1.getPriority().CompareTo(obj2.getPriority());
        }

        public UInt16 randomColor()
        {
            //Random color bank
            // Blue, Red, Green, Cyan, Magenta, Yellow
            if (colorsUnused.Count == 0)
            {
                colorsUnused = new List<UInt16>(colorsBaseBank);
            }
            Random rand = new Random();
            int index = rand.Next(colorsUnused.Count);
            UInt16 toReturn = colorsUnused[index];
            colorsUnused.RemoveAt(index);

            return toReturn;
        }
    }
}
/*  Example JSON Serial packet
 *  
 *  // Update apps
 *  {
 *      "type": "data",
 *      "applications": 
 *      [
 *          "1": {
 *              "Name":"Spotify",
 *              "Volume": "100",
 *              "Color": [30, 215, 96],
 *              "Icon": 128x128 grid for icon
 *          },
 *          "2": {
 *      
 *          },
 *          "3": {
 *      
 *          },
 *          "4": {
 *      
 *          },
 *      ]
 *  }
 *  
 *  // Control device
 *  {
 *      "type": "control",
 *      "action": "sleep|wake"
 *  }
 * 
 * */
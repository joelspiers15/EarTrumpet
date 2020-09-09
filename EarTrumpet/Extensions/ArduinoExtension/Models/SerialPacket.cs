using EarTrumpet.DataModel.Audio;
using System;
using System.Collections.Generic;

public class SerialPacket
{
    /*
     * Full packet to be sent to arduino
     * Contains list of apps and some metadata
     */
    [Serializable()]
    public class ArduinoDataPacket
    {
        public string type = "data";
        public int size;
        public string defaultDevice;
        public List<AppData> applications;
        public List<string> audioDevices;
        public int deviceCount;
        public int time; // Time as millis into day

        public ArduinoDataPacket()
        {
            this.applications = new List<AppData>();
            this.audioDevices = new List<string>();
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

    /*
     * Data for a single app
     */
    [Serializable()]
    public class AppData
    {
        // Json Properties (to be included in serial packet)
        public string title;
        public int volume;
        public UInt16 color;

        // Server only data
        string iconPath;
        int priority;
        IAudioDeviceSession session;

        public AppData(string title, float volume, UInt16 color, string iconPath, int priority, IAudioDeviceSession session)
        {
            this.title = title;
            this.volume = (int)Math.Round(100 * volume);
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

    /*
     * Class to deserialize a request before we know what type of request it is
     */
    [Serializable()]
    public class GenericRequest
    {
        public string type;
    }

    /*
     * Class to define a request to transmit an app's icon
     */
    [Serializable()]
    public class IconRequest
    {
        public const string type = "icon_request";
        public int index;
    }

    /*
     * Class to define a request to change an app's volume
     */
    [Serializable()]
    public class VolumeChangeRequest
    {
        public const string type = "volume_change";
        public int index;
        public float volume;
    }

    [Serializable()]
    public class DeviceChangeRequest
    {
        public const string type = "device_change";
        public string deviceName;
    }

    [Serializable()]
    public class UpdateRequest
    {
        public const string type = "update_request";
    }

    [Serializable()]
    public class LogRequest
    {
        public const string type = "log";
        public int level;
        public string message;
    }
}

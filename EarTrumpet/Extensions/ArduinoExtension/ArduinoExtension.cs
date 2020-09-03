using System;
using System.Collections.Generic;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.DataModel.Audio;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Drawing;

using static Models;
using static Helpers;
using EarTrumpet.Extensions;
using System.IO;

public class ArduinoExtension
{
    IAudioDeviceManager deviceManager;

    public ArduinoDataPacket arduinoDataPacket;
    ArduinoSerialController serialController;

    const int defaultPriority = 5;
    Dictionary<String, AppOverride> appOverrides = new Dictionary<string, AppOverride>();

    /*
     * Constructor
     */
    public ArduinoExtension()
    {
        // Get device manager and setup listeners
        this.deviceManager = WindowsAudioFactory.Create(AudioDeviceKind.Playback);
        this.deviceManager.DefaultChanged += handleDefaultDeviceChange;
        this.deviceManager.Default.Groups.CollectionChanged += handleAppSessionChange;

        using (StreamReader file = File.OpenText("./AppOverrides.json"))
        {
            JsonSerializer serializer = new JsonSerializer();
            List<AppOverride> overrides = (List<AppOverride>)serializer.Deserialize(file, typeof(List<AppOverride>));
            foreach(AppOverride @override in overrides)
            {
                appOverrides.Add(@override.name, @override);
            }
        }

        //Data model setup
        refreshDataPacket();

        //Serial port setup
        serialController = new ArduinoSerialController(this);
    }

    /*
     * Generates a data packet based on current default audio device
     */
    public void refreshDataPacket()
    {
        ArduinoDataPacket toReturn = new ArduinoDataPacket();

        // Pull list of audio devices
        toReturn.defaultDevice = deviceManager.Default.DisplayName;
        foreach(IAudioDevice currDevice in deviceManager.Devices) {
            toReturn.audioDevices.Add(currDevice.DisplayName);
        }
        if(toReturn.audioDevices.Count > 6)
        {
            toReturn.audioDevices.RemoveRange(6, toReturn.audioDevices.Count - 6);
        }
        toReturn.deviceCount = toReturn.audioDevices.Count;

        // Pull list of application audio sessions from default device
        IAudioDevice device = deviceManager.Default;
        HashSet<IAudioDeviceSession> sessions = device.Groups.ToSet();

        // Add each application audio session to ArduinoDataPacket
        foreach (IAudioDeviceSession session in sessions)
        {
            string title = session.DisplayName;
            int priority = defaultPriority;

            Bitmap icon = iconBmapFromPath(session.IconPath);
            UInt16 color;
            if (icon != null)
            {
                color = colorTo16bit(colorFromBitmap(icon));
            } else
            {
                color = randomColor();
            }

            if (appOverrides.ContainsKey(title))
            {
                if (appOverrides[title].priority != null)
                {
                    priority = (int)appOverrides[title].priority;
                }
                if (appOverrides[title].name_override != null)
                {
                    title = appOverrides[title].name_override;
                }
            }

            toReturn.applications.Add(new AppData(title, session.Volume, color, session.IconPath, priority, session));
        }

        // Sort apps based on priority then trim down to top 4
        toReturn.applications.Sort(SortAppsByPriority);
        for (int i = 4; i < toReturn.applications.Count;)
        {
            toReturn.applications.RemoveAt(i);
        }
        toReturn.size = toReturn.applications.Count;

        toReturn.time = (DateTime.Now.Hour * 60 * 60 * 1000) + (DateTime.Now.Minute * 60 * 1000) + (DateTime.Now.Second * 1000) + DateTime.Now.Millisecond;

        arduinoDataPacket = toReturn;
    }

    public void handleDefaultDeviceChange(object sender, IAudioDevice newDevice)
    {
        refreshDataPacket();
        serialController.sendUpdatePacket();
        newDevice.Groups.CollectionChanged += handleAppSessionChange;
    }

    public void handleAppSessionChange(object sender, NotifyCollectionChangedEventArgs e)
    {
        refreshDataPacket();
        serialController.sendUpdatePacket();
    }

    /**
     * Method to recieve and act upon a message over Serial
     */
    public void HandleMessage(string json)
    {
        GenericRequest packet = JsonConvert.DeserializeObject<GenericRequest>(json);
        switch (packet.type)
        {
            case "icon_request":
                IconRequest iconRequest = JsonConvert.DeserializeObject<IconRequest>(json);

                // Grab bitmap from exe and transmit over serial
                Bitmap toSend = null;
                try
                {
                    toSend = iconBmapFromPath(arduinoDataPacket.applications[iconRequest.index].getIconPath());
                }
                catch (Exception) { }
                serialController.sendIconBmap(toSend);

                break;

            case "volume_change":
                VolumeChangeRequest volumeChangeRequest = JsonConvert.DeserializeObject<VolumeChangeRequest>(json);
                IAudioDeviceSession session = arduinoDataPacket.applications[volumeChangeRequest.index].getSession();
                session.Volume = volumeChangeRequest.volume / 100;
                break;

            case "device_change":
                DeviceChangeRequest deviceChangeRequest = JsonConvert.DeserializeObject<DeviceChangeRequest>(json);
                foreach(IAudioDevice device in deviceManager.Devices)
                {
                    if(device.DisplayName.Equals(deviceChangeRequest.deviceName))
                    {
                        deviceManager.Default = device;
                        break;
                    }
                }
                break;

            default: break;
        }
    }

    //TODO: Infer color from Icon
    public UInt16 randomColor()
    {
        //Random color bank
        // Blue, Red, Green, Cyan, Magenta, Yellow
        List<UInt16> colorsBank = new List<UInt16> { 31, 63488, 2016, 2047, 63519, 65504 };

        Random rand = new Random();
        int index = rand.Next(colorsBank.Count);
        UInt16 toReturn = colorsBank[index];

        return toReturn;
    }
}


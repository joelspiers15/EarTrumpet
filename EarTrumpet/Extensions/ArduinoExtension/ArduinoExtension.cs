using System;
using System.Collections.Generic;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.DataModel.Audio;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Drawing;
using System.Runtime.InteropServices;

using static Models;
using static Helpers;
using EarTrumpet.Extensions;

public class ArduinoExtension
{
    public static class foo
    {
        [DllImport("Shell32.dll", EntryPoint = "SHDefExtractIconW")]
        private static extern int SHDefExtractIconW([MarshalAs(UnmanagedType.LPTStr)] string pszIconFile, int iIndex, uint uFlags, ref IntPtr phiconLarge, ref IntPtr phiconSmall, uint nIconSize);
    }

    IAudioDeviceManager deviceManager;

    ArduinoDataPacket arduinoDataPacket;
    ArduinoSerialController serialController;

    Dictionary<string, int> priorities = new Dictionary<string, int>();
    Dictionary<string, string> nameOverrides = new Dictionary<string, string>();
    Dictionary<string, UInt16> colorOverrides = new Dictionary<string, UInt16>();

    //Random color bank
    // Blue, Red, Green, Cyan, Magenta, Yellow
    List<UInt16> colorsBaseBank = new List<UInt16> { 31, 63488, 2016, 2047, 63519, 65504 };
    List<UInt16> colorsUnused;

    /*
     * Constructor
     */
    public ArduinoExtension()
    {
        // Get device manager and setup listeners
        this.deviceManager = WindowsAudioFactory.Create(AudioDeviceKind.Playback);
        this.deviceManager.Default.Groups.CollectionChanged += handleStateChange;

        colorsUnused = new List<UInt16>(colorsBaseBank);

        //Priorities list
        priorities.Add("Spotify", 0);
        priorities.Add("Google Chrome", 1);
        priorities.Add("Discord", 2);
        priorities.Add("Default", 5);
        priorities.Add("steam", 6);
        priorities.Add("System Sounds", 10);
        priorities.Add("Blizzard Battle.net", 11);
        priorities.Add("SocialClubHelper", 11);
        priorities.Add("Launcher", 11);
        priorities.Add("Razer Synapse", 12);
        priorities.Add("ChromaVisualizer", 12);

        //Name overrides
        nameOverrides.Add("Google Chrome", "Chrome");
        nameOverrides.Add("System Sounds", "System");
        nameOverrides.Add("steam", "Steam");
        nameOverrides.Add("VLC media player", "VLC");
        nameOverrides.Add("Red Dead Redemption 2", "Red Dead 2");
        nameOverrides.Add("conhost", "Console");
        nameOverrides.Add("Blizzard Battle.net", "Battle.net");
        nameOverrides.Add("Razer Synapse", "Synapse");
        nameOverrides.Add("TwitchUI", "Twitch");

        //Color overrides
        colorOverrides.Add("Spotify", 7852);
        colorOverrides.Add("Discord", 29787);
        colorOverrides.Add("Google Chrome", 55879);
        colorOverrides.Add("Steam", 16);
        colorOverrides.Add("Red Dead Redemption 2", 63488);
        colorOverrides.Add("VLC media player", 62698);

        //Data model setup
        arduinoDataPacket = getDataPacket();

        //Serial port setup
        serialController = new ArduinoSerialController(this);
    }

    /*
     * Generates a data packet based on current default audio device
     */
    public ArduinoDataPacket getDataPacket()
    {
        ArduinoDataPacket toReturn = new ArduinoDataPacket();

        // Pull list of application audio sessions from default devices
        IAudioDevice device = deviceManager.Default;
        HashSet<IAudioDeviceSession> sessions = device.Groups.ToSet();
    
        // Add each application audio session to ArduinoDataPacket
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

        // Sort apps based on priority then trim down to top 4
        toReturn.applications.Sort(SortAppsByPriority);
        for (int i = 4; i < toReturn.applications.Count;)
        {
            toReturn.applications.RemoveAt(i);
        }
        toReturn.size = toReturn.applications.Count;

        return toReturn;
    }

    public void handleStateChange(object sender, NotifyCollectionChangedEventArgs e)
    { 
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
                Icon toSend = null;
                try
                {
                    toSend = iconFromPath(arduinoDataPacket.applications[iconRequest.index].getIconPath());
                }
                catch (Exception) { }
                serialController.sendIcon(toSend);
                break;

            case "volume_change":
                VolumeChangeRequest volumeChangeRequest = JsonConvert.DeserializeObject<VolumeChangeRequest>(json);
                IAudioDeviceSession session = arduinoDataPacket.applications[volumeChangeRequest.index].getSession();
                session.Volume = volumeChangeRequest.volume / 100;
                break;

            default: break;
        }
    }

    //TODO: Infer color from Icon
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


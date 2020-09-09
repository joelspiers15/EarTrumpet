using EarTrumpet.DataModel.Audio;
using System;
using System.Collections.Generic;

public class Config
{
    /*
     * Configuration for arduino extension 
     */
    [Serializable()]
    public class ArduinoConfig
    {
        public string portName;
        public List<AppOverride> appOverrides;
    }

    /*
     * App override settings
     * 
     * Allows overriding an app's default name or priority for display on Arduino
     */
    [Serializable()]
    public class AppOverride
    {
        public string name;
        public string name_override;
        public int? priority;
    }
}

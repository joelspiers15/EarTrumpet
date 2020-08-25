using System;
using EarTrumpet.Interop.Helpers;
using System.Drawing;
using EarTrumpet.Interop;
using System.Text;

using static Models;
using System.Runtime.InteropServices;
using TsudaKageyu;

public static class Helpers
{
    /*
     * Attempt to get an icon from an application path
     */
    public static Icon iconFromPath(string path)
    {
        Icon toReturn = null;
        try
        {
            // Get System icon
            StringBuilder iconPath = new StringBuilder((path.Contains(",") ? path : path + ",1"));
            int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);
            toReturn = IconHelper.LoadIconResource(iconPath.ToString(), iconIndex, 128, 128);
        }
        catch(Exception)
        {
            // Use IconExtractor library to try to get a high res icon
            IconExtractor ie = new IconExtractor(path);
            Icon icon = ie.GetIcon(0);
            Icon[] allIcons = IconUtil.Split(icon);

            // Pick the highest resolution icon with a bit depth >16
            int bestIcon = 0;
            for(int i = 0; i < allIcons.Length; i++)
            {
                int curSize = allIcons[i].Size.Height;
                if(curSize > allIcons[bestIcon].Size.Height && IconUtil.GetBitCount(allIcons[i]) >= 16)
                {
                    bestIcon = i;
                }
            }

            toReturn = allIcons[bestIcon];
        }
        return toReturn;
    }

    public static double map(int value, int min, int max, int minScale, int maxScale)
    {
        double scaled = minScale + (double)(value - min) / (max - min) * (maxScale - minScale);
        return scaled;
    }

    public static int SortAppsByPriority(AppData obj1, AppData obj2)
    {
        return obj1.getPriority().CompareTo(obj2.getPriority());
    }
}
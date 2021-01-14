using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ClearOneDSP
{
    public enum DeviceType: byte
    {
        ClearOne880     = 0x31, // '1' 
        ClearOneTH20    = 0x32, // '2' 
        ClearOne840T    = 0x33, // '3' 
        ClearOne8i      = 0x41, // 'A' 
        ClearOne880T    = 0x44, // 'D' 
        ClearOne880TA   = 0x48, // 'H' 
        ClearOneSR1212  = 0x47, // 'G' 
        ClearOneSR1212A = 0x49, // 'I' 
        ClearOneBeamMic = 0x4E, // 'N' 
        ClearOneDANTE   = 0x53, // 'S' 
        Any = 0x2A, // '*' 
    }

    public enum Group : byte
    {
        Input           = 0x49, // 'I' 
        Output          = 0x4F, // 'O' 
        Mic             = 0x4D, // 'M' 
        AmpOut          = 0x4A, // 'J' 
        Processing      = 0x50, // 'P' 
        LineInput       = 0x4C, // 'L' 
        Fader           = 0x58, // 'X' 
        Preset          = 0x46, // 'F' 
    }

    static class OnOffToggle
    {
        public const string Off    = "0";
        public const string On     = "1";
        public const string Toggle = "2";
    }

    public static class EnumEx
    {
        public static char ToChar(this  DeviceType type)
        {
            return Convert.ToChar((byte)type);
        }

        public static char ToChar(this  Group group)
        {
            return Convert.ToChar((byte)group);
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LedCSharp;

namespace LyncStatusforRGBDevices
{
    public enum Sdk { Logitech, Corsair }
    static class LedSdkAbstraction
    {
        private const string COLOR_OUT_OF_RANGE = "Color values must be between 0-100";

        private static int ConvertPercentagesToRgbValues(int percent)
        {
            if (percent < 0 || percent > 100) throw new ArgumentOutOfRangeException();
            return (255 * percent) / 100;
        }
        public static bool Initialize(Sdk sdk)
        {
            if (sdk == Sdk.Logitech) return LogitechGSDK.LogiLedInit();
            if (sdk == Sdk.Corsair) throw new NotImplementedException();
            return false;
        }
        public static bool Initialize(Sdk sdk, string name)
        {
            if (sdk == Sdk.Logitech) return LogitechGSDK.LogiLedInitWithName(name);
            if (sdk == Sdk.Corsair) throw new NotImplementedException();
            return false;
        }
        public static void Shutdown(Sdk sdk)
        {
            if (sdk == Sdk.Logitech) LogitechGSDK.LogiLedShutdown();
            if (sdk == Sdk.Corsair) throw new NotImplementedException();
        }
        public static bool SetLighting(Sdk sdk, int red, int green, int blue)
        {
            if (red < 0 || red > 100)
                throw new ArgumentOutOfRangeException("red", red, COLOR_OUT_OF_RANGE);
            if (blue < 0 || blue > 100)
                throw new ArgumentOutOfRangeException("blue", blue, COLOR_OUT_OF_RANGE);
            if (green < 0 || green > 100)
                throw new ArgumentOutOfRangeException("green", green, COLOR_OUT_OF_RANGE);

            if (sdk == Sdk.Logitech) return LogitechGSDK.LogiLedSetLighting(red, green, blue);

            if (sdk == Sdk.Corsair)
            {
                //red = ConvertPercentagesToRgbValues(red);
                //blue = ConvertPercentagesToRgbValues(blue);
                //green = ConvertPercentagesToRgbValues(green);
                throw new NotImplementedException();
            }
            return false;
        }
        public static bool FlashLighting()
        {
            throw new NotImplementedException();            
        }
    }
}

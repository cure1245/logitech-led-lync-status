﻿using System;
using LedCSharp;
using CUE.NET;
using CUE.NET.Brushes;
using CUE.NET.Effects;
using CUE.NET.Devices.Generic;

namespace LyncStatusforRGBDevices
{
    public enum LedSdk { Logitech, Corsair }
    static class LedSdkAbstraction
    {
        private const string COLOR_OUT_OF_RANGE = "Color values must be between 0-100";

        private static byte[] ConvertPercentagesToRgbValues(int r, int g, int b)
        {
            r = (r * 255) / 100;
            g = (g * 255) / 100;
            b = (b * 255) / 100;
            byte[] vs = { (byte)r, (byte)g, (byte)b };
            return vs;
        }
        public static bool Initialize(LedSdk sdk)
        {
            if (sdk == LedSdk.Logitech) return LogitechGSDK.LogiLedInit();
            if (sdk == LedSdk.Corsair)
            {
                CueSDK.Initialize(true);
                return true;
            }

            return false;
        }
        public static bool Initialize(LedSdk sdk, string name)
        {
            if (sdk == LedSdk.Logitech) return LogitechGSDK.LogiLedInitWithName(name);
            if (sdk == LedSdk.Corsair)
            {
                CueSDK.Initialize(true);
                CueSDK.UpdateMode = CUE.NET.Devices.Generic.Enums.UpdateMode.Continuous;
                CueSDK.HeadsetSDK.Brush = new SolidColorBrush(new CorsairColor(0, 0, 0));
                return true;
            }

            return false;
        }
        public static void Shutdown(LedSdk sdk)
        {
            if (sdk == LedSdk.Logitech) LogitechGSDK.LogiLedShutdown();
            if (sdk == LedSdk.Corsair) CueSDK.Reinitialize();
        }
        public static bool SetLighting(LedSdk sdk, int red, int green, int blue)
        {
            CheckRgbValues(red, green, blue);
            if (sdk == LedSdk.Logitech)
            {
                LogitechGSDK.LogiLedStopEffects();
                return LogitechGSDK.LogiLedSetLighting(red, green, blue);
            }

            if (sdk == LedSdk.Corsair)
            {
                CueSDK.HeadsetSDK.Brush.Effects.Clear();
                byte[] rgb = ConvertPercentagesToRgbValues(red, green, blue);
                CueSDK.HeadsetSDK.Brush = new SolidColorBrush(new CorsairColor(rgb[0], rgb[1], rgb[2]));
                return true;
            }
            return false;
        }
        public static bool FlashLighting(LedSdk sdk, int red, int green, int blue, int duration, int interval)
        {
            CheckRgbValues(red, green, blue);
            if (sdk == LedSdk.Logitech)
            {
                LogitechGSDK.LogiLedFlashLighting(red, green, blue, duration, interval);
                return true;
            }
            if (sdk == LedSdk.Corsair)
            {
                CueSDK.HeadsetSDK.Brush.Effects.Clear();
                byte[] rgb = ConvertPercentagesToRgbValues(red, green, blue);
                SolidColorBrush b = new SolidColorBrush(new CorsairColor(rgb[0], rgb[1], rgb[2]));
                b.AddEffect(new FlashEffect()
                {
                    Attack = 0f,
                    Sustain = (float)interval / 2f,
                    Release = 0f,
                    Interval = (float)interval / 2f,
                    Repetitions = duration / interval
                });
                CueSDK.HeadsetSDK.Brush = b;
                b.UpdateEffects();
                return true;
            }
            return false;
        }
        public static bool PulseLighting(LedSdk sdk, int red, int green, int blue, int duration, int interval)
        {
            CheckRgbValues(red, green, blue);
            if (sdk == LedSdk.Logitech)
            {
                LogitechGSDK.LogiLedPulseLighting(red, green, blue, duration, interval);
                return true;
            }
            if (sdk == LedSdk.Corsair)
            {
                CueSDK.HeadsetSDK.Brush.Effects.Clear();
                byte[] rgb = ConvertPercentagesToRgbValues(red, green, blue);
                SolidColorBrush b = new SolidColorBrush(new CorsairColor(rgb[0], rgb[1], rgb[2]));
                b.AddEffect(new FlashEffect()
                {
                    Attack = (float)interval / 2f,
                    Sustain = 0f,
                    Release = (float)interval / 2f,
                    Interval = (float)interval,
                    Repetitions = duration / interval
                });
                CueSDK.HeadsetSDK.Brush = b;
                b.UpdateEffects();
                return true;
            }
            return false;
        }
        private static void CheckRgbValues(int red, int green, int blue)
        {
            if (red < 0 || red > 100)
                throw new ArgumentOutOfRangeException("red", red, COLOR_OUT_OF_RANGE);
            if (blue < 0 || blue > 100)
                throw new ArgumentOutOfRangeException("blue", blue, COLOR_OUT_OF_RANGE);
            if (green < 0 || green > 100)
                throw new ArgumentOutOfRangeException("green", green, COLOR_OUT_OF_RANGE);
        }
    }
}

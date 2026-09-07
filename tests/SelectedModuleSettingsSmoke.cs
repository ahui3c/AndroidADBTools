using System;
using System.Collections;
using System.Reflection;
using System.Web.Script.Serialization;

internal static class SelectedModuleSettingsSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Missing AndroidADBTools.exe path.");
            return 2;
        }

        Assembly assembly = Assembly.LoadFrom(args[0]);
        Type settingsType = assembly.GetType("AndroidADBTools.AppSettings", true);
        PropertyInfo selectedModule = settingsType.GetProperty("SelectedModule");
        object defaults = Activator.CreateInstance(settingsType);
        if (!String.Equals((string)selectedModule.GetValue(defaults, null), "common-apps", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Default module is not common-apps.");
            return 3;
        }

        JavaScriptSerializer serializer = new JavaScriptSerializer();
        object restored = serializer.Deserialize("{\"SelectedModule\":\"device-information\"}", settingsType);
        if (!String.Equals((string)selectedModule.GetValue(restored, null), "device-information", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Selected module was not restored from JSON.");
            return 4;
        }

        Type payloadType = assembly.GetType("AndroidADBTools.AppLabelReaderPayload", true);
        byte[] payload = (byte[])payloadType.GetMethod("GetJarBytes",
            BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        if (payload.Length < 1000 || payload[0] != (byte)'P' || payload[1] != (byte)'K')
        {
            Console.Error.WriteLine("Embedded app-label reader payload is invalid.");
            return 5;
        }

        PropertyInfo calibrationsProperty = settingsType.GetProperty("BrightnessCalibrations");
        IList calibrations = calibrationsProperty.GetValue(defaults, null) as IList;
        if (calibrations == null)
        {
            Console.Error.WriteLine("Default brightness calibration collection is missing.");
            return 6;
        }
        Type calibrationType = assembly.GetType("AndroidADBTools.BrightnessCalibrationRecord", true);
        object calibration = Activator.CreateInstance(calibrationType);
        calibrationType.GetProperty("DeviceKey").SetValue(calibration, "PIXEL|ABC123", null);
        calibrationType.GetProperty("BrightnessValue").SetValue(calibration, 128, null);
        calibrationType.GetProperty("BrightnessMaximum").SetValue(calibration, 255, null);
        calibrationType.GetProperty("TargetNit").SetValue(calibration, 200M, null);
        calibrationType.GetProperty("MeasuredNit").SetValue(calibration, 199.5M, null);
        calibrations.Add(calibration);
        string savedJson = serializer.Serialize(defaults);
        object restoredSettings = serializer.Deserialize(savedJson, settingsType);
        IList restoredCalibrations = calibrationsProperty.GetValue(restoredSettings, null) as IList;
        if (restoredCalibrations == null || restoredCalibrations.Count != 1)
        {
            Console.Error.WriteLine("Brightness calibration was not restored from JSON.");
            return 7;
        }
        object restoredCalibration = restoredCalibrations[0];
        if ((int)calibrationType.GetProperty("BrightnessValue").GetValue(restoredCalibration, null) != 128 ||
            (decimal)calibrationType.GetProperty("MeasuredNit").GetValue(restoredCalibration, null) != 199.5M)
        {
            Console.Error.WriteLine("Brightness calibration values changed during serialization.");
            return 8;
        }

        return 0;
    }
}

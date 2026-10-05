using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Minimal playtest log: one CSV row per event in Application.persistentDataPath/playtest_log.csv.
// Columns: utc, session, mode, event, wave, a, b, c
public static class ProtoTelemetry
{
    // Marks sessions that weren't played by a person (the editor autopilot sets it), so playtest data stays separable.
    public static string Tag;

    static string session;
    static string path;

    public static string FilePath => path ?? (path = Path.Combine(Application.persistentDataPath, "playtest_log.csv"));

    public static void BeginSession(string mode)
    {
        session = Guid.NewGuid().ToString("N").Substring(0, 8);
        Log("session_start", 0, mode, Tag);
    }

    public static void Log(string evt, int wave, object a = null, object b = null, object c = null)
    {
        try
        {
            bool newFile = !File.Exists(FilePath);
            using (var w = new StreamWriter(FilePath, true))
            {
                if (newFile)
                    w.WriteLine("utc,session,mode,event,wave,a,b,c");
                w.WriteLine(string.Join(",",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    session ?? "-",
                    ProtoThrower.mode,
                    evt,
                    wave.ToString(CultureInfo.InvariantCulture),
                    Format(a), Format(b), Format(c)));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Telemetry write failed: " + e.Message);
        }
    }

    static string Format(object o)
    {
        if (o == null) return "";
        if (o is float f) return f.ToString("0.###", CultureInfo.InvariantCulture);
        return o.ToString().Replace(",", ";").Replace("\n", " | ");
    }
}

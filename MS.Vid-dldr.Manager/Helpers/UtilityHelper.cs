using System;
using System.Text;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;

namespace MS.Vid_dldr.Manager.Helpers;

public static class UtilityHelper
{
    public static string GetVersion()
    {
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);

        return fvi.FileVersion;
    }

    public static DateTime GetDateTimeNowUtc()
    {
        return DateTime.UtcNow;
    }

    public static string ToJsonString(JsonDocument? jsonDocument)
    {
        if (jsonDocument == null) return "";

        using (var stream = new MemoryStream())
        {
            Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            jsonDocument.WriteTo(writer);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public static Timestamp GetProtoTimestampNotSet()
    {
        Timestamp timestampNotSet = new Timestamp();
        timestampNotSet.Seconds = 0;
        timestampNotSet.Nanos = 0;

        return timestampNotSet;
    }

    public static int RandomInt()
    {
        Random rand = new Random();

        return rand.Next(1, 10);
    }

    public static DateTime SetKind(this DateTime DT, DateTimeKind DTKind)
    {
        return DateTime.SpecifyKind(DT, DTKind);
    }

    public static DateTime GetMinWithKind(DateTimeKind DTKind)
    {
        return DateTime.SpecifyKind(DateTime.MinValue, DTKind);
    }
}
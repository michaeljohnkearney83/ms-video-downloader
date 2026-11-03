namespace MS.Vid_dldr.DownloadUpdater.Helpers;

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
}
using System.Text.RegularExpressions;

namespace MS.Vid_dldr.Manager.Helpers;

/// <summary>
/// A session's deployment folder: <c>AppSettings:HomeDrivePath</c> + deployment number (e.g. C:\Users\me\Videos\vd\1380\),
/// where a download goes when the client leaves the folder empty.
/// </summary>
public static partial class DeploymentFolder
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _.-]{0,63}$")]
    private static partial Regex SafeName();

    /// <summary>A deployment number that is one plain folder name (no separators, no "..", no drive).</summary>
    public static bool IsSafeName(string? deploymentNo) =>
        deploymentNo != null && SafeName().IsMatch(deploymentNo) && !deploymentNo.Contains("..");

    /// <summary>The folder as the Distributor (on Windows) sees it.</summary>
    public static string Default(string? homeDrivePath, string deploymentNo)
    {
        var root = homeDrivePath ?? "";
        if (root.Length > 0 && !root.EndsWith('\\') && !root.EndsWith('/')) root += "\\";
        return $"{root}{deploymentNo}\\";
    }

    /// <summary>
    /// Creates the deployment folder through <paramref name="mountPath"/>, the HomeDrivePath folder as mounted into this
    /// container. Returns the path it created, or null when there was nothing to do (no mount configured, or the folder
    /// already exists). Throws <see cref="ArgumentException"/> for a deployment number that isn't a plain folder name.
    /// </summary>
    public static string? Create(string? mountPath, string deploymentNo)
    {
        if (string.IsNullOrWhiteSpace(mountPath)) return null;
        if (!IsSafeName(deploymentNo))
            throw new ArgumentException($"Deployment no. '{deploymentNo}' is not a plain folder name", nameof(deploymentNo));

        var folder = Path.Combine(mountPath, deploymentNo);
        if (Directory.Exists(folder)) return null;
        Directory.CreateDirectory(folder);
        return folder;
    }
}

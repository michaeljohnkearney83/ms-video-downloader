namespace MS.Vid_dldr.Distributor;

/// <summary>Creates the share (distribution) folder and deployment folders (e.g. ...\vd\1380) when they are missing.</summary>
public static class FolderHelper
{
    /// <summary>
    /// True when <paramref name="path"/> exists or was created; <paramref name="created"/> says which. False, with the
    /// reason in <paramref name="error"/>, for a relative path or one that can't be created (no access, bad drive).
    /// </summary>
    public static bool EnsureFolder(string? path, out bool created, out string? error)
    {
        created = false;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "No folder given";
            return false;
        }
        // A relative path would be created next to the Distributor's exe, not where the user expects the file.
        if (!Path.IsPathFullyQualified(path))
        {
            error = $"Folder is not a full path: {path}";
            return false;
        }
        if (Directory.Exists(path)) return true;

        try
        {
            Directory.CreateDirectory(path);
            created = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            error = $"Could not create {path}: {ex.Message}";
            return false;
        }
    }
}

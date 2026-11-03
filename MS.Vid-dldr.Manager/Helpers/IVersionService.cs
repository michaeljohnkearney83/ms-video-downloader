namespace MS.Vid_dldr.Manager.Helpers;

public interface IVersionService
{
    void InitiateCollectionOfVersions(System.DateTime? requested = null);
}
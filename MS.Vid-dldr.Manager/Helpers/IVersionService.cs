namespace MS.Vid_dldr.Manager.Helpers;

public interface IVersionService
{
    Task InitiateCollectionOfVersionsAsync(System.DateTime? requested = null);
}
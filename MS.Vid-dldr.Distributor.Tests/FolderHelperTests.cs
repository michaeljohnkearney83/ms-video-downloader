namespace MS.Vid_dldr.Distributor.Tests;

public class FolderHelperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vd-folder-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Missing_deployment_folder_is_created_with_its_parents()
    {
        // The Manager's default: HomeDrivePath + deployment number + "\".
        var folder = Path.Combine(_root, "vd", "1380") + Path.DirectorySeparatorChar;

        Assert.True(FolderHelper.EnsureFolder(folder, out var created, out var error));
        Assert.True(created);
        Assert.Null(error);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void Existing_folder_is_left_alone()
    {
        Directory.CreateDirectory(_root);

        Assert.True(FolderHelper.EnsureFolder(_root, out var created, out _));
        Assert.False(created);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vd/1380")]
    public void Empty_or_relative_paths_are_refused(string? folder)
    {
        Assert.False(FolderHelper.EnsureFolder(folder, out var created, out var error));
        Assert.False(created);
        Assert.NotNull(error);
    }

    [Fact]
    public void A_path_under_a_file_cannot_be_created()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "not-a-folder");
        File.WriteAllText(file, "x");

        Assert.False(FolderHelper.EnsureFolder(Path.Combine(file, "1380"), out _, out var error));
        Assert.StartsWith("Could not create", error);
    }
}

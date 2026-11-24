using MS.Vid_dldr.Manager.Helpers;

namespace MS.Vid_dldr.Manager.Tests;

public class DeploymentFolderTests : IDisposable
{
    private readonly string _mount = Path.Combine(Path.GetTempPath(), "vd-mount-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_mount)) Directory.Delete(_mount, recursive: true);
    }

    [Theory]
    [InlineData("C:/Users/me/Videos/vd/", "1380", "C:/Users/me/Videos/vd/1380\\")]
    [InlineData("C:\\H-Drive\\", "1380", "C:\\H-Drive\\1380\\")]
    [InlineData("C:/Users/me/Videos/vd", "1380", "C:/Users/me/Videos/vd\\1380\\")] // no trailing slash: still a subfolder
    public void Default_is_home_drive_plus_deployment_no(string home, string deploymentNo, string expected)
    {
        Assert.Equal(expected, DeploymentFolder.Default(home, deploymentNo));
    }

    [Fact]
    public void Starting_a_session_creates_the_folder_once()
    {
        Directory.CreateDirectory(_mount);

        Assert.Equal(Path.Combine(_mount, "1380"), DeploymentFolder.Create(_mount, "1380"));
        Assert.True(Directory.Exists(Path.Combine(_mount, "1380")));
        Assert.Null(DeploymentFolder.Create(_mount, "1380")); // already there
    }

    [Fact]
    public void Without_a_mount_nothing_is_created()
    {
        Assert.Null(DeploymentFolder.Create("", "1380"));
        Assert.Null(DeploymentFolder.Create(null, "1380"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../etc")]
    [InlineData("1380/../x")]
    [InlineData("C:\\Windows")]
    [InlineData("")]
    public void Deployment_no_must_be_a_plain_folder_name(string deploymentNo)
    {
        Assert.False(DeploymentFolder.IsSafeName(deploymentNo));
        Assert.Throws<ArgumentException>(() => DeploymentFolder.Create(_mount, deploymentNo));
    }
}

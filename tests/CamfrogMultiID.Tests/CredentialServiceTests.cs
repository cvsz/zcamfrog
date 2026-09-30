using System.IO;
using System.Text;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class CredentialServiceTests : IDisposable
{
  private readonly string _tempRoot;
  private readonly AppPaths _paths;
  private readonly CredentialService _svc;

  public CredentialServiceTests()
  {
    _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-cred-" + Guid.NewGuid().ToString("N"));
    _paths = new AppPaths(_tempRoot);
    _svc = new CredentialService(_paths);
  }

  public void Dispose()
  {
    try { Directory.Delete(_tempRoot, true); } catch { }
  }

  [Fact]
  public void Save_And_Load_Roundtrips()
  {
    _svc.Save("acc1", "s3cr3t!");
    var loaded = _svc.Load("acc1");
    Assert.Equal("s3cr3t!", loaded);
  }

  [Fact]
  public void Load_Missing_ReturnsNull()
  {
    Assert.Null(_svc.Load("nonexistent"));
  }

  [Fact]
  public void Save_EmptyPassword_Roundtrips()
  {
    _svc.Save("empty", "");
    Assert.Equal("", _svc.Load("empty"));
  }

  [Fact]
  public void Save_Unicode_Roundtrips()
  {
    var pwd = "pässwörd🔒";
    _svc.Save("unicode", pwd);
    Assert.Equal(pwd, _svc.Load("unicode"));
  }

  [Fact]
  public void Save_Overwrite_Updates()
  {
    _svc.Save("over", "first");
    _svc.Save("over", "second");
    Assert.Equal("second", _svc.Load("over"));
  }

  [Fact]
  public void Save_InvalidName_Throws()
  {
    Assert.ThrowsAny<ArgumentException>(() => _svc.Save("", "pwd"));
    Assert.ThrowsAny<ArgumentException>(() => _svc.Save("   ", "pwd"));
    Assert.ThrowsAny<ArgumentException>(() => _svc.Save(null!, "pwd"));
  }

  [Fact]
  public void Save_DoesNotLeavePlaintextOnDisk()
  {
    var pwd = "superSecret123";
    _svc.Save("plaincheck", pwd);
    var path = Path.Combine(_paths.Secrets, "plaincheck.bin");
    var bytes = File.ReadAllBytes(path);
    var asText = Encoding.UTF8.GetString(bytes);
    Assert.DoesNotContain(pwd, asText);
    // file should not be readable as plaintext
    Assert.NotEqual(pwd, asText);
  }

  [Fact]
  public void Load_CorruptedFile_Throws()
  {
    var path = Path.Combine(_paths.Secrets, "corrupt.bin");
    File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5 });
    Assert.ThrowsAny<Exception>(() => _svc.Load("corrupt"));
  }

  [Fact]
  public void Save_CreatesSecretsDirectory()
  {
    var name = "dirtest";
    _svc.Save(name, "pwd");
    Assert.True(Directory.Exists(_paths.Secrets));
    Assert.True(File.Exists(Path.Combine(_paths.Secrets, name + ".bin")));
  }

  [Fact]
  public void Save_LongPassword_Roundtrips()
  {
    var longPwd = new string('a', 5000);
    _svc.Save("long", longPwd);
    Assert.Equal(longPwd, _svc.Load("long"));
  }

  [Theory]
  [InlineData("..")]
  [InlineData(".")]
  [InlineData("../evil")]
  [InlineData("..\\evil")]
  [InlineData("a/b")]
  [InlineData("a\\b")]
  [InlineData("C:evil")]
  [InlineData("\\\\server\\share\\evil")]
  [InlineData("sub\\..\\..\\evil")]
  [InlineData("evil*")]
  [InlineData("evil?")]
  [InlineData("evil\"x")]
  [InlineData("evil|x")]
  [InlineData("evil ")]
  [InlineData("evil.")]
  [InlineData("evil\tx")]
  [InlineData("evil\nx")]
  public void HostileName_NeverEscapesSecretsDirectory(string name)
  {
    // Regression: SecretPath used Path.Combine directly, so a traversing
    // name could read, overwrite, or delete files outside the secrets dir.
    var outside = Path.Combine(_tempRoot, "outside.bin");
    File.WriteAllText(outside, "untouched");
    var before = Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories)
        .Select(f => f)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    Assert.ThrowsAny<ArgumentException>(() => _svc.Save(name, "pwd"));
    Assert.ThrowsAny<ArgumentException>(() => _svc.Load(name));
    Assert.ThrowsAny<ArgumentException>(() => _svc.Delete(name));
    Assert.False(_svc.Exists(name));

    Assert.Equal("untouched", File.ReadAllText(outside));
    var created = Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories)
        .Where(f => !before.Contains(f))
        .ToList();
    Assert.Empty(created);
  }

  [Fact]
  public void HostileName_DoesNotDeleteExistingOutsideFile()
  {
    var outside = Path.Combine(_tempRoot, "keep.bin");
    File.WriteAllText(outside, "keep");
    Assert.ThrowsAny<ArgumentException>(() => _svc.Delete("..\\keep"));
    Assert.True(File.Exists(outside));
  }

  [Fact]
  public void ValidName_IsAcceptedAndContained()
  {
    _svc.Save("user_1", "pw");
    Assert.Equal("pw", _svc.Load("user_1"));
    Assert.True(_svc.Exists("user_1"));
    Assert.True(File.Exists(Path.Combine(_paths.Secrets, "user_1.bin")));
    Assert.True(_svc.Delete("user_1"));
    Assert.False(_svc.Exists("user_1"));
  }

  [Fact]
  public void ValidateSecretName_RejectsOverlongName()
  {
    Assert.ThrowsAny<ArgumentException>(() => CredentialService.ValidateSecretName(new string('a', 129)));
    CredentialService.ValidateSecretName(new string('a', 128));
  }
}

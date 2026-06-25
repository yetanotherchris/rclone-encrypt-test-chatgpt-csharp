using System.Diagnostics;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RcloneEncryptTestChatgptCsharp.Tests;

public sealed class CliTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ProjectPath = Path.Combine(RepoRoot, "src", "rclone-encrypt-test-chatgpt-csharp", "rclone-encrypt-test-chatgpt-csharp.csproj");

    [Fact]
    public async Task EncryptDecrypt_WithPasswordFlag_AndDefaultSalt()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.txt");
        var encrypted = Path.Combine(dir, "encrypted.bin");
        var output = Path.Combine(dir, "output.txt");

        await File.WriteAllTextAsync(input, "hello world");

        var encrypt = await RunCliAsync(["encrypt", "--password", "Testpassword1", "-i", input, "-o", encrypted]);
        AssertSuccess(encrypt);
        Assert.Contains("WARNING", encrypt.Stderr);

        var decrypt = await RunCliAsync(["decrypt", "--password", "Testpassword1", "-i", encrypted, "-o", output]);
        AssertSuccess(decrypt);

        var result = await File.ReadAllTextAsync(output);
        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task EncryptDecrypt_WithPasswordFlag_AndCustomSalt()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.txt");
        var encrypted = Path.Combine(dir, "encrypted.bin");
        var output = Path.Combine(dir, "output.txt");
        const string salt = "deadbeefdeadbeefdeadbeefdeadbeef";

        await File.WriteAllTextAsync(input, "salted content");

        var encrypt = await RunCliAsync(["encrypt", "--password", "Testpassword1", "--salt", salt, "-i", input, "-o", encrypted]);
        AssertSuccess(encrypt);

        var decrypt = await RunCliAsync(["decrypt", "--password", "Testpassword1", "--salt", salt, "-i", encrypted, "-o", output]);
        AssertSuccess(decrypt);

        var result = await File.ReadAllTextAsync(output);
        Assert.Equal("salted content", result);
    }

    [Fact]
    public async Task EncryptDecrypt_WithBase64FilenameEncoding_AutoDerivedOutput()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "TEST_FILE.txt");
        await File.WriteAllTextAsync(input, "alpha beta gamma");

        var encrypt = await RunCliAsync(["encrypt", "--password", "Testpassword1", "--filename-encoding", "base64", "-i", input]);
        AssertSuccess(encrypt);
        Assert.Contains("Derived output filename:", encrypt.Stderr);

        var encryptedName = ExtractDerivedPath(encrypt.Stderr);
        Assert.NotNull(encryptedName);
        Assert.True(File.Exists(encryptedName));

        var decrypt = await RunCliAsync(["decrypt", "--password", "Testpassword1", "--filename-encoding", "base64", "-i", encryptedName!]);
        AssertSuccess(decrypt);

        var restored = await File.ReadAllTextAsync(input);
        Assert.Equal("alpha beta gamma", restored);
    }

    [Fact]
    public async Task EncryptDecrypt_PromptedPasswordAndSalt_WhenInputRedirected()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "prompt.txt");
        var encrypted = Path.Combine(dir, "prompt.enc");
        var output = Path.Combine(dir, "prompt.out");

        await File.WriteAllTextAsync(input, "prompt flow");

        var encryptInput = "Testpassword1" + Environment.NewLine + Environment.NewLine;
        var encrypt = await RunCliAsync(["encrypt", "-i", input, "-o", encrypted], stdin: encryptInput);
        AssertSuccess(encrypt);
        Assert.Contains("Password:", encrypt.Stderr);
        Assert.Contains("Salt (optional", encrypt.Stderr);

        var decryptInput = "Testpassword1" + Environment.NewLine + Environment.NewLine;
        var decrypt = await RunCliAsync(["decrypt", "-i", encrypted, "-o", output], stdin: decryptInput);
        AssertSuccess(decrypt);

        var result = await File.ReadAllTextAsync(output);
        Assert.Equal("prompt flow", result);
    }

    [Fact]
    public async Task DecryptsProvidedBase32Fixture_ToExpectedFilename()
    {
        var fixture = Path.Combine(RepoRoot, "kr9tu4e1da4u3nifdd99g9tf5o");
        var dir = CreateTempDir();
        var localFixture = Path.Combine(dir, Path.GetFileName(fixture));
        File.Copy(fixture, localFixture);

        var output = Path.Combine(CreateTempDir(), "ignored.tmp");
        var result = await RunCliAsync(["decrypt", "--password", "Testpassword1", "--filename-encoding", "base32", "-i", localFixture, "-o", output]);
        AssertSuccess(result);

        var auto = await RunCliAsync(["decrypt", "--password", "Testpassword1", "--filename-encoding", "base32", "-i", localFixture]);
        AssertSuccess(auto);
        Assert.Contains("Derived output filename:", auto.Stderr);
        Assert.EndsWith("TEST_FILE.txt", ExtractDerivedPath(auto.Stderr), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecryptsProvidedBase64Fixture_ToExpectedFilename()
    {
        var fixture = Path.Combine(RepoRoot, "Iyxcijgc9bp3o5Y0npW6xqUvwWNcc3MA4SadB0sR6cY");
        var dir = CreateTempDir();
        var localFixture = Path.Combine(dir, Path.GetFileName(fixture));
        File.Copy(fixture, localFixture);

        var auto = await RunCliAsync(["decrypt", "--password", "Testpassword1", "--filename-encoding", "base64", "-i", localFixture]);
        AssertSuccess(auto);
        Assert.Contains("Derived output filename:", auto.Stderr);
        Assert.Contains("TEST_FILE", ExtractDerivedPath(auto.Stderr), StringComparison.Ordinal);
    }

    private static string? ExtractDerivedPath(string stderr)
    {
        const string marker = "Derived output filename:";
        var index = stderr.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var end = stderr.IndexOfAny(['\r', '\n'], start);
        if (end < 0)
        {
            end = stderr.Length;
        }

        return stderr[start..end].Trim();
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "rclone-encrypt-test-chatgpt-csharp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(IReadOnlyList<string> args, string? stdin = null)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardInput = stdin is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(ProjectPath);
        psi.ArgumentList.Add("--");
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start process");

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var readme = Path.Combine(current.FullName, "README.md");
            var project = Path.Combine(current.FullName, "src", "rclone-encrypt-test-chatgpt-csharp", "rclone-encrypt-test-chatgpt-csharp.csproj");
            if (File.Exists(readme) && File.Exists(project))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("could not locate repository root");
    }

    private static void AssertSuccess((int ExitCode, string Stdout, string Stderr) result)
    {
        Assert.True(result.ExitCode == 0, $"Exit code was {result.ExitCode}\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");
    }
}

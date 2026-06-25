using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;
using TweetNaclSharp;

namespace RcloneEncryptTestChatgptCsharp;

internal sealed class RcloneKey
{
    public required byte[] DataKey { get; init; }
    public required byte[] NameKey { get; init; }
    public required byte[] NameTweak { get; init; }
}

internal static class RcloneCrypt
{
    private static readonly byte[] FileMagic = "RCLONE\0\0"u8.ToArray();

    private const int FileHeaderSize = 32;
    private const int BlockDataSize = 64 * 1024;
    private const int SecretBoxOverhead = 16;
    private static readonly byte[] DefaultSalt =
    [
        0xA8, 0x0D, 0xF4, 0x3A, 0x8F, 0xBD, 0x03, 0x08,
        0xA7, 0xCA, 0xB8, 0x3E, 0x58, 0x1F, 0x86, 0xB1
    ];

    public static RcloneKey DeriveKey(string password, byte[]? salt)
    {
        salt ??= DefaultSalt;
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var derived = SCrypt.Generate(passwordBytes, salt, 16384, 8, 1, 80);
        return new RcloneKey
        {
            DataKey = derived[..32],
            NameKey = derived[32..64],
            NameTweak = derived[64..80]
        };
    }

    public static async Task EncryptFileAsync(string inputPath, string outputPath, string password, byte[]? salt)
    {
        var key = DeriveKey(password, salt);

        await using var input = File.OpenRead(inputPath);
        await using var output = File.Create(outputPath);

        var nonce = new byte[24];
        RandomNumberGenerator.Fill(nonce);

        var header = new byte[FileHeaderSize];
        FileMagic.CopyTo(header.AsSpan(0, 8));
        nonce.CopyTo(header, 8);
        await output.WriteAsync(header);

        var buffer = new byte[BlockDataSize];

        while (true)
        {
            var read = await input.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            var encrypted = Nacl.Secretbox(buffer.AsSpan(0, read).ToArray(), nonce, key.DataKey);
            await output.WriteAsync(encrypted);

            IncrementNonce(nonce);

            if (read < BlockDataSize)
            {
                break;
            }
        }
    }

    public static async Task DecryptFileAsync(string inputPath, string outputPath, string password, byte[]? salt)
    {
        var key = DeriveKey(password, salt);

        await using var input = File.OpenRead(inputPath);
        await using var output = File.Create(outputPath);

        var header = new byte[FileHeaderSize];
        var headerRead = await input.ReadAtLeastAsync(header, FileHeaderSize, throwOnEndOfStream: false);
        if (headerRead < FileHeaderSize)
        {
            throw new InvalidOperationException("encrypted file too short");
        }

        if (!header.AsSpan(0, 8).SequenceEqual(FileMagic))
        {
            throw new InvalidOperationException("bad magic bytes");
        }

        var nonce = header.AsSpan(8, 24).ToArray();
        var block = new byte[BlockDataSize + SecretBoxOverhead];

        while (true)
        {
            var authRead = await input.ReadAtLeastAsync(block.AsMemory(0, SecretBoxOverhead), SecretBoxOverhead, throwOnEndOfStream: false);
            if (authRead == 0)
            {
                break;
            }

            if (authRead < SecretBoxOverhead)
            {
                throw new InvalidOperationException("encrypted file too short");
            }

            var dataRead = 0;
            while (dataRead < BlockDataSize)
            {
                var read = await input.ReadAsync(block.AsMemory(SecretBoxOverhead + dataRead, BlockDataSize - dataRead));
                if (read == 0)
                {
                    break;
                }

                dataRead += read;
            }

            var payloadLength = SecretBoxOverhead + dataRead;
            var plaintext = Nacl.SecretboxOpen(block.AsSpan(0, payloadLength).ToArray(), nonce, key.DataKey);
            if (plaintext is null)
            {
                throw new InvalidOperationException("wrong password or corrupt data");
            }

            await output.WriteAsync(plaintext);
            IncrementNonce(nonce);

            if (dataRead < BlockDataSize)
            {
                break;
            }
        }
    }

    private static void IncrementNonce(byte[] nonce)
    {
        for (var i = 0; i < nonce.Length; i++)
        {
            nonce[i]++;
            if (nonce[i] != 0)
            {
                return;
            }
        }
    }
}

using System.Security.Cryptography;

namespace RcloneEncryptTestChatgptCsharp;

internal static class FilenameCrypt
{
    public static string EncryptFileName(byte[] nameKey, byte[] tweak, string plaintext, FilenameEncoding encoding)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = nameKey;

        var plain = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var padded = Pkcs7Pad(plain, 16);
        var encrypted = EmeTransform(aes, tweak, padded, encrypt: true);

        return encoding switch
        {
            FilenameEncoding.Base64 => Base64UrlEncode(encrypted),
            _ => Base32HexLowerEncode(encrypted)
        };
    }

    public static string DecryptFileName(byte[] nameKey, byte[] tweak, string ciphertext, FilenameEncoding encoding)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return ciphertext;
        }

        var cipherBytes = encoding switch
        {
            FilenameEncoding.Base64 => Base64UrlDecode(ciphertext),
            _ => Base32HexLowerDecode(ciphertext)
        };

        if (cipherBytes.Length == 0 || cipherBytes.Length % 16 != 0)
        {
            throw new InvalidOperationException("encrypted name is not a multiple of block size");
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = nameKey;

        var plainPadded = EmeTransform(aes, tweak, cipherBytes, encrypt: false);
        var plain = Pkcs7Unpad(plainPadded);
        return System.Text.Encoding.UTF8.GetString(plain);
    }

    private static byte[] EmeTransform(SymmetricAlgorithm aes, byte[] tweak, byte[] input, bool encrypt)
    {
        if (tweak.Length != 16)
        {
            throw new InvalidOperationException("tweak must be 16 bytes");
        }

        if (input.Length == 0 || input.Length % 16 != 0)
        {
            throw new InvalidOperationException("data must be a positive multiple of 16 bytes");
        }

        var m = input.Length / 16;
        if (m > 16 * 8)
        {
            throw new InvalidOperationException("EME supports up to 128 blocks");
        }

        var output = new byte[input.Length];
        var lTable = TabulateL(aes, m);

        for (var j = 0; j < m; j++)
        {
            var p = input.AsSpan(j * 16, 16).ToArray();
            var pp = Xor(p, lTable[j]);
            var ppp = AesTransform(aes, pp, encrypt);
            ppp.CopyTo(output, j * 16);
        }

        var mp = Xor(output.AsSpan(0, 16).ToArray(), tweak);
        for (var j = 1; j < m; j++)
        {
            mp = Xor(mp, output.AsSpan(j * 16, 16).ToArray());
        }

        var mc = AesTransform(aes, mp, encrypt);
        var mixed = Xor(mp, mc);

        for (var j = 1; j < m; j++)
        {
            mixed = MultiplyByTwo(mixed);
            var cccj = Xor(output.AsSpan(j * 16, 16).ToArray(), mixed);
            cccj.CopyTo(output, j * 16);
        }

        var ccc1 = Xor(mc, tweak);
        for (var j = 1; j < m; j++)
        {
            ccc1 = Xor(ccc1, output.AsSpan(j * 16, 16).ToArray());
        }

        ccc1.CopyTo(output, 0);

        for (var j = 0; j < m; j++)
        {
            var transformed = AesTransform(aes, output.AsSpan(j * 16, 16).ToArray(), encrypt);
            var finalBlock = Xor(transformed, lTable[j]);
            finalBlock.CopyTo(output, j * 16);
        }

        return output;
    }

    private static List<byte[]> TabulateL(SymmetricAlgorithm aes, int m)
    {
        var zero = new byte[16];
        var li = AesTransform(aes, zero, encrypt: true);
        var list = new List<byte[]>(capacity: m);
        for (var i = 0; i < m; i++)
        {
            li = MultiplyByTwo(li);
            list.Add(li.ToArray());
        }

        return list;
    }

    private static byte[] AesTransform(SymmetricAlgorithm aes, byte[] block, bool encrypt)
    {
        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        var output = new byte[16];
        _ = transform.TransformBlock(block, 0, 16, output, 0);
        return output;
    }

    private static byte[] MultiplyByTwo(byte[] input)
    {
        if (input.Length != 16)
        {
            throw new InvalidOperationException("len must be 16");
        }

        var output = new byte[16];
        output[0] = (byte)(2 * input[0]);
        output[0] = (byte)(output[0] ^ (135 & (byte)(-(input[15] >> 7))));

        for (var j = 1; j < 16; j++)
        {
            output[j] = (byte)(2 * input[j]);
            output[j] += (byte)(input[j - 1] >> 7);
        }

        return output;
    }

    private static byte[] Xor(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            throw new InvalidOperationException("xor length mismatch");
        }

        var output = new byte[a.Length];
        for (var i = 0; i < a.Length; i++)
        {
            output[i] = (byte)(a[i] ^ b[i]);
        }

        return output;
    }

    private static byte[] Pkcs7Pad(byte[] input, int blockSize)
    {
        var padding = blockSize - (input.Length % blockSize);
        var output = new byte[input.Length + padding];
        input.CopyTo(output, 0);
        for (var i = input.Length; i < output.Length; i++)
        {
            output[i] = (byte)padding;
        }

        return output;
    }

    private static byte[] Pkcs7Unpad(byte[] input)
    {
        if (input.Length == 0)
        {
            throw new InvalidOperationException("invalid padding");
        }

        var padding = input[^1];
        if (padding == 0 || padding > input.Length)
        {
            throw new InvalidOperationException("invalid padding");
        }

        for (var i = input.Length - padding; i < input.Length; i++)
        {
            if (input[i] != padding)
            {
                throw new InvalidOperationException("invalid padding");
            }
        }

        return input[..^padding];
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var value = input.Replace('-', '+').Replace('_', '/');
        value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
        return Convert.FromBase64String(value);
    }

    private static readonly char[] Base32HexAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();
    private static readonly Dictionary<char, int> Base32HexMap = BuildBase32HexMap();

    private static string Base32HexLowerEncode(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                var index = (buffer >> (bitsLeft - 5)) & 0x1F;
                sb.Append(char.ToLowerInvariant(Base32HexAlphabet[index]));
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 0x1F;
            sb.Append(char.ToLowerInvariant(Base32HexAlphabet[index]));
        }

        return sb.ToString();
    }

    private static byte[] Base32HexLowerDecode(string encoded)
    {
        if (encoded.EndsWith('=') || encoded.Contains('='))
        {
            throw new InvalidOperationException("encrypted filename contains padding characters");
        }

        var clean = encoded.Trim().ToUpperInvariant();
        var bytes = new List<byte>(clean.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in clean)
        {
            if (!Base32HexMap.TryGetValue(c, out var val))
            {
                throw new InvalidOperationException("invalid base32 character");
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            while (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return bytes.ToArray();
    }

    private static Dictionary<char, int> BuildBase32HexMap()
    {
        var map = new Dictionary<char, int>(32);
        for (var i = 0; i < Base32HexAlphabet.Length; i++)
        {
            map[Base32HexAlphabet[i]] = i;
        }

        return map;
    }
}

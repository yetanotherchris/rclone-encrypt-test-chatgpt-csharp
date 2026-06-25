using System.Security.Cryptography;
using System.Text;
using System.Reflection;

namespace RcloneEncryptTestChatgptCsharp;

internal static class Cli
{
    private static readonly string Version = ResolveVersion();

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "encrypt" or "e" => await RunEncryptAsync(commandArgs),
                "decrypt" or "d" => await RunDecryptAsync(commandArgs),
                "generate-salt" => RunGenerateSalt(),
                "version" or "--version" or "-v" => RunVersion(),
                "help" or "--help" or "-h" => RunHelp(),
                _ => Fail($"unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int RunHelp()
    {
        PrintUsage();
        return 0;
    }

    private static int RunVersion()
    {
        Console.WriteLine($"rclone-encrypt-test-chatgpt-csharp {Version}");
        return 0;
    }

    private static string ResolveVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex >= 0 ? informational[..plusIndex] : informational;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";
    }

    private static int RunGenerateSalt()
    {
        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);
        Console.WriteLine(Convert.ToHexStringLower(salt));
        return 0;
    }

    private static async Task<int> RunEncryptAsync(string[] args)
    {
        var options = CommandOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintEncryptUsage();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.InputFile))
        {
            PrintEncryptUsage();
            return Fail("input file is required via -i or --input-file");
        }

        var password = await ResolvePasswordAsync(options.Password);
        var salt = await ResolveSaltAsync(options.SaltHex);
        var encoding = ResolveFilenameEncoding(options.FilenameEncoding);

        var output = options.OutputFile;
        if (string.IsNullOrWhiteSpace(output))
        {
            output = DeriveEncryptOutput(options.InputFile, password, salt, encoding);
            Console.Error.WriteLine($"Derived output filename: {output}");
        }

        Console.Error.WriteLine($"Encrypting {options.InputFile} -> {output} ...");
        await RcloneCrypt.EncryptFileAsync(options.InputFile, output, password, salt);
        Console.Error.WriteLine("Done.");
        return 0;
    }

    private static async Task<int> RunDecryptAsync(string[] args)
    {
        var options = CommandOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintDecryptUsage();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.InputFile))
        {
            PrintDecryptUsage();
            return Fail("input file is required via -i or --input-file");
        }

        var password = await ResolvePasswordAsync(options.Password);
        var salt = await ResolveSaltAsync(options.SaltHex);
        var encoding = ResolveFilenameEncoding(options.FilenameEncoding);

        var output = options.OutputFile;
        if (string.IsNullOrWhiteSpace(output))
        {
            output = DeriveDecryptOutput(options.InputFile, password, salt, encoding);
            Console.Error.WriteLine($"Derived output filename: {output}");
        }

        Console.Error.WriteLine($"Decrypting {options.InputFile} -> {output} ...");
        await RcloneCrypt.DecryptFileAsync(options.InputFile, output, password, salt);
        Console.Error.WriteLine("Done.");
        return 0;
    }

    private static string DeriveEncryptOutput(string inputPath, string password, byte[]? salt, FilenameEncoding encoding)
    {
        var fileName = Path.GetFileName(inputPath);
        var directory = Path.GetDirectoryName(inputPath);
        var key = RcloneCrypt.DeriveKey(password, salt);
        var encryptedFile = FilenameCrypt.EncryptFileName(key.NameKey, key.NameTweak, fileName, encoding);
        return string.IsNullOrWhiteSpace(directory) ? encryptedFile : Path.Combine(directory, encryptedFile);
    }

    private static string DeriveDecryptOutput(string inputPath, string password, byte[]? salt, FilenameEncoding encoding)
    {
        var fileName = Path.GetFileName(inputPath);
        var directory = Path.GetDirectoryName(inputPath);
        var key = RcloneCrypt.DeriveKey(password, salt);
        var decryptedFile = FilenameCrypt.DecryptFileName(key.NameKey, key.NameTweak, fileName, encoding);
        return string.IsNullOrWhiteSpace(directory) ? decryptedFile : Path.Combine(directory, decryptedFile);
    }

    private static async Task<string> ResolvePasswordAsync(string? fromFlag)
    {
        if (!string.IsNullOrWhiteSpace(fromFlag))
        {
            Console.Error.WriteLine("WARNING: Using --password on the command line is insecure.");
            Console.Error.WriteLine("         The password is visible in process listings and shell history.");
            Console.Error.WriteLine("         Use RCLONE_ENCRYPT_PASSWORD environment variable instead,");
            Console.Error.WriteLine("         or omit --password to be prompted securely.");
            Console.Error.WriteLine("         If you must use --password, wipe your terminal history afterwards.");
            return fromFlag;
        }

        var fromEnv = Environment.GetEnvironmentVariable("RCLONE_ENCRYPT_PASSWORD");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        Console.Error.Write("Password: ");
        var password = await ReadSecretLineAsync();
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("password cannot be empty");
        }

        return password;
    }

    private static async Task<byte[]?> ResolveSaltAsync(string? fromFlag)
    {
        var fromEnv = Environment.GetEnvironmentVariable("RCLONE_ENCRYPT_SALT");
        var raw = string.IsNullOrWhiteSpace(fromFlag) ? fromEnv : fromFlag;

        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.Error.Write("Salt (optional, press Enter for default): ");
            raw = (await Console.In.ReadLineAsync())?.Trim();
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return Convert.FromHexString(raw);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"invalid salt hex: {ex.Message}");
        }
    }

    private static FilenameEncoding ResolveFilenameEncoding(string? value)
    {
        var env = Environment.GetEnvironmentVariable("RCLONE_ENCRYPT_FILENAME_ENCODING");
        var raw = string.IsNullOrWhiteSpace(value) ? env : value;
        return FilenameEncodingParser.ParseOrDefault(raw);
    }

    private static async Task<string> ReadSecretLineAsync()
    {
        if (Console.IsInputRedirected)
        {
            var line = await Console.In.ReadLineAsync();
            Console.Error.WriteLine();
            return line?.Trim() ?? string.Empty;
        }

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
            }
        }

        return new string(chars.ToArray());
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            Usage: rclone-encrypt-test-chatgpt-csharp <command> [options]

            Encrypt and decrypt files using rclone-compatible encryption.

            Commands:
              encrypt       Encrypt a file
              decrypt       Decrypt a file
              generate-salt Generate a random 16-byte salt (hex-encoded)
              version       Print version

            Use 'rclone-encrypt-test-chatgpt-csharp <command> --help' for command-specific options.
            """);
    }

    private static void PrintEncryptUsage()
    {
        Console.Error.WriteLine(
            """
            Usage: rclone-encrypt-test-chatgpt-csharp encrypt [options]

            Options:
              --password             Password (WARNING: insecure - use env var RCLONE_ENCRYPT_PASSWORD instead, or omit to be prompted)
              --salt                 Optional hex-encoded salt (omit to use rclone default salt; also via RCLONE_ENCRYPT_SALT)
              --filename-encoding    Filename encoding for encrypted filenames: base32 (default) or base64
              -i, --input-file       Input file path (required)
              -o, --output-file      Output file path (optional, derived if omitted)
            """);
    }

    private static void PrintDecryptUsage()
    {
        Console.Error.WriteLine(
            """
            Usage: rclone-encrypt-test-chatgpt-csharp decrypt [options]

            Options:
              --password             Password (WARNING: insecure - use env var RCLONE_ENCRYPT_PASSWORD instead, or omit to be prompted)
              --salt                 Optional hex-encoded salt (omit to use rclone default salt; also via RCLONE_ENCRYPT_SALT)
              --filename-encoding    Filename encoding for encrypted filenames: base32 (default) or base64
              -i, --input-file       Input file path (required)
              -o, --output-file      Output file path (optional, derived if omitted)
            """);
    }

    private sealed record CommandOptions(
        string? InputFile,
        string? OutputFile,
        string? Password,
        string? SaltHex,
        string? FilenameEncoding,
        bool ShowHelp)
    {
        public static CommandOptions Parse(IEnumerable<string> args)
        {
            var inputFile = default(string);
            var outputFile = default(string);
            var password = default(string);
            var saltHex = default(string);
            var filenameEncoding = default(string);
            var showHelp = false;

            var list = args.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var arg = list[i];
                switch (arg)
                {
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    case "-i":
                    case "--input-file":
                        inputFile = GetValue(list, ref i, arg);
                        break;
                    case "-o":
                    case "--output-file":
                        outputFile = GetValue(list, ref i, arg);
                        break;
                    case "--password":
                        password = GetValue(list, ref i, arg);
                        break;
                    case "--salt":
                        saltHex = GetValue(list, ref i, arg);
                        break;
                    case "--filename-encoding":
                        filenameEncoding = GetValue(list, ref i, arg);
                        break;
                    default:
                        throw new InvalidOperationException($"unknown option: {arg}");
                }
            }

            return new CommandOptions(inputFile, outputFile, password, saltHex, filenameEncoding, showHelp);
        }

        private static string GetValue(IReadOnlyList<string> args, ref int index, string flag)
        {
            var valueIndex = index + 1;
            if (valueIndex >= args.Count)
            {
                throw new InvalidOperationException($"missing value for {flag}");
            }

            index = valueIndex;
            return args[valueIndex];
        }
    }
}

internal enum FilenameEncoding
{
    Base32,
    Base64
}

internal static class FilenameEncodingParser
{
    public static FilenameEncoding ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FilenameEncoding.Base32;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "base32" => FilenameEncoding.Base32,
            "base64" => FilenameEncoding.Base64,
            _ => throw new InvalidOperationException($"unknown filename encoding: \"{value}\" (supported: base32, base64)")
        };
    }
}

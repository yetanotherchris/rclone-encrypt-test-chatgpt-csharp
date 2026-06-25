# rclone-encrypt-test-chatgpt-csharp

A small CLI tool that encrypts and decrypts using the rclone encryption defaults.

Rclone uses a custom salt if no salt is provided, which this tool will use by default. A few similar tools:

- https://github.com/rclone/rclone
- https://github.com/mcolatosti/rclonedecrypt
- https://github.com/br0kenpixel/rclone-rcc
- @fyears/rclone-crypt

## Installation

**Homebrew (macOS/Linux)**

```bash
brew tap yetanotherchris/rclone-encrypt-test-chatgpt-csharp https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp
brew install rclone-encrypt-test-chatgpt-csharp
```

**Scoop (Windows)**

```powershell
scoop bucket add rclone-encrypt-test-chatgpt-csharp https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp
scoop install rclone-encrypt-test-chatgpt-csharp
```

## Examples usage

### Basic encrypt/decrypt

```bash
# Encrypt a file (prompts for password and optional salt)
rclone-encrypt-test-chatgpt-csharp encrypt -i ./document.txt -o ./document.txt.encrypted

# Decrypt a file
rclone-encrypt-test-chatgpt-csharp decrypt -i ./document.txt.encrypted -o ./document.txt
```

### Encrypt/decrypt with custom salt

```bash
rclone-encrypt-test-chatgpt-csharp encrypt --password "Testpassword1" --salt "deadbeefdeadbeefdeadbeefdeadbeef" -i ./input.txt -o ./output.bin
rclone-encrypt-test-chatgpt-csharp decrypt --password "Testpassword1" --salt "deadbeefdeadbeefdeadbeefdeadbeef" -i ./output.bin -o ./input.txt
```

### Use environment variables (recommended for password)

```bash
export RCLONE_ENCRYPT_PASSWORD="Testpassword1"
export RCLONE_ENCRYPT_SALT="deadbeefdeadbeefdeadbeefdeadbeef"
rclone-encrypt-test-chatgpt-csharp encrypt -i ./input.txt -o ./output.bin
rclone-encrypt-test-chatgpt-csharp decrypt -i ./output.bin -o ./input.txt
```

### Automatic filename encryption/decryption (output optional)

When `-o`/`--output-file` is omitted, the CLI encrypts or decrypts the filename using AES-EME.

```bash
rclone-encrypt-test-chatgpt-csharp encrypt --password "Testpassword1" -i ./TEST_FILE.txt
rclone-encrypt-test-chatgpt-csharp decrypt --password "Testpassword1" -i ./kr9tu4e1da4u3nifdd99g9tf5o
```

### Custom filename encoding (base32 or base64)

```bash
rclone-encrypt-test-chatgpt-csharp encrypt --password "Testpassword1" --filename-encoding base64 -i ./TEST_FILE.txt
rclone-encrypt-test-chatgpt-csharp decrypt --password "Testpassword1" --filename-encoding base64 -i ./Iyxcijgc9bp3o5Y0npW6xqUvwWNcc3MA4SadB0sR6cY
```

### About `--password`

Using `--password` works, but the CLI prints a warning because command-line arguments can be exposed in process listings and shell history.

If you use it anyway, prefer temporary shell sessions, environment variables, and clear your history entry after running the command.

## Details

Rclone encryption uses:

- NaCl SecretBox (XSalsa20 + Poly1305) for file contents.
- AES-EME for filenames.
- scrypt (N=16384, r=8, p=1) for key material derivation.

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--password` | prompted | Password (warning shown if used on command line) |
| `--salt` | rclone default salt | Optional hex-encoded salt |
| `--filename-encoding` | `base32` | Filename encoding for filename encrypt/decrypt (`base32`, `base64`) |
| `-i`, `--input-file` | required | Input file path |
| `-o`, `--output-file` | auto-derived | Optional output file path |

## Building from source

Requires .NET 10 SDK.

```bash
git clone https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp
cd rclone-encrypt-test-chatgpt-csharp
dotnet build
dotnet test
```

## Releases

Pushing a `vX.Y.Z` tag triggers the [Build and Release workflow](.github/workflows/build-release.yml), which publishes self-contained binaries for Linux/macOS/Windows, creates a GitHub Release, and updates the Scoop manifest (`rclone-encrypt-test-chatgpt-csharp.json`) and Homebrew formula (`Formula/rclone-encrypt-test-chatgpt-csharp.rb`).

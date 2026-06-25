class RcloneEncryptTestChatgptCsharp < Formula
  desc "CLI for rclone-compatible file and filename encryption"
  homepage "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp"
  version "0.0.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v0.0.0/rclone-encrypt-test-chatgpt-csharp-darwin-arm64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    else
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v0.0.0/rclone-encrypt-test-chatgpt-csharp-darwin-amd64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v0.0.0/rclone-encrypt-test-chatgpt-csharp-linux-arm64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    else
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v0.0.0/rclone-encrypt-test-chatgpt-csharp-linux-amd64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  def install
    bin.install "rclone-encrypt-test-chatgpt-csharp-darwin-arm64" => "rclone-encrypt-test-chatgpt-csharp" if OS.mac? && Hardware::CPU.arm?
    bin.install "rclone-encrypt-test-chatgpt-csharp-darwin-amd64" => "rclone-encrypt-test-chatgpt-csharp" if OS.mac? && !Hardware::CPU.arm?
    bin.install "rclone-encrypt-test-chatgpt-csharp-linux-arm64" => "rclone-encrypt-test-chatgpt-csharp" if OS.linux? && Hardware::CPU.arm?
    bin.install "rclone-encrypt-test-chatgpt-csharp-linux-amd64" => "rclone-encrypt-test-chatgpt-csharp" if OS.linux? && !Hardware::CPU.arm?
  end

  test do
    assert_match "rclone-encrypt-test-chatgpt-csharp #{version}", shell_output("#{bin}/rclone-encrypt-test-chatgpt-csharp --version")
  end
end

class RcloneEncryptTestChatgptCsharp < Formula
  desc "CLI for rclone-compatible file and filename encryption"
  homepage "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp"
  version "1.0.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v1.0.0/rclone-encrypt-test-chatgpt-csharp-darwin-arm64.tar.gz"
      sha256 "3ba88969819e0143440cb9212b1b3b91f6db64aa3c27ff7798eb20e503206bc2"
    else
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v1.0.0/rclone-encrypt-test-chatgpt-csharp-darwin-amd64.tar.gz"
      sha256 "5e911b069ad6780f16b62a8bb7fea432377d48a5510ceda9a278eb29b1c318ba"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v1.0.0/rclone-encrypt-test-chatgpt-csharp-linux-arm64.tar.gz"
      sha256 "805b2a30c32a7d97ad2c0cee9c51233ac53c65179cfda63bcad8e78196c7e205"
    else
      url "https://github.com/yetanotherchris/rclone-encrypt-test-chatgpt-csharp/releases/download/v1.0.0/rclone-encrypt-test-chatgpt-csharp-linux-amd64.tar.gz"
      sha256 "be7b04890f5c6504e5b4e2a988bcd50ddd710f1dbbb416f46dd5681419ad5e9a"
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
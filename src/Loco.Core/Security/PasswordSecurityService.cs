using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Loco.Core.Security
{
    /// <summary>
    /// パスワードセキュリティサービス - P0項目#7,#8
    /// 複雑性要件、bcrypt暗号化、アカウントロックアウト対策
    /// </summary>
    public class PasswordSecurityService
    {
        private readonly ILogger<PasswordSecurityService> _logger;
        private readonly PasswordConfiguration _config;
        private readonly Dictionary<string, AccountLockInfo> _lockoutTracker;
        private readonly object _lockObject = new object();

        // 一般的な脆弱パスワードリスト（上位100）
        private static readonly HashSet<string> CommonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "123456", "password123", "admin", "qwerty", "12345678", "123456789",
            "letmein", "1234567890", "football", "iloveyou", "admin123", "welcome", "monkey",
            "login", "abc123", "starwars", "123123", "dragon", "passw0rd", "master", "hello",
            "freedom", "whatever", "qazwsx", "trustno1", "654321", "jordan23", "harley",
            "password1", "1234", "robert", "matthew", "jordan", "michelle", "mindy", "patrick",
            "123abc", "andrew", "joshua", "1qaz2wsx", "qwertyuiop", "asdfghjkl", "zxcvbnm"
        };

        // パスワード強度評価パターン
        private static readonly Dictionary<Regex, int> StrengthPatterns = new Dictionary<Regex, int>
        {
            { new Regex(@"[a-z]", RegexOptions.Compiled), 1 },              // 小文字
            { new Regex(@"[A-Z]", RegexOptions.Compiled), 1 },              // 大文字
            { new Regex(@"[0-9]", RegexOptions.Compiled), 1 },              // 数字
            { new Regex(@"[^a-zA-Z0-9]", RegexOptions.Compiled), 1 },       // 特殊文字
            { new Regex(@".{12,}", RegexOptions.Compiled), 2 },             // 長さ12以上
            { new Regex(@"(.)\1{2,}", RegexOptions.Compiled), -2 },         // 同じ文字の連続（減点）
            { new Regex(@"(012|123|234|345|456|567|678|789|890)", RegexOptions.Compiled), -1 }, // 連続数字
            { new Regex(@"(abc|bcd|cde|def|efg|fgh|ghi|hij|ijk|jkl|klm|lmn|mno|nop|opq|pqr|qrs|rst|stu|tuv|uvw|vwx|wxy|xyz)", RegexOptions.IgnoreCase | RegexOptions.Compiled), -1 } // 連続アルファベット
        };

        public PasswordSecurityService(
            ILogger<PasswordSecurityService> logger = null,
            PasswordConfiguration config = null)
        {
            _logger = logger;
            _config = config ?? new PasswordConfiguration();
            _lockoutTracker = new Dictionary<string, AccountLockInfo>();
        }

        /// <summary>
        /// パスワード複雑性要件の検証 - P0項目#7
        /// </summary>
        public PasswordValidationResult ValidatePassword(string password, string username = null)
        {
            var result = new PasswordValidationResult();

            try
            {
                if (string.IsNullOrEmpty(password))
                {
                    result.AddError("Password is required");
                    return result;
                }

                // 基本的な長さ要件
                if (password.Length < _config.MinLength)
                {
                    result.AddError($"Password must be at least {_config.MinLength} characters long");
                }

                if (password.Length > _config.MaxLength)
                {
                    result.AddError($"Password must not exceed {_config.MaxLength} characters");
                }

                // 文字種別要件
                var hasLower = password.Any(char.IsLower);
                var hasUpper = password.Any(char.IsUpper);
                var hasDigit = password.Any(char.IsDigit);
                var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

                if (_config.RequireLowercase && !hasLower)
                    result.AddError("Password must contain at least one lowercase letter");

                if (_config.RequireUppercase && !hasUpper)
                    result.AddError("Password must contain at least one uppercase letter");

                if (_config.RequireDigit && !hasDigit)
                    result.AddError("Password must contain at least one digit");

                if (_config.RequireSpecialCharacter && !hasSpecial)
                    result.AddError("Password must contain at least one special character");

                // 禁止文字のチェック
                if (_config.ForbiddenCharacters?.Any(c => password.Contains(c)) == true)
                {
                    result.AddError("Password contains forbidden characters");
                }

                // ユーザー名との類似性チェック
                if (!string.IsNullOrEmpty(username) && _config.CheckUsernameSimilarity)
                {
                    if (password.Contains(username, StringComparison.OrdinalIgnoreCase) ||
                        username.Contains(password, StringComparison.OrdinalIgnoreCase))
                    {
                        result.AddError("Password must not contain the username");
                    }
                }

                // 一般的なパスワードチェック
                if (_config.CheckCommonPasswords && CommonPasswords.Contains(password))
                {
                    result.AddError("Password is too common and easily guessed");
                }

                // 辞書攻撃対策
                if (_config.CheckDictionaryWords && IsDictionaryWord(password))
                {
                    result.AddError("Password should not be a common dictionary word");
                }

                // キーボードパターンチェック
                if (_config.CheckKeyboardPatterns && HasKeyboardPattern(password))
                {
                    result.AddError("Password should not follow keyboard patterns");
                }

                // パスワード強度計算
                result.Strength = CalculatePasswordStrength(password);
                result.StrengthLevel = GetStrengthLevel(result.Strength);

                // 最小強度要件
                if (result.Strength < _config.MinStrengthScore)
                {
                    result.AddError($"Password strength is too weak (score: {result.Strength}, required: {_config.MinStrengthScore})");
                }

                result.IsValid = result.Errors.Count == 0;

                if (!result.IsValid)
                {
                    _logger?.LogDebug("Password validation failed with {ErrorCount} errors", result.Errors.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Password validation error");
                result.AddError("Password validation failed");
                return result;
            }
        }

        /// <summary>
        /// bcryptによるセキュアなパスワードハッシュ化 - P0項目#35
        /// </summary>
        public string HashPassword(string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new ArgumentException("Password cannot be null or empty", nameof(password));
                }

                // bcryptでハッシュ化（work factor = 12, 約250ms）
                return BCrypt.Net.BCrypt.HashPassword(password, _config.BcryptWorkFactor);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Password hashing failed");
                throw new SecurityException("Password hashing failed", ex);
            }
        }

        /// <summary>
        /// パスワード検証
        /// </summary>
        public bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
                {
                    return false;
                }

                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Password verification failed");
                return false;
            }
        }

        /// <summary>
        /// アカウントロックアウト管理 - P0項目#8
        /// </summary>
        public bool IsAccountLocked(string userId)
        {
            lock (_lockObject)
            {
                if (!_lockoutTracker.TryGetValue(userId, out var lockInfo))
                {
                    return false;
                }

                // ロックアウト期間が過ぎた場合は自動解除
                if (DateTime.UtcNow > lockInfo.LockedUntil)
                {
                    _lockoutTracker.Remove(userId);
                    _logger?.LogInformation("Account lockout expired for user {UserId}", userId);
                    return false;
                }

                return lockInfo.IsLocked;
            }
        }

        /// <summary>
        /// 失敗ログイン試行の記録
        /// </summary>
        public void RecordFailedLogin(string userId, string ipAddress = null)
        {
            lock (_lockObject)
            {
                if (!_lockoutTracker.TryGetValue(userId, out var lockInfo))
                {
                    lockInfo = new AccountLockInfo
                    {
                        UserId = userId,
                        FailedAttempts = 0,
                        FirstFailedAttempt = DateTime.UtcNow
                    };
                    _lockoutTracker[userId] = lockInfo;
                }

                lockInfo.FailedAttempts++;
                lockInfo.LastFailedAttempt = DateTime.UtcNow;
                lockInfo.LastIpAddress = ipAddress;

                // 失敗試行回数が閾値を超えた場合はロック
                if (lockInfo.FailedAttempts >= _config.MaxFailedAttempts)
                {
                    var lockoutDuration = CalculateLockoutDuration(lockInfo.FailedAttempts);
                    lockInfo.IsLocked = true;
                    lockInfo.LockedAt = DateTime.UtcNow;
                    lockInfo.LockedUntil = DateTime.UtcNow.Add(lockoutDuration);

                    _logger?.LogWarning("Account locked for user {UserId} after {FailedAttempts} failed attempts | IP: {IpAddress}",
                        userId, lockInfo.FailedAttempts, ipAddress);
                }
                else
                {
                    _logger?.LogDebug("Failed login recorded for user {UserId} ({FailedAttempts}/{MaxAttempts}) | IP: {IpAddress}",
                        userId, lockInfo.FailedAttempts, _config.MaxFailedAttempts, ipAddress);
                }
            }
        }

        /// <summary>
        /// 成功ログイン後の失敗カウントリセット
        /// </summary>
        public void RecordSuccessfulLogin(string userId)
        {
            lock (_lockObject)
            {
                if (_lockoutTracker.TryGetValue(userId, out var lockInfo))
                {
                    lockInfo.FailedAttempts = 0;
                    lockInfo.LastSuccessfulLogin = DateTime.UtcNow;

                    if (lockInfo.IsLocked)
                    {
                        lockInfo.IsLocked = false;
                        lockInfo.LockedUntil = null;
                        _logger?.LogInformation("Account unlocked for user {UserId} after successful login", userId);
                    }
                }
            }
        }

        /// <summary>
        /// 管理者による手動アカウントロック解除
        /// </summary>
        public bool UnlockAccount(string userId, string adminUserId)
        {
            lock (_lockObject)
            {
                if (_lockoutTracker.TryGetValue(userId, out var lockInfo) && lockInfo.IsLocked)
                {
                    lockInfo.IsLocked = false;
                    lockInfo.LockedUntil = null;
                    lockInfo.FailedAttempts = 0;

                    _logger?.LogWarning("Account manually unlocked for user {UserId} by admin {AdminUserId}",
                        userId, adminUserId);

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// セキュアなパスワード生成
        /// </summary>
        public string GenerateSecurePassword(int length = 16)
        {
            if (length < _config.MinLength)
                length = _config.MinLength;

            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var chars = new List<char>();
            using var rng = RandomNumberGenerator.Create();

            // 各文字種から最低1文字は含める
            if (_config.RequireLowercase) chars.Add(GetRandomChar(lowercase, rng));
            if (_config.RequireUppercase) chars.Add(GetRandomChar(uppercase, rng));
            if (_config.RequireDigit) chars.Add(GetRandomChar(digits, rng));
            if (_config.RequireSpecialCharacter) chars.Add(GetRandomChar(special, rng));

            // 残りの文字をランダムに追加
            var allChars = "";
            if (_config.RequireLowercase) allChars += lowercase;
            if (_config.RequireUppercase) allChars += uppercase;
            if (_config.RequireDigit) allChars += digits;
            if (_config.RequireSpecialCharacter) allChars += special;

            while (chars.Count < length)
            {
                chars.Add(GetRandomChar(allChars, rng));
            }

            // シャッフル
            for (int i = chars.Count - 1; i > 0; i--)
            {
                var randomBytes = new byte[4];
                rng.GetBytes(randomBytes);
                var j = Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }

        /// <summary>
        /// パスワード強度の詳細分析
        /// </summary>
        public PasswordAnalysis AnalyzePassword(string password)
        {
            var analysis = new PasswordAnalysis
            {
                Length = password?.Length ?? 0,
                HasLowercase = password?.Any(char.IsLower) == true,
                HasUppercase = password?.Any(char.IsUpper) == true,
                HasDigits = password?.Any(char.IsDigit) == true,
                HasSpecialCharacters = password?.Any(c => !char.IsLetterOrDigit(c)) == true,
                IsCommonPassword = password != null && CommonPasswords.Contains(password),
                HasKeyboardPattern = password != null && HasKeyboardPattern(password),
                HasRepeatingCharacters = password != null && HasRepeatingCharacters(password),
                EstimatedCrackTime = EstimateCrackTime(password),
                Entropy = CalculateEntropy(password)
            };

            analysis.Strength = CalculatePasswordStrength(password);
            analysis.StrengthLevel = GetStrengthLevel(analysis.Strength);

            return analysis;
        }

        // プライベートメソッド

        private int CalculatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return 0;

            var score = 0;

            // パターンマッチングによるスコア計算
            foreach (var pattern in StrengthPatterns)
            {
                if (pattern.Key.IsMatch(password))
                {
                    score += pattern.Value;
                }
            }

            // 長さボーナス
            score += Math.Min(password.Length / 4, 5);

            // ユニーク文字の比率
            var uniqueChars = password.Distinct().Count();
            score += (uniqueChars * 2) / password.Length;

            return Math.Max(0, score);
        }

        private PasswordStrengthLevel GetStrengthLevel(int score)
        {
            return score switch
            {
                < 3 => PasswordStrengthLevel.VeryWeak,
                < 6 => PasswordStrengthLevel.Weak,
                < 10 => PasswordStrengthLevel.Fair,
                < 15 => PasswordStrengthLevel.Strong,
                _ => PasswordStrengthLevel.VeryStrong
            };
        }

        private bool IsDictionaryWord(string password)
        {
            // 簡易版：英語の一般的な単語をチェック
            var commonWords = new[] { "password", "welcome", "admin", "user", "login", "system", "computer", "internet", "security" };
            return commonWords.Any(word => password.Contains(word, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasKeyboardPattern(string password)
        {
            var keyboardPatterns = new[]
            {
                "qwerty", "asdf", "zxcv", "1234", "abcd",
                "qwertyuiop", "asdfghjkl", "zxcvbnm"
            };

            return keyboardPatterns.Any(pattern => 
                password.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasRepeatingCharacters(string password)
        {
            return Regex.IsMatch(password, @"(.)\1{2,}");
        }

        private TimeSpan CalculateLockoutDuration(int failedAttempts)
        {
            // 指数バックオフ: 初回15分、以降倍増（最大24時間）
            var minutes = Math.Min(15 * Math.Pow(2, failedAttempts - _config.MaxFailedAttempts), 1440);
            return TimeSpan.FromMinutes(minutes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char GetRandomChar(string chars, RandomNumberGenerator rng)
        {
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var index = Math.Abs(BitConverter.ToInt32(bytes, 0)) % chars.Length;
            return chars[index];
        }

        private string EstimateCrackTime(string password)
        {
            if (string.IsNullOrEmpty(password))
                return "Instantly";

            var entropy = CalculateEntropy(password);
            var combinations = Math.Pow(2, entropy);
            
            // 毎秒10億回の試行を仮定
            var secondsToCrack = combinations / (1_000_000_000 * 2); // 平均で半分の時間

            if (secondsToCrack < 1) return "Less than 1 second";
            if (secondsToCrack < 60) return $"{secondsToCrack:F0} seconds";
            if (secondsToCrack < 3600) return $"{secondsToCrack/60:F0} minutes";
            if (secondsToCrack < 86400) return $"{secondsToCrack/3600:F0} hours";
            if (secondsToCrack < 31536000) return $"{secondsToCrack/86400:F0} days";
            
            return $"{secondsToCrack/31536000:F0} years";
        }

        private double CalculateEntropy(string password)
        {
            if (string.IsNullOrEmpty(password))
                return 0;

            var hasLower = password.Any(char.IsLower);
            var hasUpper = password.Any(char.IsUpper);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

            var charsetSize = 0;
            if (hasLower) charsetSize += 26;
            if (hasUpper) charsetSize += 26;
            if (hasDigit) charsetSize += 10;
            if (hasSpecial) charsetSize += 32; // 一般的な特殊文字の数

            return password.Length * Math.Log2(charsetSize);
        }
    }

    // サポートクラス

    public class PasswordConfiguration
    {
        public int MinLength { get; set; } = 12;
        public int MaxLength { get; set; } = 128;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialCharacter { get; set; } = true;
        public bool CheckCommonPasswords { get; set; } = true;
        public bool CheckDictionaryWords { get; set; } = true;
        public bool CheckKeyboardPatterns { get; set; } = true;
        public bool CheckUsernameSimilarity { get; set; } = true;
        public int MaxFailedAttempts { get; set; } = 5;
        public int BcryptWorkFactor { get; set; } = 12;
        public int MinStrengthScore { get; set; } = 8;
        public char[] ForbiddenCharacters { get; set; } = { '\0', '\r', '\n', '\t' };
    }

    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public int Strength { get; set; }
        public PasswordStrengthLevel StrengthLevel { get; set; }

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }

    public class PasswordAnalysis
    {
        public int Length { get; set; }
        public bool HasLowercase { get; set; }
        public bool HasUppercase { get; set; }
        public bool HasDigits { get; set; }
        public bool HasSpecialCharacters { get; set; }
        public bool IsCommonPassword { get; set; }
        public bool HasKeyboardPattern { get; set; }
        public bool HasRepeatingCharacters { get; set; }
        public string EstimatedCrackTime { get; set; }
        public double Entropy { get; set; }
        public int Strength { get; set; }
        public PasswordStrengthLevel StrengthLevel { get; set; }
    }

    public class AccountLockInfo
    {
        public string UserId { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime FirstFailedAttempt { get; set; }
        public DateTime LastFailedAttempt { get; set; }
        public DateTime? LastSuccessfulLogin { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? LockedUntil { get; set; }
        public string LastIpAddress { get; set; }
    }

    public enum PasswordStrengthLevel
    {
        VeryWeak,
        Weak,
        Fair,
        Strong,
        VeryStrong
    }
}
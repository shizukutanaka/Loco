using System;

namespace Loco.Core.RateLimiting
{
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int Remaining { get; set; }
        public DateTime ResetTime { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RateLimitStatus
    {
        public int Remaining { get; set; }
        public DateTime ResetTime { get; set; }
        public bool IsLimited { get; set; }
    }
}
using System;
using System.Collections.Generic;

namespace MatchingEngine.Models.Brokerage
{
    public class Airdrop
    {
        public string Id { get; set; }
        public string CurrencyCode { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Send email to users or not</summary>
        public bool NotifyUsers { get; set; } = false;

        /// <summary>Airdrop will only apply to users who registered later</summary>
        public DateTimeOffset? MinRegisterDate { get; set; }

        /// <summary>
        /// Airdrop will only apply to these specific users.
        /// Can't be saved in Brokerage.
        /// If null then users will be requested from UserProfiles
        /// </summary>
        public List<string> UserIds { get; set; } = null;

        public override string ToString() => $"Airdrop({Id}, {Amount} {CurrencyCode}, UserIds:{UserIds?.Count}" +
            $"{(MinRegisterDate.HasValue ? $"MinRegisterDate:{MinRegisterDate}" : "")})";
    }
}

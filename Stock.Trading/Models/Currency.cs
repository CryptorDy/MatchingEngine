using System.ComponentModel.DataAnnotations;

namespace MatchingEngine.Models
{
    public class Currency
    {
        /// <summary>
        /// Currency code
        /// </summary>
        [Key]
        public string Code { get; set; }

        /// <summary>
        /// Currency value
        /// </summary>
        [Required]
        public string Value { get; set; }

        /// <summary>
        /// Is currency halted
        /// </summary>
        [Required]
        public bool IsHalted { get; set; }

        /// <summary>
        /// Is currency fiat
        /// </summary>
        public bool IsFiat { get; set; } = false;

        /// <summary>
        /// Is currency showed for users
        /// </summary>
        public bool IsShowedForUsers { get; set; }

        /// <summary>
        /// Is this currency used by users to pay commissions (can be only one such currency)
        /// </summary>
        public bool IsForCommissionConversion { get; set; }

        /// <summary>
        /// Token of which currency (null if not a token)
        /// </summary>
        public string TokenOf { get; set; } = null;

        /// <summary>
        /// Is erc20 token contract address
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// How many decimal digits should balances have after rounding
        /// </summary>
        public int Digits { get; set; }

        /// <summary>
        /// Get gateway serviceId for crypto-adapter of this currency
        /// </summary>
        public string CryptoAdapterId => (TokenOf ?? Code).ToLower();
    }
}

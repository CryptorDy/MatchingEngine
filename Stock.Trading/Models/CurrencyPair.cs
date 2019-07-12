using System.ComponentModel.DataAnnotations;

namespace MatchingEngine.Models
{
    /// <summary>
    /// Валютная пара
    /// </summary>
    public class CurrencyPair
    {
        [Key]
        public string Code { get; set; }

        /// <summary>
        /// Is currency pair halted
        /// </summary>
        [Required]
        public bool IsHalted { get; set; }

        /// <summary>
        /// Идентификатор валюты, из которой осуществляется покупка/продажа
        /// </summary>
        [Required]
        public string CurrencyFromId { get; set; }

        public virtual Currency CurrencyFrom { get; set; }

        /// <summary>
        /// Идентификатор валюты, которую покупают/продают
        /// </summary>
        [Required]
        public string CurrencyToId { get; set; }

        public virtual Currency CurrencyTo { get; set; }

        private CurrencyPair(string code, string currencyFromId, string currencyToId)
        {
            Code = code;
            CurrencyFromId = currencyFromId;
            CurrencyToId = currencyToId;
        }

        public CurrencyPair()
        {
        }
    }
}

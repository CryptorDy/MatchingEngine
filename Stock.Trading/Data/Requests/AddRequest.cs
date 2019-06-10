using System;

namespace Stock.Trading.Requests
{
    public class AddRequest
    {
        /// <summary>
        /// Количество "выбранной валюты"
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Цена за "выбранную валюту"
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Дата и время выставления ордера
        /// </summary>
        public DateTime OrderDateUtc { get; set; }

        /// <summary>
        /// Пользователь, выставивший ордер
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Busines process id,  order Id
        /// </summary>
        public string ActionId { get; set; }

        /// <summary>
        /// Валютная пара
        /// </summary>
        public string CurrencyPariId { get; set; }

        /// <summary>
        /// Original order exchange (0 - our exchange)
        /// </summary>
        public int ExchangeId { get; set; } = 0;

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        public bool FromInnerTradingBot { get; set; } = false;
    }
}

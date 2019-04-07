namespace Stock.Trading.Requests
{
    public class Order
    {
        /// <summary>
        ///
        /// </summary>
        public bool IsBid { get; set; }

        /// <summary>
        /// Количество "выбранной валюты"
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Цена за "выбранную валюту"
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Пользователь, выставивший ордер
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string TradingOrderId { get; set; }
    }
}

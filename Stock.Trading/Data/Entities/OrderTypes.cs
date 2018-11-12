using System.ComponentModel.DataAnnotations;

namespace Stock.Trading.Data.Entities
{
    /// <summary>
    /// Тип ордера
    /// </summary>
    public class OrderType
    {
        /// <summary>
        /// Code
        /// </summary>
        [Key]
        public string Code { get; set; }

        /// <summary>
        /// Order type
        /// </summary>
        [Required]
        public string Name { get; set; }

        public static readonly OrderType Active = new OrderType("Active", "Активна");
        public static readonly OrderType Canceled = new OrderType("Canceled", "Отменена");
        public static readonly OrderType Blocked = new OrderType("Blocked", "Заблокирована");
        public static readonly OrderType Completed = new OrderType("Completed", "Совершена");

        private OrderType(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public OrderType()
        {
        }
    }
}
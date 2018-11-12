namespace Stock.Trading.Data
{
    public class Currency
    {
        public string Code { get; set; }

        public string Value { get; set; }

        private Currency(string code, string value)
        {
            Code = code;
            Value = value;
        }

        public Currency()
        {
        }
    }
}
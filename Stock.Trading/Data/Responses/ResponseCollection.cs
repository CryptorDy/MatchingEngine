using System.Collections.Generic;

namespace Stock.Trading.Responses
{
    public class ResponseCollection<T> where T : class, new()
    {
        public List<T> Items { get; set; } = new List<T>();
    }
}
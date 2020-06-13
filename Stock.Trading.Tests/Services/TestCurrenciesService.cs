using MatchingEngine.Models;
using MatchingEngine.Services;
using System.Collections.Generic;

namespace Stock.Trading.Tests
{
    public class TestCurrenciesService : CurrenciesService
    {
        public TestCurrenciesService() : base(null, null)
        {
        }

        public void SetValues(List<Currency> currencies, List<CurrencyPair> currencyPairs)
        {
            _currencies = currencies;
            _currencyPairs = currencyPairs;
        }
    }
}

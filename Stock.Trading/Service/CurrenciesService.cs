using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public interface ICurrenciesService
    {
        List<Currency> GetCurrencies();

        Currency GetCurrency(string code);

        CurrencyPair GetCurrencyPair(string code);

        List<CurrencyPair> GetCurrencyPairs();

        string GetAdapterId(string currencyCode);

        int GetBalanceDigits(string currencyCode);

        int GetPriceDigits(string currencyPairCode);

        int GetAmountDigits(string currencyPairCode);

        Task LoadData();
    }

    public class CurrenciesService : ICurrenciesService
    {
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        protected List<Currency> _currencies = new List<Currency>();
        protected  List<CurrencyPair> _currencyPairs = new List<CurrencyPair>();

        /// <summary>
        /// Use for rounding commissions, quote amounts, deposits, withdrawals
        /// </summary>
        public const int Digits = 8;

        public CurrenciesService(GatewayHttpClient gatewayHttpClient,
            ILogger<CurrenciesService> logger)
        {
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        #region Getters

        public List<CurrencyPair> GetCurrencyPairs() => _currencyPairs;

        public CurrencyPair GetCurrencyPair(string code) => _currencyPairs.FirstOrDefault(_ => _.Code == code);

        public List<Currency> GetCurrencies() => _currencies;

        public Currency GetCurrency(string code) => _currencies.FirstOrDefault(_ => _.Code == code);

        public string GetAdapterId(string currencyCode) => GetCurrency(currencyCode).CryptoAdapterId;

        public int GetBalanceDigits(string currencyCode) => GetCurrency(currencyCode).Digits;

        public int GetPriceDigits(string currencyPairCode) => GetCurrencyPair(currencyPairCode).DigitsPrice;

        public int GetAmountDigits(string currencyPairCode) => GetCurrencyPair(currencyPairCode).DigitsAmount;

        #endregion Getters

        #region Load methods

        public async Task LoadData()
        {
            var currencyPairs = await LoadCurrencyPairs();
            if (currencyPairs != null)
            {
                _currencyPairs = currencyPairs;
            }

            var currencies = await LoadCurrencies();
            if (currencies != null)
            {
                _currencies = currencies;
            }
        }

        private async Task<List<CurrencyPair>> LoadCurrencyPairs()
        {
            try
            {
                var result = await _gatewayHttpClient.GetAsync<List<CurrencyPair>>("depository/currency-pairs");
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
                return null;
            }
        }

        private async Task<List<Currency>> LoadCurrencies()
        {
            try
            {
                var result = await _gatewayHttpClient.GetAsync<List<Currency>>("depository/currencies");
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
                return null;
            }
        }

        #endregion Load methods
    }
}

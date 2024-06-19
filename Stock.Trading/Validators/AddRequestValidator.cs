using FluentValidation;
using MatchingEngine.Models;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Validators
{
    public class AddRequestValidator : AbstractValidator<OrderCreateRequest>
    {
        public AddRequestValidator()
        {
            RuleFor(request => request.ActionId).NotEmpty();
            RuleFor(request => request.Amount).GreaterThan(0);
            RuleFor(request => request.Price).GreaterThan(0);
            RuleFor(request => request.DateCreated).NotEmpty();
            RuleFor(request => request.UserId).NotEmpty();
            RuleFor(request => request.CurrencyPairCode).NotEmpty();
        }
    }
}

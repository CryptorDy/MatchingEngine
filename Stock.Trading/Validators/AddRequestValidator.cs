using FluentValidation;
using Stock.Trading.Requests;

namespace MatchingEngine.Validators
{
    public class AddRequestValidator : AbstractValidator<AddRequest>
    {
        public AddRequestValidator()
        {
            RuleFor(request => request.Amount).GreaterThan(0);
            RuleFor(request => request.Price).GreaterThan(0);
            RuleFor(request => request.OrderDateUtc).NotEmpty();
            RuleFor(request => request.UserId).NotEmpty();
            RuleFor(request => request.CurrencyPariId).NotEmpty();
        }
    }
}

using FluentValidation;
using Stock.Trading.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            RuleFor(request => request.UserId).NotEmpty();
        }
    }
}

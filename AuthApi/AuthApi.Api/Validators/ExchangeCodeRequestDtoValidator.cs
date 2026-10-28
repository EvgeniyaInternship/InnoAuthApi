using AuthApi.Api.DTOs.Requests;
using FluentValidation;

namespace AuthApi.Api.Validators;

public sealed class ExchangeCodeRequestDtoValidator : AbstractValidator<ExchangeCodeRequestDto>
{
    public ExchangeCodeRequestDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Authorization code cannot be empty.");

        RuleFor(x => x.RedirectUri)
            .NotEmpty()
            .WithMessage("Redirect URI cannot be empty.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("Redirect URI must be a valid absolute URI.");
    }
}
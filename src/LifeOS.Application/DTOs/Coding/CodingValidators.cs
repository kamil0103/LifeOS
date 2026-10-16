using FluentValidation;
namespace LifeOS.Application.DTOs.Coding;
public class SolveProblemRequestValidator : AbstractValidator<SolveProblemRequest>
{
    public SolveProblemRequestValidator()
    {
        RuleFor(r => r.SolutionLanguage).MaximumLength(100);
        RuleFor(r => r.Notes).MaximumLength(5000);
        RuleFor(r => r.TimeSpentMinutes).InclusiveBetween(1, 1440).When(r => r.TimeSpentMinutes.HasValue);
    }
}
public class CreateCodingProblemRequestValidator : AbstractValidator<CreateCodingProblemRequest>
{
    public CreateCodingProblemRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Difficulty).NotEmpty();
        RuleFor(r => r.Platform).MaximumLength(100);
        RuleFor(r => r.Category).MaximumLength(100);
        RuleFor(r => r.Description).MaximumLength(10000);
        RuleFor(r => r.Notes).MaximumLength(5000);
        RuleFor(r => r.Url).MaximumLength(2048);
    }
}
public class UpdateCodingProblemRequestValidator : AbstractValidator<UpdateCodingProblemRequest>
{
    public UpdateCodingProblemRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Difficulty).NotEmpty();
        RuleFor(r => r.Platform).MaximumLength(100);
        RuleFor(r => r.Category).MaximumLength(100);
        RuleFor(r => r.Description).MaximumLength(10000);
        RuleFor(r => r.Notes).MaximumLength(5000);
        RuleFor(r => r.Url).MaximumLength(2048);
    }
}

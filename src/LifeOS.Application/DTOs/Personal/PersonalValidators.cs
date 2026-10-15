using FluentValidation;
using LifeOS.Application.DTOs.Bible;
namespace LifeOS.Application.DTOs.Personal;
public class JournalRules<T> : AbstractValidator<T> where T : CreateJournalEntryRequest
{
    public JournalRules() { RuleFor(r => r.Title).NotEmpty().MaximumLength(300); RuleFor(r => r.Content).NotNull().MaximumLength(100000); RuleFor(r => r.Mood).MaximumLength(100); RuleFor(r => r.Date).MaximumLength(10); RuleFor(r => r.RequestId).Must(id => id != Guid.Empty).When(r => r.RequestId.HasValue); }
}
public class CreateJournalEntryRequestValidator : JournalRules<CreateJournalEntryRequest> { }
public class UpdateJournalEntryRequestValidator : JournalRules<UpdateJournalEntryRequest> { }
public class SaveRecipeRequestValidator : AbstractValidator<SaveRecipeRequest>
{
    public SaveRecipeRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(300); RuleFor(r => r.BaseServings).InclusiveBetween(1, 10000);
        RuleFor(r => r.Description).MaximumLength(10000); RuleFor(r => r.Instructions).NotNull().MaximumLength(50000); RuleFor(r => r.Category).MaximumLength(100);
        RuleFor(r => r.Ingredients).NotNull().Must(r => r == null || r.Count <= 250); RuleFor(r => r.Steps).NotNull().Must(r => r == null || r.Count <= 250);
        RuleForEach(r => r.Ingredients).NotNull().ChildRules(i => { i.RuleFor(r => r.Name).NotEmpty().MaximumLength(300); i.RuleFor(r => r.Quantity).InclusiveBetween(0, 1000000); i.RuleFor(r => r.Unit).MaximumLength(100); i.RuleFor(r => r.Notes).MaximumLength(1000); });
        RuleForEach(r => r.Steps).NotNull().ChildRules(s => { s.RuleFor(r => r.Text).MaximumLength(10000); s.RuleFor(r => r.StepType).NotEmpty().MaximumLength(50); s.RuleFor(r => r.Variables).NotNull().Must(v => v == null || v.Count <= 20);
            s.RuleForEach(r => r.Variables).NotNull().ChildRules(v => { v.RuleFor(r => r.Name).NotEmpty().MaximumLength(100); v.RuleFor(r => r.Unit).MaximumLength(30); v.RuleFor(r => r.ScalingMode).Must(m => new[] { "none", "linear", "sqrt" }.Contains(m)); v.RuleFor(r => r.Value).InclusiveBetween(-1000000,1000000); }); });
        RuleFor(r => r.RequestId).Must(id => id != Guid.Empty).When(r => r.RequestId.HasValue);
    }
}
public class CreateBookmarkRequestValidator : AbstractValidator<CreateBookmarkRequest>
{
    public CreateBookmarkRequestValidator() { RuleFor(r => r.VerseId).NotEmpty(); RuleFor(r => r.Note).MaximumLength(5000); RuleFor(r => r.Color).Must(c => new[] { "yellow", "blue", "green", "red", "purple" }.Contains(c)); }
}

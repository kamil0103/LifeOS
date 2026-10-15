using FluentValidation;
namespace LifeOS.Application.DTOs.Coach;
public class CoachChatRequestValidator : AbstractValidator<CoachChatRequest>
{
    public CoachChatRequestValidator(){RuleFor(r=>r.Message).NotEmpty().MaximumLength(4000);RuleFor(r=>r.RequestId).Must(id=>id!=Guid.Empty).WithMessage("Invalid message request identifier.");}
}
public class CoachMemoryRequestValidator : AbstractValidator<CoachMemoryRequest>
{
    public CoachMemoryRequestValidator(){RuleFor(r=>r.Content).NotEmpty().MaximumLength(500);}
}

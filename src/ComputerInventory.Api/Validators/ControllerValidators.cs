using ComputerInventory.Api.Controllers;
using FluentValidation;

namespace ComputerInventory.Api.Validators;

public class UpdateComputerRequestValidator : AbstractValidator<UpdateComputerRequest>
{
    public UpdateComputerRequestValidator()
    {
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IpAddress).MaximumLength(45);
        RuleFor(x => x.MacAddress).MaximumLength(17)
            .Matches(@"^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$")
            .When(x => !string.IsNullOrEmpty(x.MacAddress))
            .WithMessage("MacAddress must be in format XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX");
        RuleFor(x => x.Manufacturer).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OperatingSystem).MaximumLength(200);
        RuleFor(x => x.Processor).MaximumLength(200);
        RuleFor(x => x.RamGB).InclusiveBetween(0, 10000).When(x => x.RamGB.HasValue);
        RuleFor(x => x.StorageGB).InclusiveBetween(0, 100000).When(x => x.StorageGB.HasValue);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.AssignedTo).MaximumLength(200);
        RuleFor(x => x.HeartbeatIntervalMinutes).InclusiveBetween(1, 1440);
    }
}

public class CreateMaintenanceRequestValidator : AbstractValidator<CreateMaintenanceRequest>
{
    public CreateMaintenanceRequestValidator()
    {
        RuleFor(x => x.ComputerId).GreaterThan(0);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ScheduledDate).NotEmpty();
        RuleFor(x => x.Cost).InclusiveBetween(0, 1_000_000).When(x => x.Cost.HasValue);
        RuleFor(x => x.PerformedBy).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class UpdateMaintenanceRequestValidator : AbstractValidator<UpdateMaintenanceRequest>
{
    public UpdateMaintenanceRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ScheduledDate).NotEmpty();
        RuleFor(x => x.CompletedDate)
            .GreaterThanOrEqualTo(x => x.ScheduledDate)
            .When(x => x.CompletedDate.HasValue)
            .WithMessage("CompletedDate must be on or after ScheduledDate");
        RuleFor(x => x.Cost).InclusiveBetween(0, 1_000_000).When(x => x.Cost.HasValue);
        RuleFor(x => x.PerformedBy).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class CompleteMaintenanceRequestValidator : AbstractValidator<CompleteMaintenanceRequest>
{
    public CompleteMaintenanceRequestValidator()
    {
        RuleFor(x => x.Cost).InclusiveBetween(0, 1_000_000).When(x => x.Cost.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .WithMessage("NewPassword must be at least 8 characters");
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class GrantAccessRequestValidator : AbstractValidator<GrantAccessRequest>
{
    public GrantAccessRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.AccessLevel).IsInEnum();
    }
}

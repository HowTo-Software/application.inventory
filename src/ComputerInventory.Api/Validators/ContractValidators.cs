using ComputerInventory.Contracts;
using FluentValidation;

namespace ComputerInventory.Api.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class CreateComputerRequestValidator : AbstractValidator<Contracts.CreateComputerRequest>
{
    public CreateComputerRequestValidator()
    {
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(100);
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
        RuleFor(x => x.PurchasePrice).InclusiveBetween(0, 1_000_000).When(x => x.PurchasePrice.HasValue);
        RuleFor(x => x.AssignedTo).MaximumLength(200);
        RuleFor(x => x.HeartbeatIntervalMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.WarrantyExpiration)
            .GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.WarrantyExpiration.HasValue)
            .WithMessage("WarrantyExpiration must be on or after PurchaseDate");
    }
}

public class BulkStatusUpdateRequestValidator : AbstractValidator<Contracts.BulkStatusUpdateRequest>
{
    public BulkStatusUpdateRequestValidator()
    {
        RuleFor(x => x.ComputerIds).NotEmpty().WithMessage("At least one ComputerId is required");
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50);
    }
}

public class CreateInviteCodeRequestValidator : AbstractValidator<CreateInviteCodeRequest>
{
    public CreateInviteCodeRequestValidator()
    {
        RuleFor(x => x.MaxUses).InclusiveBetween(1, 1000);
        RuleFor(x => x.ExpirationHours).InclusiveBetween(1, 8760);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class UploadPackageRequestValidator : AbstractValidator<UploadPackageRequest>
{
    public UploadPackageRequestValidator()
    {
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50)
            .Matches(@"^\d+\.\d+\.\d+(\.\d+)?(-[\w.]+)?$")
            .WithMessage("Version must be a valid semantic version (e.g. 1.0.0, 1.2.3-beta)");
        RuleFor(x => x.PackageType).MaximumLength(20);
        RuleFor(x => x.ReleaseNotes).MaximumLength(5000);
        RuleFor(x => x.MinimumAgentVersion).MaximumLength(50);
        RuleFor(x => x.Channel).MaximumLength(20);
    }
}

public class UpdatePackageRequestValidator : AbstractValidator<UpdatePackageRequest>
{
    public UpdatePackageRequestValidator()
    {
        RuleFor(x => x.ReleaseNotes).MaximumLength(5000);
        RuleFor(x => x.MinimumAgentVersion).MaximumLength(50);
        RuleFor(x => x.Channel).MaximumLength(20);
    }
}

public class AddDistributionRuleRequestValidator : AbstractValidator<AddDistributionRuleRequest>
{
    public AddDistributionRuleRequestValidator()
    {
        RuleFor(x => x.RuleType).NotEmpty();
        RuleFor(x => x.TargetValue).NotEmpty().MaximumLength(200);
    }
}

public class CreateVersionHistoryRequestValidator : AbstractValidator<CreateVersionHistoryRequest>
{
    public CreateVersionHistoryRequestValidator()
    {
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ChangeLog).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.Checksum).MaximumLength(128);
        RuleFor(x => x.FileSize).GreaterThan(0).When(x => x.FileSize.HasValue);
    }
}

public class UpdateVersionHistoryRequestValidator : AbstractValidator<UpdateVersionHistoryRequest>
{
    public UpdateVersionHistoryRequestValidator()
    {
        RuleFor(x => x.ChangeLog).NotEmpty().MaximumLength(5000);
    }
}

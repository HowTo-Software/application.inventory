namespace ComputerInventory.Api.Services;

public interface IPasswordValidator
{
    PasswordValidationResult Validate(string password);
}

public class PasswordValidator : IPasswordValidator
{
    private const int MinLength = 8;
    private const int MaxLength = 128;

    public PasswordValidationResult Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return new PasswordValidationResult(errors);
        }

        if (password.Length < MinLength)
            errors.Add($"Password must be at least {MinLength} characters long.");

        if (password.Length > MaxLength)
            errors.Add($"Password must not exceed {MaxLength} characters.");

        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter.");

        if (!password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter.");

        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            errors.Add("Password must contain at least one special character.");

        return new PasswordValidationResult(errors);
    }
}

public class PasswordValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }

    public PasswordValidationResult(List<string> errors)
    {
        Errors = errors.AsReadOnly();
    }

    public string ErrorMessage => string.Join(" ", Errors);
}

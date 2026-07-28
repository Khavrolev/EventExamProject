using System.ComponentModel.DataAnnotations;

namespace EventExamProject.Validation;

[AttributeUsage(AttributeTargets.Property)]
public class DateAfterAttribute(string otherPropertyName, bool orEqual = false) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime currentValue)
        {
            return ValidationResult.Success;
        }

        var otherProperty = context.ObjectType.GetProperty(otherPropertyName) ?? throw new ArgumentException($"Property {otherPropertyName} not found");

        if (otherProperty.GetValue(context.ObjectInstance) is not DateTime otherValue)
        {
            return ValidationResult.Success;
        }

        var isValid = orEqual ? currentValue >= otherValue : currentValue > otherValue;

        return isValid
            ? ValidationResult.Success
            : new ValidationResult(string.Format(ErrorMessageString, context.DisplayName, otherPropertyName));
    }
}
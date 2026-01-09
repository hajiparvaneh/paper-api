using System.Text;
using FluentValidation;
using PaperAPI.Application.Pdf;

namespace PaperAPI.PdfApi.Models.Requests;

public sealed class GeneratePdfRequestValidator : AbstractValidator<GeneratePdfRequest>
{
    private const int MaxHtmlBytes = 500 * 1024;

    public GeneratePdfRequestValidator()
    {
        RuleFor(r => r.Html)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("The 'html' field is required.")
            .Must(BeWithinLimit).WithMessage("HTML payload exceeds 500KB.");

        RuleFor(r => r.Options)
            .Custom((options, context) =>
            {
                if (options is null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(options.Orientation) &&
                    !string.Equals(options.Orientation, "Portrait", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(options.Orientation, "Landscape", StringComparison.OrdinalIgnoreCase))
                {
                    context.AddFailure("options.orientation", "Orientation must be either 'Portrait' or 'Landscape'.");
                }

                if (options.EnableJavascript == true && options.DisableJavascript == true)
                {
                    context.AddFailure("options.enableJavascript", "EnableJavascript and DisableJavascript cannot both be true.");
                }

                if (options.Images == true && options.NoImages == true)
                {
                    context.AddFailure("options.images", "Images and NoImages cannot both be true.");
                }

                ValidateNonNegative(context, options.MarginTop, "options.marginTop");
                ValidateNonNegative(context, options.MarginRight, "options.marginRight");
                ValidateNonNegative(context, options.MarginBottom, "options.marginBottom");
                ValidateNonNegative(context, options.MarginLeft, "options.marginLeft");
                ValidateNonNegative(context, options.HeaderSpacing, "options.headerSpacing");
                ValidateNonNegative(context, options.FooterSpacing, "options.footerSpacing");

                if (options.Zoom.HasValue && options.Zoom.Value <= 0)
                {
                    context.AddFailure("options.zoom", "Zoom must be greater than 0.");
                }

                if (options.Dpi.HasValue && options.Dpi.Value <= 0)
                {
                    context.AddFailure("options.dpi", "Dpi must be greater than 0.");
                }

                if (options.ImageDpi.HasValue && options.ImageDpi.Value <= 0)
                {
                    context.AddFailure("options.imageDpi", "ImageDpi must be greater than 0.");
                }

                if (options.ImageQuality.HasValue && (options.ImageQuality.Value < 0 || options.ImageQuality.Value > 100))
                {
                    context.AddFailure("options.imageQuality", "ImageQuality must be between 0 and 100.");
                }
            });
    }

    private static bool BeWithinLimit(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var length = Encoding.UTF8.GetByteCount(html);
        return length <= MaxHtmlBytes;
    }

    private static void ValidateNonNegative(ValidationContext<GeneratePdfRequest> context, decimal? value, string fieldName)
    {
        if (value.HasValue && value.Value < 0)
        {
            context.AddFailure(fieldName, $"{fieldName} must be greater than or equal to 0.");
        }
    }
}

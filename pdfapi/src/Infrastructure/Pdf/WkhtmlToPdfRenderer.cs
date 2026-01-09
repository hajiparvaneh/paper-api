using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Pdf;
using PaperAPI.Infrastructure.Options;

namespace PaperAPI.Infrastructure.Pdf;

public sealed class WkhtmlToPdfRenderer : IPdfRenderer
{
    private readonly WkhtmlToPdfOptions _options;
    private readonly ILogger<WkhtmlToPdfRenderer> _logger;

    public WkhtmlToPdfRenderer(IOptions<WkhtmlToPdfOptions> options, ILogger<WkhtmlToPdfRenderer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task RenderAsync(string html, PdfOptions options, Stream output, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        options ??= new PdfOptions();

        var htmlBytes = Encoding.UTF8.GetByteCount(html);
        _logger.LogDebug("Preparing wkhtmltopdf render; HTML payload size is {ByteCount} bytes", htmlBytes);
        _logger.LogDebug("Requested PDF options: {OptionsSummary}", BuildOptionSummary(options));

        if (!string.IsNullOrWhiteSpace(options.HeaderHtml))
        {
            _logger.LogDebug("Custom header HTML provided ({Length} characters)", options.HeaderHtml.Length);
        }

        if (!string.IsNullOrWhiteSpace(options.FooterHtml))
        {
            _logger.LogDebug("Custom footer HTML provided ({Length} characters)", options.FooterHtml.Length);
        }

        var htmlFilePath = Path.Combine(Path.GetTempPath(), $"paperapi-{Guid.NewGuid():N}.html");
        var pdfFilePath = Path.ChangeExtension(htmlFilePath, ".pdf");

        var tempFiles = new List<string>();

        try
        {
            _logger.LogDebug("Writing HTML payload to {HtmlFilePath}", htmlFilePath);
            await File.WriteAllTextAsync(htmlFilePath, html, Encoding.UTF8, cancellationToken);

            var arguments = BuildArguments(options, htmlFilePath, pdfFilePath, tempFiles);
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _logger.LogDebug("Starting wkhtmltopdf process with arguments: {Arguments}", string.Join(' ', startInfo.ArgumentList));
            process.Start();

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;

            if (process.ExitCode != 0 || !File.Exists(pdfFilePath))
            {
                _logger.LogError("wkhtmltopdf failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                throw new PdfRenderingException($"wkhtmltopdf exited with code {process.ExitCode}", stderr);
            }

            _logger.LogDebug("wkhtmltopdf completed; streaming file {PdfFilePath}", pdfFilePath);

            await using var pdfStream = new FileStream(
                pdfFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            await pdfStream.CopyToAsync(output, 81920, cancellationToken);
        }
        finally
        {
            TryDeleteFile(htmlFilePath);
            TryDeleteFile(pdfFilePath);

            foreach (var tempFile in tempFiles)
            {
                TryDeleteFile(tempFile);
            }
        }
    }

    private static IReadOnlyList<string> BuildArguments(
        PdfOptions options,
        string htmlFilePath,
        string pdfFilePath,
        ICollection<string> tempFiles)
    {
        var args = new List<string> { "--quiet" };

        AddStringOption(args, "--page-size", options.PageSize);
        AddStringOption(args, "--orientation", options.Orientation);
        AddDecimalOption(args, "--margin-top", options.MarginTop);
        AddDecimalOption(args, "--margin-right", options.MarginRight);
        AddDecimalOption(args, "--margin-bottom", options.MarginBottom);
        AddDecimalOption(args, "--margin-left", options.MarginLeft);
        AddFlag(args, "--print-media-type", options.PrintMediaType);
        AddFlag(args, "--disable-smart-shrinking", options.DisableSmartShrinking);
        AddFlag(args, "--enable-javascript", options.EnableJavascript);
        AddFlag(args, "--disable-javascript", options.DisableJavascript);
        AddStringOption(args, "--header-left", options.HeaderLeft);
        AddStringOption(args, "--header-center", options.HeaderCenter);
        AddStringOption(args, "--header-right", options.HeaderRight);
        AddStringOption(args, "--footer-left", options.FooterLeft);
        AddStringOption(args, "--footer-center", options.FooterCenter);
        AddStringOption(args, "--footer-right", options.FooterRight);
        AddDecimalOption(args, "--header-spacing", options.HeaderSpacing);
        AddDecimalOption(args, "--footer-spacing", options.FooterSpacing);

        if (!string.IsNullOrWhiteSpace(options.HeaderHtml))
        {
            var headerPath = WriteTempHtmlFile(options.HeaderHtml);
            tempFiles.Add(headerPath);
            args.Add("--header-html");
            args.Add(headerPath);
        }

        if (!string.IsNullOrWhiteSpace(options.FooterHtml))
        {
            var footerPath = WriteTempHtmlFile(options.FooterHtml);
            tempFiles.Add(footerPath);
            args.Add("--footer-html");
            args.Add(footerPath);
        }

        AddIntOption(args, "--dpi", options.Dpi);
        AddDoubleOption(args, "--zoom", options.Zoom);
        AddIntOption(args, "--image-dpi", options.ImageDpi);
        AddIntOption(args, "--image-quality", options.ImageQuality);
        AddFlag(args, "--lowquality", options.LowQuality);
        AddFlag(args, "--images", options.Images);
        AddFlag(args, "--no-images", options.NoImages);

        args.Add(htmlFilePath);
        args.Add(pdfFilePath);

        return args;
    }

    private static string BuildOptionSummary(PdfOptions options)
    {
        var builder = new StringBuilder();
        builder.Append("PageSize=").Append(options.PageSize ?? "default").Append("; ");
        builder.Append("Orientation=").Append(options.Orientation ?? "Portrait").Append("; ");
        builder.Append("Margins(top/right/bottom/left)=")
            .Append(FormatDecimal(options.MarginTop)).Append("/")
            .Append(FormatDecimal(options.MarginRight)).Append("/")
            .Append(FormatDecimal(options.MarginBottom)).Append("/")
            .Append(FormatDecimal(options.MarginLeft)).Append("; ");
        builder.Append("HeaderText=").Append(HasText(options.HeaderLeft, options.HeaderCenter, options.HeaderRight) ? "yes" : "no").Append("; ");
        builder.Append("FooterText=").Append(HasText(options.FooterLeft, options.FooterCenter, options.FooterRight) ? "yes" : "no").Append("; ");
        builder.Append("HeaderSpacing=").Append(FormatDecimal(options.HeaderSpacing)).Append("; ");
        builder.Append("FooterSpacing=").Append(FormatDecimal(options.FooterSpacing)).Append("; ");
        builder.Append("HeaderHtml=").Append(string.IsNullOrWhiteSpace(options.HeaderHtml) ? "no" : "yes").Append("; ");
        builder.Append("FooterHtml=").Append(string.IsNullOrWhiteSpace(options.FooterHtml) ? "no" : "yes").Append("; ");
        builder.Append("Dpi=").Append(FormatInt(options.Dpi)).Append("; ");
        builder.Append("Zoom=").Append(FormatDouble(options.Zoom)).Append("; ");
        builder.Append("ImageDpi=").Append(FormatInt(options.ImageDpi)).Append("; ");
        builder.Append("ImageQuality=").Append(FormatInt(options.ImageQuality)).Append("; ");
        builder.Append("PrintMediaType=").Append(FormatBool(options.PrintMediaType)).Append("; ");
        builder.Append("DisableSmartShrinking=").Append(FormatBool(options.DisableSmartShrinking)).Append("; ");
        builder.Append("EnableJavascript=").Append(FormatBool(options.EnableJavascript)).Append("; ");
        builder.Append("DisableJavascript=").Append(FormatBool(options.DisableJavascript)).Append("; ");
        builder.Append("LowQuality=").Append(FormatBool(options.LowQuality)).Append("; ");
        builder.Append("Images=").Append(FormatBool(options.Images)).Append("; ");
        builder.Append("NoImages=").Append(FormatBool(options.NoImages));
        return builder.ToString();
    }

    private static string FormatDecimal(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "default";

    private static string FormatDouble(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "default";

    private static string FormatInt(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "default";

    private static string FormatBool(bool? value) =>
        value.HasValue ? (value.Value ? "true" : "false") : "default";

    private static bool HasText(params string?[] values) =>
        values.Any(v => !string.IsNullOrWhiteSpace(v));

    private static void AddStringOption(ICollection<string> args, string flag, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args.Add(flag);
            args.Add(value);
        }
    }

    private static void AddDecimalOption(ICollection<string> args, string flag, decimal? value)
    {
        if (value.HasValue)
        {
            args.Add(flag);
            args.Add(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddDoubleOption(ICollection<string> args, string flag, double? value)
    {
        if (value.HasValue)
        {
            args.Add(flag);
            args.Add(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddIntOption(ICollection<string> args, string flag, int? value)
    {
        if (value.HasValue)
        {
            args.Add(flag);
            args.Add(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddFlag(ICollection<string> args, string flag, bool? value)
    {
        if (value == true)
        {
            args.Add(flag);
        }
    }

    private static string WriteTempHtmlFile(string html)
    {
        var path = Path.Combine(Path.GetTempPath(), $"paperapi-{Guid.NewGuid():N}.html");
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary file {Path}", path);
        }
    }
}

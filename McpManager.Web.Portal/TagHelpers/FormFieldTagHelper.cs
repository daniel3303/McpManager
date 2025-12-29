using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace McpManager.Web.Portal.TagHelpers;

[HtmlTargetElement("form-field", TagStructure = TagStructure.WithoutEndTag)]
public class FormFieldTagHelper : TagHelper {
    private readonly IHtmlGenerator _htmlGenerator;

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; }

    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; }

    [HtmlAttributeName("type")]
    public string Type { get; set; }

    [HtmlAttributeName("class")]
    public string CssClass { get; set; }

    [HtmlAttributeName("input-class")]
    public string InputClass { get; set; }

    [HtmlAttributeName("placeholder")]
    public string Placeholder { get; set; }

    [HtmlAttributeName("readonly")]
    public bool Readonly { get; set; }

    [HtmlAttributeName("disabled")]
    public bool Disabled { get; set; }

    [HtmlAttributeName("autocomplete")]
    public string Autocomplete { get; set; }

    public FormFieldTagHelper(IHtmlGenerator htmlGenerator) {
        _htmlGenerator = htmlGenerator;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output) {
        var metadata = For.Metadata;
        var isRequired = metadata.IsRequired;
        var displayName = metadata.DisplayName ?? metadata.Name;

        // Auto-detect type from DataType attribute if not specified
        var inputType = Type ?? GetInputTypeFromMetadata(metadata);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (!string.IsNullOrEmpty(CssClass)) {
            output.Attributes.SetAttribute("class", CssClass);
        }

        // Build the legend with optional required indicator
        var legendContent = displayName;
        if (isRequired) {
            legendContent += " <span class=\"text-error\">*</span>";
        }

        // Build HTML attributes dictionary
        var htmlAttributes = BuildHtmlAttributes(inputType);

        // Generate the appropriate input element
        var inputTag = GenerateInputElement(inputType, htmlAttributes);

        // Generate the validation message
        var validationTag = _htmlGenerator.GenerateValidationMessage(
            ViewContext,
            For.ModelExplorer,
            For.Name,
            null,
            "span",
            new { @class = "text-error text-sm" }
        );

        var html = $@"<fieldset class=""fieldset"">
    <legend class=""fieldset-legend"">{legendContent}</legend>
    {GetTagHtml(inputTag)}
</fieldset>
{GetTagHtml(validationTag)}";

        output.Content.SetHtmlContent(html);
    }

    private string GetInputTypeFromMetadata(ModelMetadata metadata) {
        // Check DataType attribute
        if (metadata.DataTypeName != null) {
            var type = metadata.DataTypeName.ToLowerInvariant() switch {
                "password" => "password",
                "emailaddress" => "email",
                "url" => "url",
                "phonenumber" => "tel",
                "date" => "date",
                "time" => "time",
                "datetime" => "datetime-local",
                "multilinetext" => "textarea",
                _ => (string)null
            };
            if (type != null) return type;
        }

        // Check for validation attributes that imply input type
        var validatorMetadata = metadata.ValidatorMetadata;
        foreach (var validator in validatorMetadata) {
            if (validator is System.ComponentModel.DataAnnotations.EmailAddressAttribute) {
                return "email";
            }
            if (validator is System.ComponentModel.DataAnnotations.UrlAttribute) {
                return "url";
            }
            if (validator is System.ComponentModel.DataAnnotations.PhoneAttribute) {
                return "tel";
            }
        }

        // Check underlying type
        var modelType = Nullable.GetUnderlyingType(metadata.ModelType) ?? metadata.ModelType;

        if (modelType == typeof(DateTime) || modelType == typeof(DateOnly)) {
            return "date";
        }
        if (modelType == typeof(TimeOnly)) {
            return "time";
        }
        if (modelType == typeof(int) || modelType == typeof(long) || modelType == typeof(decimal) || modelType == typeof(double) || modelType == typeof(float)) {
            return "number";
        }

        return "text";
    }

    private Dictionary<string, object> BuildHtmlAttributes(string inputType) {
        var cssClass = $"input w-full {InputClass}".Trim();
        if (inputType == "textarea") {
            cssClass = $"textarea w-full {InputClass}".Trim();
        }

        var attributes = new Dictionary<string, object> {
            ["class"] = cssClass
        };

        if (!string.IsNullOrEmpty(Placeholder)) {
            attributes["placeholder"] = Placeholder;
        }

        if (Readonly) {
            attributes["readonly"] = "readonly";
        }

        if (Disabled) {
            attributes["disabled"] = "disabled";
        }

        if (!string.IsNullOrEmpty(Autocomplete)) {
            attributes["autocomplete"] = Autocomplete;
        }

        return attributes;
    }

    private TagBuilder GenerateInputElement(string inputType, Dictionary<string, object> htmlAttributes) {
        return inputType switch {
            "password" => _htmlGenerator.GeneratePassword(
                ViewContext,
                For.ModelExplorer,
                For.Name,
                null,
                htmlAttributes
            ),
            "textarea" => _htmlGenerator.GenerateTextArea(
                ViewContext,
                For.ModelExplorer,
                For.Name,
                3, // rows
                0, // columns (auto)
                htmlAttributes
            ),
            _ => _htmlGenerator.GenerateTextBox(
                ViewContext,
                For.ModelExplorer,
                For.Name,
                For.Model,
                null,
                new Dictionary<string, object>(htmlAttributes) { ["type"] = inputType }
            )
        };
    }

    private static string GetTagHtml(TagBuilder tag) {
        using var writer = new StringWriter();
        tag.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }
}

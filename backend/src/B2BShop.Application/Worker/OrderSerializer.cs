using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using B2BShop.Domain.Entities;

namespace B2BShop.Application.Worker;

/// <summary>
/// Serializes orders to various formats for external system transfer.
/// Supports JSON, XML, CSV, and a basic EDI-like fixed-width format.
/// </summary>
public static class OrderSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(Order order)
    {
        var payload = new
        {
            orderNumber = order.OrderNumber,
            companyId = order.CompanyId,
            status = order.Status.ToString(),
            currencyCode = order.CurrencyCode,
            grandTotal = order.GrandTotal,
            createdAt = order.CreatedAt,
            lines = order.Lines.Select(l => new {
                articleCode = l.ArticleCode,
                articleName = l.ArticleName,
                quantity = l.Quantity,
                unitOfMeasure = l.UnitOfMeasure,
                unitPrice = l.UnitPrice,
                lineTotal = l.LineTotal
            }).ToList()
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string ToXml(Order order)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var writer = XmlWriter.Create(sb, settings);

        writer.WriteStartElement("Order");
        writer.WriteElementString("OrderNumber", order.OrderNumber);
        writer.WriteElementString("CompanyId", order.CompanyId.ToString());
        writer.WriteElementString("Status", order.Status.ToString());
        writer.WriteElementString("CurrencyCode", order.CurrencyCode);
        writer.WriteElementString("GrandTotal", order.GrandTotal.ToString("F2"));
        writer.WriteElementString("CreatedAt", order.CreatedAt.ToString("O"));

        writer.WriteStartElement("Lines");
        foreach (var line in order.Lines)
        {
            writer.WriteStartElement("Line");
            writer.WriteElementString("ArticleCode", line.ArticleCode);
            writer.WriteElementString("ArticleName", line.ArticleName);
            writer.WriteElementString("Quantity", line.Quantity.ToString());
            writer.WriteElementString("UnitOfMeasure", line.UnitOfMeasure);
            writer.WriteElementString("UnitPrice", line.UnitPrice.ToString("F4"));
            writer.WriteElementString("LineTotal", line.LineTotal.ToString("F4"));
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // Lines
        writer.WriteEndElement(); // Order
        writer.Flush();

        return sb.ToString();
    }

    public static string ToCsv(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OrderNumber,CompanyId,ArticleCode,ArticleName,Quantity,UOM,UnitPrice,LineTotal,CurrencyCode");

        foreach (var line in order.Lines)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(order.OrderNumber),
                order.CompanyId,
                EscapeCsv(line.ArticleCode),
                EscapeCsv(line.ArticleName),
                line.Quantity,
                EscapeCsv(line.UnitOfMeasure),
                line.UnitPrice.ToString("F4"),
                line.LineTotal.ToString("F4"),
                EscapeCsv(order.CurrencyCode)
            ));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Produces a simplified EDI-like fixed-width format.
    /// Not standards-compliant — a training exercise stub.
    /// </summary>
    public static string ToEdi(Order order)
    {
        var sb = new StringBuilder();
        // Header record
        sb.AppendLine($"HDR{order.OrderNumber,-20}{order.CreatedAt:yyyyMMdd}{order.CurrencyCode,3}{order.GrandTotal,12:F2}");
        // Detail records
        foreach (var line in order.Lines)
        {
            sb.AppendLine($"DET{line.ArticleCode,-30}{line.Quantity,8}{line.UnitOfMeasure,-5}{line.UnitPrice,12:F4}{line.LineTotal,12:F4}");
        }
        // Trailer
        sb.AppendLine($"TRL{order.Lines.Count,5}");
        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (value == null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

/// <summary>
/// Transfer retry and dead-letter policy constants.
/// </summary>
public static class OrderTransferPolicy
{
    public const int MaxRetryAttempts = 3;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public static bool ShouldDeadLetter(int currentAttempt) => currentAttempt > MaxRetryAttempts;

    public static TimeSpan GetRetryDelay(int attemptNumber)
    {
        // Exponential backoff — doubles each attempt
        return TimeSpan.FromMinutes(Math.Pow(2, attemptNumber - 1) * 5);
    }
}

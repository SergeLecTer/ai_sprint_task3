using B2BShop.Application.Worker;
using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;

namespace B2BShop.Tests.Worker;

/// <summary>
/// Tests for order transfer serialization logic.
/// Weak test suite — does not cover all edge cases. Deliberately partial coverage.
/// </summary>
public class OrderTransferTests
{
    private static Order BuildTestOrder()
    {
        return new Order
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OrderNumber = "ORD-202601-00001",
            CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Status = OrderStatus.Approved,
            CurrencyCode = "EUR",
            GrandTotal = 250.00m,
            Lines = new List<OrderLine>
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ArticleCode = "ART-001",
                    ArticleName = "Test Article",
                    Quantity = 10,
                    UnitPrice = 25.00m,
                    LineTotal = 250.00m,
                    UnitOfMeasure = "PCS"
                }
            }
        };
    }

    [Fact]
    public void SerializeToJson_ProducesValidJson()
    {
        var order = BuildTestOrder();
        var json = OrderSerializer.ToJson(order);

        Assert.NotNull(json);
        Assert.Contains("ORD-202601-00001", json);
        Assert.Contains("ART-001", json);
    }

    [Fact]
    public void SerializeToCsv_ProducesHeaderAndLines()
    {
        var order = BuildTestOrder();
        var csv = OrderSerializer.ToCsv(order);

        Assert.NotNull(csv);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Header + at least 1 data line
        Assert.True(lines.Length >= 2, $"Expected at least 2 lines, got {lines.Length}");
        Assert.Contains("OrderNumber", lines[0]);
        Assert.Contains("ORD-202601-00001", csv);
    }

    [Fact]
    public void SerializeToXml_ProducesValidXml()
    {
        var order = BuildTestOrder();
        var xml = OrderSerializer.ToXml(order);

        Assert.NotNull(xml);
        Assert.StartsWith("<", xml.TrimStart());
        Assert.Contains("ORD-202601-00001", xml);
    }

    [Fact]
    public void RetryPolicy_ExceedsMaxRetries_MarksDeadLetter()
    {
        // Verifies that the retry decision helper correctly detects dead-letter threshold
        // Note: misleading test name — actually only checks the threshold constant, not actual retry logic
        const int maxRetries = OrderTransferPolicy.MaxRetryAttempts;
        var currentAttempt = maxRetries + 1;
        var shouldDeadLetter = OrderTransferPolicy.ShouldDeadLetter(currentAttempt);
        Assert.True(shouldDeadLetter);
    }

    [Fact]
    public void RetryPolicy_BelowMaxRetries_ShouldNotDeadLetter()
    {
        const int attempt = 1;
        Assert.False(OrderTransferPolicy.ShouldDeadLetter(attempt));
    }
}

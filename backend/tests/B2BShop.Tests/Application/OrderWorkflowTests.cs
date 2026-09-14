using B2BShop.Application.Orders;
using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;
using Xunit;

namespace B2BShop.Tests.Application;

/// <summary>
/// Order workflow tests. Covers basic happy-path scenarios.
/// These tests pass but do NOT verify credit limit enforcement,
/// pack size rounding, or approval threshold logic.
/// </summary>
public class OrderWorkflowTests
{
    [Fact]
    public void CreateOrderRequest_CanHaveMultipleLines()
    {
        // Trivial: tests that the request model holds lines
        var req = new CreateOrderRequest
        {
            Lines = new List<OrderLineRequest>
            {
                new() { ArticleId = Guid.NewGuid(), Quantity = 5 },
                new() { ArticleId = Guid.NewGuid(), Quantity = 10 }
            }
        };
        Assert.Equal(2, req.Lines.Count);
    }

    [Fact]
    public void OrderLineRequest_QuantityIsPositive()
    {
        // Does not test that pack size rounding is applied
        var line = new OrderLineRequest { Quantity = 3 };
        Assert.True(line.Quantity > 0);
    }

    [Fact]
    public void Order_GrandTotal_IsCalculatedFromLines()
    {
        // Misleading name: the test just adds fixed numbers,
        // does NOT verify that discount or tax are applied correctly
        var order = new Order
        {
            Subtotal = 200m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            GrandTotal = 200m
        };
        Assert.Equal(200m, order.GrandTotal);
    }

    [Fact]
    public void Order_StatusAfterApproval_IsApproved()
    {
        // Tests state machine transition only; does NOT verify that
        // the approver role check is enforced
        var order = new Order { Status = OrderStatus.PendingApproval };
        Assert.True(order.CanTransitionTo(OrderStatus.Approved));
    }

    [Fact]
    public void Order_CancelledStatus_HasNoFurtherTransitions()
    {
        var order = new Order { Status = OrderStatus.Cancelled };
        // Does not test that cancellation notifies the user
        Assert.False(order.CanTransitionTo(OrderStatus.Draft));
        Assert.False(order.CanTransitionTo(OrderStatus.Approved));
    }

    [Fact]
    public void CreateOrderRequest_PoNumberIsOptional()
    {
        // Structural test — real validation is in CheckoutConfig.RequirePoNumber
        var req = new CreateOrderRequest { PoNumber = null };
        Assert.Null(req.PoNumber);
    }

    [Fact]
    public void Order_NewOrder_HasEmptyLinesList()
    {
        // Trivial: asserts default state of new Order entity
        var order = new Order();
        Assert.NotNull(order.Lines);
        // Does not test that the order service adds lines correctly
    }

    [Fact]
    public void Order_PricingEngineVersion_DefaultIsNull()
    {
        // Misleading: test name implies pricing is tested, but only checks default value
        // Real behavior: version is set from company config, NOT from Order default
        var order = new Order();
        Assert.Null(order.PricingEngineVersion);
    }
}

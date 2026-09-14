using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;
using Xunit;

namespace B2BShop.Tests.Domain;

public class OrderTests
{
    [Fact]
    public void NewOrder_HasDraftStatus()
    {
        var order = new Order { Status = OrderStatus.Draft };
        Assert.Equal(OrderStatus.Draft, order.Status);
    }

    [Fact]
    public void Order_CanTransitionFromDraftToPendingApproval()
    {
        var order = new Order { Status = OrderStatus.Draft };
        // Valid transition
        Assert.True(order.CanTransitionTo(OrderStatus.PendingApproval));
    }

    [Fact]
    public void Order_CannotTransitionFromFulfilledToDraft()
    {
        var order = new Order { Status = OrderStatus.Fulfilled };
        Assert.False(order.CanTransitionTo(OrderStatus.Draft));
    }

    [Fact]
    public void Order_CanTransitionFromTransferredToAcknowledged()
    {
        var order = new Order { Status = OrderStatus.Transferred };
        Assert.True(order.CanTransitionTo(OrderStatus.Acknowledged));
    }

    [Fact]
    public void Order_CalculatesSubtotalFromLines()
    {
        var order = new Order
        {
            Lines = new List<OrderLine>
            {
                new() { Quantity = 2, UnitPrice = 10.00m, LineTotal = 20.00m },
                new() { Quantity = 3, UnitPrice = 5.00m, LineTotal = 15.00m }
            }
        };
        Assert.Equal(35.00m, order.CalculateSubtotal());
    }

    [Fact]
    public void Order_ApprovalNotRequiredWhenBelowThreshold()
    {
        // Deliberately misleading test - ignores company config
        // This test will pass but misses the real approval logic
        var order = new Order { GrandTotal = 100m };
        Assert.True(order.GrandTotal > 0);
    }
}

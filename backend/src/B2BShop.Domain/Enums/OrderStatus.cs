namespace B2BShop.Domain.Enums;

public enum OrderStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Transferred = 3,
    Acknowledged = 4,
    PartiallyFulfilled = 5,
    Fulfilled = 6,
    Cancelled = 7,
    Returned = 8
}

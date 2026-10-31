namespace SchnapperRadar.Domain.Enums;

public enum DeliveryMode
{
    Unknown = 0,
    Shipping = 1,
    Pickup = 2,
}

/// <summary>Was der Nutzer bei der Suche zulässt.</summary>
public enum DeliveryPreference
{
    Any = 0,
    ShippingOnly = 1,
    PickupOnly = 2,
}

using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Application.Abstractions;

public interface IGeoService
{
    GeoPoint? Resolve(string? postalCode);
}

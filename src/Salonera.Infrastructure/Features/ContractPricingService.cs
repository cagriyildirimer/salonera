using Salonera.Application.Features.Sales;
using Salonera.Domain.Enums;

namespace Salonera.Infrastructure.Features;

public sealed class ContractPricingService : IContractPricingService
{
    public PriceCalculation Calculate(
        IEnumerable<PriceLine> lines,
        decimal discountAmount,
        decimal vatRate,
        CurrencyCode currency,
        decimal exchangeRate)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var lineList = lines.ToList();
        if (lineList.Count == 0 || lineList.Any(line => line.Quantity <= 0 || line.UnitPrice < 0 || line.UnitCost < 0))
        {
            throw new ArgumentException("En az bir geçerli fiyat satırı bulunmalıdır.", nameof(lines));
        }

        var grossAmount = Round(lineList.Sum(line => line.Quantity * line.UnitPrice));
        if (discountAmount < 0 || discountAmount > grossAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(discountAmount), "İndirim brüt tutardan büyük veya negatif olamaz.");
        }

        if (vatRate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate), "KDV oranı 0 ile 100 arasında olmalıdır.");
        }

        if (currency == CurrencyCode.Try)
        {
            exchangeRate = 1m;
        }
        else if (exchangeRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), "Döviz kuru sıfırdan büyük olmalıdır.");
        }

        var taxableAmount = Round(grossAmount - discountAmount);
        var vatAmount = Round(taxableAmount * vatRate / 100m);
        var netAmount = Round(taxableAmount + vatAmount);
        return new PriceCalculation(
            grossAmount,
            Round(discountAmount),
            taxableAmount,
            Round(vatRate),
            vatAmount,
            netAmount,
            currency,
            exchangeRate,
            Round(netAmount * exchangeRate));
    }

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

using NursingHome.BusinessObjects.Entities;

namespace NursingHome.BusinessObjects.Helpers;

/// <summary>
/// Cách tính tiền dùng chung cho DAO (trong transaction) và Services (hiển thị).
/// VND không có đơn vị lẻ: làm tròn từng dòng tới đồng, midpoint away from zero.
/// </summary>
public static class InvoiceMath
{
    public static decimal LineTotal(InvoiceItem item) => decimal.Round(item.Quantity * item.UnitPrice, 0, MidpointRounding.AwayFromZero);

    public static decimal Total(IEnumerable<InvoiceItem> items) => items.Sum(LineTotal);

    public static decimal Paid(IEnumerable<Payment> payments) => payments.Sum(payment => payment.Amount);

    public static decimal Remaining(Invoice invoice) => Total(invoice.Items) - Paid(invoice.Payments);

    /// <summary>
    /// Phí gói trong kỳ = giá tháng × số ngày giao nhau / số ngày của tháng (tính cả ngày đầu và cuối).
    /// Trả về null khi không có ngày giao nhau.
    /// </summary>
    public static (DateOnly Start, DateOnly End, int Days, decimal Amount)? ProrateMonth(
        DateOnly billingPeriod, DateOnly subscriptionStart, DateOnly? subscriptionEnd,
        DateOnly admitted, DateOnly? discharged, decimal monthlyPrice)
    {
        var periodEnd = billingPeriod.AddMonths(1).AddDays(-1);
        var start = new[] { billingPeriod, subscriptionStart, admitted }.Max();
        var end = new[] { periodEnd, subscriptionEnd ?? periodEnd, discharged ?? periodEnd }.Min();
        if (end < start)
        {
            return null;
        }

        int days = end.DayNumber - start.DayNumber + 1;
        decimal amount = decimal.Round(monthlyPrice * days / periodEnd.Day, 0, MidpointRounding.AwayFromZero);
        return (start, end, days, amount);
    }
}

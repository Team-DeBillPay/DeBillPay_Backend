namespace DeBillPay_Backend.Services;

public class EbillService
{
    private static readonly List<Ebill> _mockEbills = new()
    {
        new Ebill(1, "Поїздка у Львів", 3500.0, "Active", 4),
        new Ebill(2, "Святкування Дня народження", 0.0, "Closed", 6),
        new Ebill(3, "Оренда Airbnb", 1200.0, "Active", 3),
        new Ebill(4, "Обід у кафе", 450.50, "Partially paid", 2)
    };

    public IEnumerable<Ebill> GetActiveEbills()
    {
        return _mockEbills.Where(e => e.Status == "Active" || e.Status == "Partially paid");
    }
}
namespace DeBillPay_Backend.Services;

public class EbillService
{
    private static readonly List<Ebill> _mockEbills = new()
    {
        new Ebill(1, "������ � ����", 3500.0, "Active", 4),
        new Ebill(2, "����������� ��� ����������", 0.0, "Closed", 6),
        new Ebill(3, "������ Airbnb", 1200.0, "Active", 3),
        new Ebill(4, "��� � ����", 450.50, "Partially paid", 2)
    };

    public IEnumerable<Ebill> GetActiveEbills()
    {
        return _mockEbills.Where(e => e.Status == "Active" || e.Status == "Partially paid");
    }
}
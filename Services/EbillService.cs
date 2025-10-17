using DeBillPay_Backend.Models;

namespace DeBillPay_Backend.Services;

public class EbillService
{
    private static readonly List<Ebill> _mockEbills = new()
    {
        new Ebill(
            1,
            "������ � ����",
            "UAH",
            3500.0m,
            "������ � ������� �� ������",
            "Travel",
            "Active",
            DateTime.Now.AddDays(-5),
            DateTime.Now,
            4
        ),
        new Ebill(
            2,
            "����������� ��� ����������",
            "UAH",
            0.0m,
            "����� ����� � ��������",
            "Party",
            "Closed",
            DateTime.Now.AddDays(-20),
            DateTime.Now.AddDays(-10),
            6
        ),
        new Ebill(
            3,
            "������ Airbnb",
            "USD",
            1200.0m,
            "�������� �� 3 �� � ������",
            "Rent",
            "Active",
            DateTime.Now.AddDays(-3),
            DateTime.Now,
            3
        ),
        new Ebill(
            4,
            "��� � ����",
            "UAH",
            450.50m,
            "���� � ��������",
            "Food",
            "Partially paid",
            DateTime.Now.AddDays(-1),
            DateTime.Now,
            2
        )
    };

    public IEnumerable<Ebill> GetActiveEbills()
    {
        return _mockEbills.Where(e => e.Status == "Active" || e.Status == "Partially paid");
    }
}
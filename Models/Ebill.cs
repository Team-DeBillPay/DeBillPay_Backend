namespace DeBillPay_Backend.Models;

public record Ebill(
    int EbillId,
    string Name,
    string Currency,
    decimal AmountOfDept,
    string Description,
    string Scenario,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int OrganizerId
);
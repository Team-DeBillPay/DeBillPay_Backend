namespace DeBillPay_Backend.Models;

public record Ebill(int EbillId, string Name, double AmountOfDept, string Status, int ParticipantCount);
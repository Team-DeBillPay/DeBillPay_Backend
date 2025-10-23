using System.ComponentModel.DataAnnotations;

namespace DeBillPay_Backend.Models.Validation
{
    public class UkrainianPhoneAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return true;

            var phone = value.ToString()!;
            var cleanPhone = System.Text.RegularExpressions.Regex.Replace(phone, @"[\s\-\(\)]", "");

            return cleanPhone switch
            {
                string p when p.StartsWith("+380") && p.Length == 13 => true,
                string p when p.StartsWith("380") && p.Length == 12 => true,
                string p when p.StartsWith("0") && p.Length == 10 => true,
                _ => false
            };
        }

        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return phone;

            var cleanPhone = System.Text.RegularExpressions.Regex.Replace(phone, @"[^\d]", "");

            if (cleanPhone.StartsWith("380") && cleanPhone.Length == 12)
                return "+" + cleanPhone;
            else if (cleanPhone.StartsWith("0") && cleanPhone.Length == 10)
                return "+38" + cleanPhone;
            else
                return "+" + cleanPhone;
        }
    }
}
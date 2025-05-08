using System.ComponentModel.DataAnnotations;

namespace Backend_Development_Lab.Dtos
{
    public class CreatePaymentRequestDto
    {
        [Required]
        [Range(0.01, 1000000.00)]
        public decimal Amount { get; set; }
        [Required]
        [StringLength(3, MinimumLength = 3)] // Np. "PLN", "USD"
        public required string Currency { get; set; }

        public string? Description { get; set; }
    }
    public class CreatePaymentResponseDto
    {
        public Guid InternalOrderId { get; set; }
        public string? PayPalOrderId { get; set; }
        public required string ApprovalUrl { get; set; } // URL do przekierowania użytkownika
    }
}

namespace Backend_Development_Lab.Models
{
    public enum OrderStatus
    {
        Pending,      // Zamówienie utworzone, czeka na płatność
        Processing,   // Płatność w trakcie przetwarzania przez PayPal
        Completed,    // Płatność pomyślnie zakończona
        Failed,       // Płatność nie powiodła się
        Cancelled,    // Płatność anulowana przez użytkownika
        Refunded      // Płatność zwrócona
    }

    public class Order
    {
        public Guid Id { get; set; }                   // Nasz wewnętrzny ID zamówienia
        public string? PayPalOrderId { get; set; }     // ID zamówienia zwrócone przez PayPal
        public decimal Amount { get; set; }
        public required string Currency { get; set; }  // Np. "PLN", "USD"
        public string? Description { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Opcjonalnie: ID użytkownika, jeśli łączymy z systemem użytkowników
        // public Guid? UserId { get; set; }
        // public User? User { get; set; }

        public Order()
        {
            Id = Guid.NewGuid();
            Status = OrderStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }
    }
}

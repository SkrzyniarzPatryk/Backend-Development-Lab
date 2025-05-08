using Backend_Development_Lab.Models;

namespace Backend_Development_Lab.Interfaces
{
    public interface IPaymentService
    {
        Task<(Order? order, string? approvalUrl, string? errorMessage)> CreatePayPalOrderAsync(decimal amount, string currency, string description, string returnUrl, string cancelUrl);
        Task<Order?> CapturePayPalOrderAsync(string payPalOrderId);
        Task<Order[]> GetOrders();
        Task<Order?> GetOrderByIdAsync(Guid orderId); // Pobranie naszego zamówienia
        Task<Order?> GetOrderByPayPalIdAsync(string payPalOrderId); // Pobranie naszego zamówienia po ID PayPal

        void AddOrder(Order order);
        void UpdateOrder(Order order);
    }
}

using Backend_Development_Lab.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Backend_Development_Lab.Interfaces;

namespace Backend_Development_Lab.Services
{
    public class PaymentService :IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _payPalApiBaseUrl;
        private readonly string _payPalClientId;
        private readonly string _payPalClientSecret;

        private static readonly ConcurrentDictionary<Guid, Order> _orders = new ConcurrentDictionary<Guid, Order>();
        private static readonly ConcurrentDictionary<string, Guid> _payPalOrderIdIndex = new ConcurrentDictionary<string, Guid>();


        public PaymentService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("PayPalApiClient");

            _payPalApiBaseUrl = _configuration["PayPal:ApiBaseUrl"] ?? "https://api-m.sandbox.paypal.com";
            _payPalClientId = _configuration["PayPal:ClientId"] ?? throw new InvalidOperationException("PayPal ClientId not configured");
            _payPalClientSecret = _configuration["PayPal:ClientSecret"] ?? throw new InvalidOperationException("PayPal ClientSecret not configured");
        }

        // --- Zarządzanie lokalnymi zamówieniami (In-Memory) ---
        public Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            _orders.TryGetValue(orderId, out var order);
            return Task.FromResult(order);
        }
        public Task<Order?> GetOrderByPayPalIdAsync(string payPalOrderId)
        {
            if (_payPalOrderIdIndex.TryGetValue(payPalOrderId, out Guid internalId))
            {
                _orders.TryGetValue(internalId, out var order);
                return Task.FromResult(order);
            }
            return Task.FromResult<Order?>(null);
        }

        public void AddOrder(Order order)
        {
            _orders.TryAdd(order.Id, order);
            if (!string.IsNullOrEmpty(order.PayPalOrderId))
            {
                _payPalOrderIdIndex.TryAdd(order.PayPalOrderId, order.Id);
            }
        }

        public void UpdateOrder(Order order)
        {
            if (_orders.ContainsKey(order.Id))
            {
                _orders[order.Id] = order;
                if (!string.IsNullOrEmpty(order.PayPalOrderId) && !_payPalOrderIdIndex.ContainsKey(order.PayPalOrderId))
                {
                    _payPalOrderIdIndex.TryAdd(order.PayPalOrderId, order.Id);
                }
            }
        }


        // --- Interakcja z PayPal API ---
        private async Task<string?> GetPayPalAccessTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_payPalApiBaseUrl}/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_payPalClientId}:{_payPalClientSecret}")));

            var content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<PayPalAuthResponse>();
                return authResponse?.access_token;
            }
            Console.WriteLine($"Error getting PayPal access token: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            return null;
        }

        public async Task<(Order? order, string? approvalUrl, string? errorMessage)> CreatePayPalOrderAsync(decimal amount, string currency, string description, string returnUrl, string cancelUrl)
        {
            var accessToken = await GetPayPalAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return (null, null, "Failed to authenticate with PayPal.");
            }

            var internalOrder = new Order
            {
                Amount = amount,
                Currency = currency,
                Description = description,
                Status = OrderStatus.Pending
            };
            AddOrder(internalOrder);


            var payPalOrderRequest = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                        new
                        {
                            amount = new { currency_code = currency, value = amount.ToString("F2").Replace(',','.') }, // Formatuj kwotę na 2 miejsca po przecinku
                            description = description
                        }
                    },
                application_context = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl,
                    brand_name = "Moja Aplikacja Sklep",
                    user_action = "PAY_NOW"
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_payPalApiBaseUrl}/v2/checkout/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payPalOrderRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var payPalOrderResponse = JsonSerializer.Deserialize<PayPalOrderResponse>(responseContent);
                if (payPalOrderResponse != null && !string.IsNullOrEmpty(payPalOrderResponse.id))
                {
                    internalOrder.PayPalOrderId = payPalOrderResponse.id;
                    internalOrder.Status = OrderStatus.Processing;
                    UpdateOrder(internalOrder);

                    var approvalLink = payPalOrderResponse.links?.FirstOrDefault(l => l.rel == "approve");
                    if (approvalLink != null)
                    {
                        return (internalOrder, approvalLink.href, null);
                    }
                    return (internalOrder, null, "Approval link not found in PayPal response.");
                }
                return (internalOrder, null, $"Failed to parse PayPal order ID from response: {responseContent}");
            }
            Console.WriteLine($"Error creating PayPal order: {response.StatusCode} - {responseContent}");
            return (internalOrder, null, $"Error creating PayPal order: {response.StatusCode} - {responseContent}");
        }

        public async Task<Order?> CapturePayPalOrderAsync(string payPalOrderId)
        {
            var accessToken = await GetPayPalAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return null;
            }

            var internalOrder = await GetOrderByPayPalIdAsync(payPalOrderId);
            if (internalOrder == null)
            {
                Console.WriteLine($"Internal order not found for PayPal Order ID: {payPalOrderId}");
                return null;
            }

            if (internalOrder.Status == OrderStatus.Completed || internalOrder.Status == OrderStatus.Failed)
            {
                Console.WriteLine($"Order {payPalOrderId} already processed. Status: {internalOrder.Status}");
                return internalOrder;
            }


            var request = new HttpRequestMessage(HttpMethod.Post, $"{_payPalApiBaseUrl}/v2/checkout/orders/{payPalOrderId}/capture");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var captureResponse = JsonSerializer.Deserialize<PayPalCaptureResponse>(responseContent);
                if (captureResponse?.status == "COMPLETED")
                {
                    internalOrder.Status = OrderStatus.Completed;
                    internalOrder.UpdatedAt = DateTime.UtcNow;
                    UpdateOrder(internalOrder);
                    return internalOrder;
                }
                else
                {
                    internalOrder.Status = OrderStatus.Failed;
                    internalOrder.UpdatedAt = DateTime.UtcNow;
                    UpdateOrder(internalOrder);
                    Console.WriteLine($"PayPal capture status for {payPalOrderId} was not COMPLETED: {captureResponse?.status} - {responseContent}");
                    return internalOrder;
                }
            }
            else
            {
                internalOrder.Status = OrderStatus.Failed;
                internalOrder.UpdatedAt = DateTime.UtcNow;
                UpdateOrder(internalOrder);
                Console.WriteLine($"Error capturing PayPal order {payPalOrderId}: {response.StatusCode} - {responseContent}");
                return internalOrder;
            }
        }

        public Task<Order[]> GetOrders()
        {
            return Task.FromResult(_orders.Values.ToArray());
        }
    }

    // Pomocnicze klasy DTO dla odpowiedzi PayPal
    public class PayPalAuthResponse
    {
        public string? scope { get; set; }
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public string? app_id { get; set; }
        public int expires_in { get; set; }
        public string? nonce { get; set; }
    }

    public class PayPalOrderResponse
    {
        public string? id { get; set; }
        public string? status { get; set; } // Np. CREATED, SAVED, APPROVED, VOIDED, COMPLETED
        public List<PayPalLinkDescription>? links { get; set; }
    }

    public class PayPalCaptureResponse
    {
        public string? id { get; set; } // To jest ID capture, nie order ID
        public string? status { get; set; } // Np. COMPLETED, DECLINED, PARTIALLY_REFUNDED, PENDING, REFUNDED
                                            // ... inne pola, które mogą być przydatne ...
    }


    public class PayPalLinkDescription
    {
        public string? href { get; set; }
        public string? rel { get; set; } // Np. "self", "approve", "capture"
        public string? method { get; set; }
    }
}

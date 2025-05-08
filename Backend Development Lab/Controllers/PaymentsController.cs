using Backend_Development_Lab.Dtos;
using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Development_Lab.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger; // Do logowania

        public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        // 1. Endpoint inicjujący płatność
        [HttpPost("create")]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // URL-e powrotu do Twojej aplikacji
            // WAŻNE: Muszą być publicznie dostępne, jeśli PayPal ma na nie przekierować.
            // Dla testów lokalnych z np. ngrok lub bezpośrednio jeśli masz publiczny IP.
            // W tym scenariuszu, gdzie klient sam sprawdza, mogą to być ścieżki w Twoim SPA.
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string returnUrl = $"{baseUrl}/api/payments/success"; // Endpoint, który obsłuży sukces
            string cancelUrl = $"{baseUrl}/api/payments/cancel";   // Endpoint, który obsłuży anulowanie

            _logger.LogInformation($"Creating PayPal order. Amount: {requestDto.Amount} {requestDto.Currency}. Return: {returnUrl}, Cancel: {cancelUrl}");

            var (order, approvalUrl, errorMessage) = await _paymentService.CreatePayPalOrderAsync(
                requestDto.Amount,
                requestDto.Currency,
                requestDto.Description ?? "Zakup w Mojej Aplikacji",
                returnUrl,
                cancelUrl);

            if (order == null || string.IsNullOrEmpty(approvalUrl))
            {
                _logger.LogError($"Failed to create PayPal order: {errorMessage}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to create payment order.", error = errorMessage });
            }

            _logger.LogInformation($"PayPal order created. Internal ID: {order.Id}, PayPal ID: {order.PayPalOrderId}, Approval URL: {approvalUrl}");

            return Ok(new CreatePaymentResponseDto
            {
                InternalOrderId = order.Id,
                PayPalOrderId = order.PayPalOrderId,
                ApprovalUrl = approvalUrl
            });
        }

        // 2. Endpoint obsługujący pomyślne przekierowanie z PayPal
        //    Użytkownik jest tu przekierowywany PO dokonaniu płatności w PayPal.
        //    Tutaj odpytujemy PayPal o status płatności (capture).
        [HttpGet("success")] // Lub inny URL skonfigurowany jako return_url
        public async Task<IActionResult> PaymentSuccess([FromQuery] string token, [FromQuery(Name = "PayerID")] string payerId)
        {
            // 'token' z query string to PayPal Order ID (w nowszym API PayPal)
            // 'PayerID' jest również zwracany przez PayPal
            _logger.LogInformation($"Payment success callback. PayPal Order ID (token): {token}, PayerID: {payerId}");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("PaymentSuccess: PayPal Order ID (token) is missing.");
                // Zwróć użytkownika na stronę błędu lub odpowiedni widok w SPA
                return BadRequest("Payment confirmation failed: Missing PayPal Order ID.");
            }

            Order? capturedOrder = await _paymentService.CapturePayPalOrderAsync(token);

            if (capturedOrder == null)
            {
                _logger.LogError($"PaymentSuccess: Failed to capture or find order for PayPal Order ID: {token}");
                // Zwróć użytkownika na stronę błędu
                return StatusCode(StatusCodes.Status500InternalServerError, "Error processing payment after confirmation.");
            }

            if (capturedOrder.Status == OrderStatus.Completed)
            {
                _logger.LogInformation($"Payment completed successfully for PayPal Order ID: {token}, Internal Order ID: {capturedOrder.Id}");
                // Tutaj logika po udanej płatności, np.:
                // - Przekieruj użytkownika na stronę podsumowania zamówienia w Twojej aplikacji
                // - Wyświetl komunikat o sukcesie
                // W kontekście API, możemy zwrócić dane zamówienia
                return Ok(new { message = "Payment completed successfully!", orderId = capturedOrder.Id, payPalOrderId = capturedOrder.PayPalOrderId, status = capturedOrder.Status });
            }
            else
            {
                _logger.LogWarning($"Payment for PayPal Order ID: {token} was not completed successfully after capture. Status: {capturedOrder.Status}");
                // Płatność mogła się nie powieść na etapie capture lub miała inny status
                // Zwróć użytkownika na stronę z informacją o problemie
                return BadRequest(new { message = "Payment was not completed successfully.", orderId = capturedOrder.Id, status = capturedOrder.Status });
            }
        }


        // 3. Endpoint obsługujący anulowanie płatności przez użytkownika w PayPal
        [HttpGet("cancel")] // Lub inny URL skonfigurowany jako cancel_url
        public async Task<IActionResult> PaymentCancel([FromQuery] string token)
        {
            // 'token' z query string to PayPal Order ID
            _logger.LogInformation($"Payment cancelled by user. PayPal Order ID (token): {token}");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("PaymentCancel: PayPal Order ID (token) is missing.");
                return BadRequest("Payment cancellation information is incomplete.");
            }

            // Znajdź zamówienie w naszej bazie i oznacz je jako anulowane
            var order = await _paymentService.GetOrderByPayPalIdAsync(token);
            if (order != null)
            {
                if (order.Status != OrderStatus.Completed) // Nie zmieniaj statusu, jeśli już zapłacone
                {
                    order.Status = OrderStatus.Cancelled;
                    order.UpdatedAt = DateTime.UtcNow;
                    _paymentService.UpdateOrder(order); // Załóżmy, że serwis ma metodę UpdateOrder
                    _logger.LogInformation($"Internal order {order.Id} (PayPal ID: {token}) marked as Cancelled.");
                }
            }
            else
            {
                _logger.LogWarning($"PaymentCancel: Could not find internal order for PayPal ID {token} to mark as cancelled.");
            }

            // Przekieruj użytkownika na odpowiednią stronę w Twojej aplikacji (np. koszyk)
            // W kontekście API, możemy zwrócić informację
            return Ok(new { message = "Payment was cancelled by the user.", payPalOrderId = token });
        }

        [HttpGet("orders")] // Lub inny URL skonfigurowany jako cancel_url
        public async Task<IActionResult> GetOrders()
        {
            // Znajdź zamówienia w naszej bazie i oznacz je jako anulowane
            var orders = await _paymentService.GetOrders();
            if (orders != null)
            {
                return Ok(orders);
            }
            else
            {
                _logger.LogWarning($"GetOrders: Could not find internal order for PayPal ID to mark as cancelled.");
                return NotFound("No orders found.");
            }
        }

        [HttpPost("orders/{orderId}/update")] // Endpoint do ręcznego przechwytywania płatności
        public async Task<IActionResult> UpdatePayment([FromRoute] Guid orderId, [FromBody] OrderStatus status )
        {
            var order = await _paymentService.GetOrderByIdAsync(orderId);
            if (order != null)
            {
               order.Status = status;
               var orderV2 = await _paymentService.GetOrderByIdAsync(orderId);
               return Ok(orderV2);
            }
            else
            {
                return NotFound("Order not found.");
            }
        }

        // Endpoint do symulacji Webhooka (Uproszczona wersja, NIEBEZPIECZNA bez walidacji)
        // W tym podejściu (bez prawdziwych webhooków) ten endpoint jest mniej potrzebny,
        // bo status sprawdzamy w "success" i "cancel".
        // Jeśli miałby być używany, MUSI być zabezpieczony.
        // [HttpPost("webhook")]
        // public async Task<IActionResult> PayPalWebhook([FromBody] object payload)
        // {
        //     _logger.LogInformation("PayPal Webhook received.");
        //     // BARDZO WAŻNE: Weryfikacja podpisu webhooka od PayPal jest tutaj pominięta dla uproszczenia!
        //     // W produkcji jest to absolutnie krytyczne dla bezpieczeństwa.
        //     // https://developer.paypal.com/docs/api/webhooks/v1/#verify-webhook-signature

        //     // Tutaj przetwarzanie payloadu webhooka i aktualizacja statusu zamówienia
        //     // np. odczytanie event_type, resource.id (PayPal Order ID)
        //     // var payPalOrderId = ...;
        //     // var eventType = ...;
        //     // if (eventType == "CHECKOUT.ORDER.APPROVED" || eventType == "PAYMENT.CAPTURE.COMPLETED")
        //     // {
        //     //    var order = await _paymentService.GetOrderByPayPalIdAsync(payPalOrderId);
        //     //    if (order != null && order.Status != OrderStatus.Completed)
        //     //    {
        //     //        var capturedOrder = await _paymentService.CapturePayPalOrderAsync(payPalOrderId); // Można by też od razu oznaczyć jako Completed
        //     //        // ... obsłuż wynik capture ...
        //     //    }
        //     // }
        //     // else if (eventType == "PAYMENT.CAPTURE.DENIED" || eventType == "CHECKOUT.ORDER.VOIDED")
        //     // { /* ... obsłuż błąd ... */ }


        //     _logger.LogInformation($"Webhook payload: {JsonSerializer.Serialize(payload)}");
        //     return Ok();
        // }
    }
}
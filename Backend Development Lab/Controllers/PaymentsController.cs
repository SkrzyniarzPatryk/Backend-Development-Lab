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
        private readonly ILogger<PaymentsController> _logger;

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

            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string returnUrl = $"{baseUrl}/api/payments/success";
            string cancelUrl = $"{baseUrl}/api/payments/cancel";

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
        [HttpGet("success")]
        public async Task<IActionResult> PaymentSuccess([FromQuery] string token, [FromQuery(Name = "PayerID")] string payerId)
        {
            _logger.LogInformation($"Payment success callback. PayPal Order ID (token): {token}, PayerID: {payerId}");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("PaymentSuccess: PayPal Order ID (token) is missing.");
                return BadRequest("Payment confirmation failed: Missing PayPal Order ID.");
            }

            Order? capturedOrder = await _paymentService.CapturePayPalOrderAsync(token);

            if (capturedOrder == null)
            {
                _logger.LogError($"PaymentSuccess: Failed to capture or find order for PayPal Order ID: {token}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error processing payment after confirmation.");
            }

            if (capturedOrder.Status == OrderStatus.Completed)
            {
                _logger.LogInformation($"Payment completed successfully for PayPal Order ID: {token}, Internal Order ID: {capturedOrder.Id}");
                return Ok(new { message = "Payment completed successfully!", orderId = capturedOrder.Id, payPalOrderId = capturedOrder.PayPalOrderId, status = capturedOrder.Status });
            }
            else
            {
                _logger.LogWarning($"Payment for PayPal Order ID: {token} was not completed successfully after capture. Status: {capturedOrder.Status}");
                return BadRequest(new { message = "Payment was not completed successfully.", orderId = capturedOrder.Id, status = capturedOrder.Status });
            }
        }


        // 3. Endpoint obsługujący anulowanie płatności przez użytkownika w PayPal
        [HttpGet("cancel")]
        public async Task<IActionResult> PaymentCancel([FromQuery] string token)
        {
            _logger.LogInformation($"Payment cancelled by user. PayPal Order ID (token): {token}");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("PaymentCancel: PayPal Order ID (token) is missing.");
                return BadRequest("Payment cancellation information is incomplete.");
            }

            var order = await _paymentService.GetOrderByPayPalIdAsync(token);
            if (order != null)
            {
                if (order.Status != OrderStatus.Completed)
                {
                    order.Status = OrderStatus.Cancelled;
                    order.UpdatedAt = DateTime.UtcNow;
                    _paymentService.UpdateOrder(order);
                    _logger.LogInformation($"Internal order {order.Id} (PayPal ID: {token}) marked as Cancelled.");
                }
            }
            else
            {
                _logger.LogWarning($"PaymentCancel: Could not find internal order for PayPal ID {token} to mark as cancelled.");
            }

            return Ok(new { message = "Payment was cancelled by the user.", payPalOrderId = token });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
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

        [HttpPost("orders/{orderId}/update")]
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
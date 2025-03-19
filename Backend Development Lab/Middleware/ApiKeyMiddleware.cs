namespace Backend_Development_Lab.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private const string API_KEY = "X-API-KEY";
        private const string API_VALUE = "123456";

        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(API_KEY, out var extractedApiKey) || extractedApiKey != API_VALUE)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized: Invalid API Key");
                return;
            }
            await _next(context);
        }
    }
}
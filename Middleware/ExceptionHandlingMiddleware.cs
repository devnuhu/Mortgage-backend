using MMGT.API.Exceptions;
using MMGT.API.DTOs;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MMGT.API.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                _logger.LogError($"Unhandled exception: {exception}");
                await HandleExceptionAsync(context, exception);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse
            {
                Timestamp = DateTime.UtcNow,
                TraceId = context.TraceIdentifier
            };

            switch (exception)
            {
                case AuthenticationException ex:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case AuthorizationException ex:
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    response.StatusCode = StatusCodes.Status403Forbidden;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case ResourceNotFoundException ex:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    response.StatusCode = StatusCodes.Status404NotFound;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case ValidationException ex:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    response.ValidationErrors = ex.ValidationErrors;
                    break;

                case BusinessRuleException ex:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case FileOperationException ex:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case DatabaseException ex:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case ExternalServiceException ex:
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    response.StatusCode = StatusCodes.Status502BadGateway;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case TokenException ex:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;

                case MMGT.API.Exceptions.TimeoutException ex:
                    context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                    response.StatusCode = StatusCodes.Status408RequestTimeout;
                    response.ErrorCode = ex.ErrorCode;
                    response.Message = ex.Message;
                    break;


                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.ErrorCode = "INTERNAL_SERVER_ERROR";
                    response.Message = "An unexpected error occurred. Please contact support if the problem persists.";
#if DEBUG
                    response.Details = exception.Message;
#endif
                    break;
            }

            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}

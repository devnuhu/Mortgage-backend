namespace MMGT.API.Exceptions
{
    /// <summary>
    /// Custom exception class for application-specific errors with proper HTTP status codes and user-friendly messages
    /// </summary>
    public class ApplicationException : Exception
    {
        public int StatusCode { get; set; }
        public string ErrorCode { get; set; }
        public Dictionary<string, string[]>? ValidationErrors { get; set; }

        public ApplicationException(string message, int statusCode = 400, string errorCode = "GENERAL_ERROR") 
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        public ApplicationException(string message, int statusCode, string errorCode, Dictionary<string, string[]> validationErrors) 
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            ValidationErrors = validationErrors;
        }
    }

    /// <summary>
    /// Thrown when authentication fails (login, invalid credentials, etc.)
    /// </summary>
    public class AuthenticationException : ApplicationException
    {
        public AuthenticationException(string message) 
            : base(message, 401, "AUTHENTICATION_FAILED") { }
    }

    /// <summary>
    /// Thrown when user doesn't have permission to access a resource
    /// </summary>
    public class AuthorizationException : ApplicationException
    {
        public AuthorizationException(string message) 
            : base(message, 403, "AUTHORIZATION_FAILED") { }
    }

    /// <summary>
    /// Thrown when a requested resource is not found
    /// </summary>
    public class ResourceNotFoundException : ApplicationException
    {
        public ResourceNotFoundException(string resourceName, string identifier) 
            : base($"{resourceName} with identifier '{identifier}' not found", 404, "RESOURCE_NOT_FOUND") { }

        public ResourceNotFoundException(string message) 
            : base(message, 404, "RESOURCE_NOT_FOUND") { }
    }

    /// <summary>
    /// Thrown when validation of input data fails
    /// </summary>
    public class ValidationException : ApplicationException
    {
        public ValidationException(string message) 
            : base(message, 400, "VALIDATION_ERROR") { }

        public ValidationException(string message, Dictionary<string, string[]> errors) 
            : base(message, 400, "VALIDATION_ERROR", errors) { }
    }

    /// <summary>
    /// Thrown when a business rule is violated
    /// </summary>
    public class BusinessRuleException : ApplicationException
    {
        public BusinessRuleException(string message) 
            : base(message, 400, "BUSINESS_RULE_VIOLATED") { }
    }

    /// <summary>
    /// Thrown when a file operation fails
    /// </summary>
    public class FileOperationException : ApplicationException
    {
        public FileOperationException(string message) 
            : base(message, 400, "FILE_OPERATION_FAILED") { }
    }

    /// <summary>
    /// Thrown when a database operation fails
    /// </summary>
    public class DatabaseException : ApplicationException
    {
        public DatabaseException(string message) 
            : base(message, 500, "DATABASE_ERROR") { }
    }

    /// <summary>
    /// Thrown when an external service call fails
    /// </summary>
    public class ExternalServiceException : ApplicationException
    {
        public ExternalServiceException(string message, string serviceName) 
            : base($"Error communicating with {serviceName}: {message}", 502, "EXTERNAL_SERVICE_ERROR") { }
    }

    /// <summary>
    /// Thrown when token validation fails
    /// </summary>
    public class TokenException : ApplicationException
    {
        public TokenException(string message) 
            : base(message, 401, "TOKEN_INVALID") { }
    }

    /// <summary>
    /// Thrown when an operation times out
    /// </summary>
    public class TimeoutException : ApplicationException
    {
        public TimeoutException(string message) 
            : base(message, 408, "REQUEST_TIMEOUT") { }
    }
}

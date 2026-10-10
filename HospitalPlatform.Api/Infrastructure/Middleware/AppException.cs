namespace HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;

public abstract class AppException(string message, int statusCode, string errorCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string errorCode { get; } = errorCode;
}

public class BadRequestException(string message)
    : AppException(message, StatusCodes.Status400BadRequest, "bad_request");

public class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict, "conflict");

public class ForbiddenException(string message)
    : AppException(message, StatusCodes.Status403Forbidden, "forbidden");

public class NotFoundException(string message)
    : AppException(message, StatusCodes.Status404NotFound, "not_found");

public class UnauthorizedException(string message)
    : AppException(message, StatusCodes.Status401Unauthorized, "unauthorized");
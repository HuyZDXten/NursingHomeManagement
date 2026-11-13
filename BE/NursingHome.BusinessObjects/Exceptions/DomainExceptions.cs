namespace NursingHome.BusinessObjects.Exceptions;

// Đặt ở BusinessObjects (thay vì Services như ASS2) để DAO cũng ném được lỗi nghiệp vụ
// từ bên trong transaction. ExceptionHandlingMiddleware ở API đổi chúng thành HTTP status.

/// <summary>404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>409: dữ liệu trùng hoặc đang được dùng.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>400: vi phạm quy tắc nghiệp vụ.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>401: sai thông tin đăng nhập hoặc phiên không hợp lệ.</summary>
public class UnauthorizedServiceException(string message) : Exception(message);

/// <summary>403: đã đăng nhập nhưng không có quyền với đối tượng này.</summary>
public class ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.") : Exception(message);

/// <summary>429.</summary>
public class TooManyRequestsException(string message) : Exception(message);

/// <summary>503: dịch vụ ngoài (SMTP, PayOS, AI) chưa cấu hình hoặc không phản hồi.</summary>
public class ServiceUnavailableException(string message) : Exception(message);

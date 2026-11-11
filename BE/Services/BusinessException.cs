namespace NursingHomeManagement.Services;

public sealed class BusinessException(string message) : Exception(message);

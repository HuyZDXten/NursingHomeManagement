namespace NursingHome.Services.Helpers;

/// <summary>Entity → DTO của module Vận hành &amp; Tài chính (Viên). Phần lõi vẫn ở DtoMapper.cs.</summary>
/// Mỗi task viết phần của mình ở file riêng OperationsMapper.&lt;Task&gt;.cs (partial) để không đụng nhau.
public static partial class OperationsMapper
{
    // Ví dụ: public static InquiryDto ToDto(this Inquiry inquiry) => new() { ... };  → OperationsMapper.Admissions.cs
}

/// <summary>Entity → DTO của module Chăm sóc &amp; Gia đình (Huy). Phần lõi vẫn ở DtoMapper.cs.</summary>
public static class CareMapper
{
    // Ví dụ: public static IncidentDto ToDto(this Incident incident) => new() { ... };
}

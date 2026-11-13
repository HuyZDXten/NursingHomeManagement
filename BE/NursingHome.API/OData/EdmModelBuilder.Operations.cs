using Microsoft.OData.ModelBuilder;

namespace NursingHome.API.OData;

/// <summary>MODULE VẬN HÀNH &amp; TÀI CHÍNH (Viên): entity set OData của module.</summary>
public static partial class EdmModelBuilder
{
    static partial void AddOperationsEntitySets(ODataConventionModelBuilder builder)
    {
        // Ví dụ (S3-V1): builder.EntitySet<InquiryDto>("Inquiries");
    }
}

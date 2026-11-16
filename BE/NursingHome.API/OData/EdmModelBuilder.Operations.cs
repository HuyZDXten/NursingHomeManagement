using Microsoft.OData.ModelBuilder;
using NursingHome.DTOs.Admissions;

namespace NursingHome.API.OData;

/// <summary>MODULE VẬN HÀNH &amp; TÀI CHÍNH (Viên): entity set OData của module.</summary>
public static partial class EdmModelBuilder
{
    static partial void AddOperationsEntitySets(ODataConventionModelBuilder builder)
    {
        builder.EntitySet<InquiryDto>("Inquiries"); // S3-V1
    }
}

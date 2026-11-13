using Microsoft.OData.ModelBuilder;

namespace NursingHome.API.OData;

/// <summary>MODULE CHĂM SÓC &amp; GIA ĐÌNH (Huy): entity set OData của module.</summary>
public static partial class EdmModelBuilder
{
    static partial void AddCareEntitySets(ODataConventionModelBuilder builder)
    {
        // Ví dụ (S3-H1): builder.EntitySet<IncidentDto>("Incidents");
    }
}

using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Payments;
using NursingHome.DTOs.Residents;

namespace NursingHome.API.OData;

/// <summary>
/// Các màn danh sách dùng OData ($filter, $orderby, $top, $skip, $count) như ASS2.
/// Thêm entity set mới: khai báo trong file của module (EdmModelBuilder.Operations.cs / .Care.cs)
/// + một ODataController có action [EnableQuery] [Route("odata/TenEntitySet")].
/// </summary>
public static partial class EdmModelBuilder
{
    public static IEdmModel Build()
    {
        var builder = new ODataConventionModelBuilder();
        // camelCase giống JSON của các API REST: FE dùng chung một kiểu (vd. $filter=status eq 'Active').
        builder.EnableLowerCamelCase();
        builder.EntitySet<ResidentListItemDto>("Residents");
        builder.EntitySet<AccountDto>("Accounts");
        builder.EntitySet<InvoiceListItemDto>("Invoices");
        builder.EntitySet<CareTaskDto>("CareTasks");
        builder.EntitySet<PaymentTransactionDto>("PaymentTransactions");
        AddOperationsEntitySets(builder);
        AddCareEntitySets(builder);
        return builder.GetEdmModel();
    }

    static partial void AddOperationsEntitySets(ODataConventionModelBuilder builder);

    static partial void AddCareEntitySets(ODataConventionModelBuilder builder);
}

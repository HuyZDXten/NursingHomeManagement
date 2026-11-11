using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Services;

namespace NursingHomeManagement.Controllers;

public abstract class WorkflowController : Controller
{
    protected async Task<bool> TrySaveAsync(Func<Task> operation)
    {
        if (!ModelState.IsValid)
        {
            return false;
        }

        try
        {
            await operation();
            TempData["Success"] = "Đã lưu thay đổi thành công.";
            return true;
        }
        catch (BusinessException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (DatabaseConflict.IsExpected(exception))
        {
            ModelState.AddModelError(string.Empty,
                "Dữ liệu bị trùng, đang được sử dụng hoặc vừa thay đổi bởi người khác. Vui lòng tải lại trang và kiểm tra trước khi thử lại.");
        }

        return false;
    }
}

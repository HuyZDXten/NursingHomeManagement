# File Phase 2–6 và refactor Phase 1

Các file dưới đây được tạo mới hoặc chỉnh sửa trong đợt triển khai. Không liệt kê bin/obj/work hoặc từng file thư viện bên thứ ba.

## Refactor và hạ tầng

- `BE/Program.cs`
- `BE/Extensions/ServiceCollectionExtensions.cs`
- `BE/Data/ApplicationDbContext.cs`
- `BE/Data/DbInitializer.cs`
- `BE/Data/DemoScenario.cs`
- `BE/Models/ApplicationUser.cs`
- `BE/Models/BaseEntity.cs`
- `BE/Models/Bed.cs`
- `BE/Models/BedAssignment.cs`
- `BE/Models/CareRecord.cs`
- `BE/Models/CareTask.cs`
- `BE/Models/Enums.cs`
- `BE/Models/ErrorViewModel.cs`
- `BE/Models/FamilyContact.cs`
- `BE/Models/Invoice.cs`
- `BE/Models/InvoiceItem.cs`
- `BE/Models/MedicationLog.cs`
- `BE/Models/MedicationSchedule.cs`
- `BE/Models/Payment.cs`
- `BE/Models/Resident.cs`
- `BE/Models/ResidentFamilyLink.cs`
- `BE/Models/ResidentSubscription.cs`
- `BE/Models/Room.cs`
- `BE/Models/ServicePackage.cs`
- `BE/Models/VitalSign.cs`
- `BE/Services/CurrentUser.cs`
- `BE/Services/ResidentAccessService.cs`
- `BE/Services/DatabaseConflict.cs`
- `BE/Services/AccessExceptionMiddleware.cs`
- `BE/Services/FacilityClock.cs`
- `BE/Controllers/WorkflowController.cs`

## Controllers các module

- `BE/Controllers/AccountController.cs`
- `BE/Controllers/BedsController.cs`
- `BE/Controllers/BillingController.cs`
- `BE/Controllers/CaregiverController.cs`
- `BE/Controllers/CareTasksController.cs`
- `BE/Controllers/DashboardController.cs`
- `BE/Controllers/FamilyController.cs`
- `BE/Controllers/HealthController.cs`
- `BE/Controllers/HomeController.cs`
- `BE/Controllers/MedicationController.cs`
- `BE/Controllers/ResidentsController.cs`
- `BE/Controllers/RoomsController.cs`
- `BE/Controllers/UsersController.cs`

## Services nghiệp vụ

- `BE/Services/BillingService.cs`
- `BE/Services/BusinessException.cs`
- `BE/Services/CareTaskService.cs`
- `BE/Services/DashboardService.cs`
- `BE/Services/FamilyService.cs`
- `BE/Services/HealthService.cs`
- `BE/Services/LookupService.cs`
- `BE/Services/MedicationService.cs`
- `BE/Services/ResidentService.cs`
- `BE/Services/RoomService.cs`
- `BE/Services/UserAccountService.cs`

## ViewModels và form validation

- `BE/ViewModels/BedAssignmentForm.cs`
- `BE/ViewModels/BedForm.cs`
- `BE/ViewModels/CareTaskForm.cs`
- `BE/ViewModels/CareUpdateForm.cs`
- `BE/ViewModels/ChangePasswordForm.cs`
- `BE/ViewModels/ContactForm.cs`
- `BE/ViewModels/DashboardViewModel.cs`
- `BE/ViewModels/FamilyDetailsViewModel.cs`
- `BE/ViewModels/FamilyLinkForm.cs`
- `BE/ViewModels/HealthForm.cs`
- `BE/ViewModels/InvoiceForm.cs`
- `BE/ViewModels/InvoiceItemForm.cs`
- `BE/ViewModels/LoginViewModel.cs`
- `BE/ViewModels/MedicationDay.cs`
- `BE/ViewModels/MedicationForm.cs`
- `BE/ViewModels/MedicationLogForm.cs`
- `BE/ViewModels/PackageForm.cs`
- `BE/ViewModels/PagedList.cs`
- `BE/ViewModels/PaymentForm.cs`
- `BE/ViewModels/ResidentForm.cs`
- `BE/ViewModels/RoomForm.cs`
- `BE/ViewModels/SubscriptionForm.cs`
- `BE/ViewModels/UserEditForm.cs`
- `BE/ViewModels/UserForm.cs`

## Razor Views

- `BE/Views/_ViewImports.cshtml`
- `BE/Views/_ViewStart.cshtml`
- `BE/Views/Account/AccessDenied.cshtml`
- `BE/Views/Account/Login.cshtml`
- `BE/Views/Beds/History.cshtml`
- `BE/Views/Billing/Details.cshtml`
- `BE/Views/Billing/Index.cshtml`
- `BE/Views/Billing/Packages.cshtml`
- `BE/Views/Billing/Subscriptions.cshtml`
- `BE/Views/Caregiver/Index.cshtml`
- `BE/Views/CareTasks/History.cshtml`
- `BE/Views/CareTasks/Index.cshtml`
- `BE/Views/Dashboard/Index.cshtml`
- `BE/Views/Family/Details.cshtml`
- `BE/Views/Family/Index.cshtml`
- `BE/Views/Health/Index.cshtml`
- `BE/Views/Home/Index.cshtml`
- `BE/Views/Home/Welcome.cshtml`
- `BE/Views/Medication/History.cshtml`
- `BE/Views/Medication/Index.cshtml`
- `BE/Views/Medication/Today.cshtml`
- `BE/Views/Residents/Details.cshtml`
- `BE/Views/Residents/Index.cshtml`
- `BE/Views/Rooms/Details.cshtml`
- `BE/Views/Rooms/Index.cshtml`
- `BE/Views/Shared/_Layout.cshtml`
- `BE/Views/Shared/_Layout.cshtml.css`
- `BE/Views/Shared/_Pagination.cshtml`
- `BE/Views/Shared/_ValidationScriptsPartial.cshtml`
- `BE/Views/Shared/EditorTemplates/BedAssignmentForm.cshtml`
- `BE/Views/Shared/EditorTemplates/BedForm.cshtml`
- `BE/Views/Shared/EditorTemplates/CareTaskForm.cshtml`
- `BE/Views/Shared/EditorTemplates/CareUpdateForm.cshtml`
- `BE/Views/Shared/EditorTemplates/ChangePasswordForm.cshtml`
- `BE/Views/Shared/EditorTemplates/ContactForm.cshtml`
- `BE/Views/Shared/EditorTemplates/FamilyLinkForm.cshtml`
- `BE/Views/Shared/EditorTemplates/HealthForm.cshtml`
- `BE/Views/Shared/EditorTemplates/InvoiceForm.cshtml`
- `BE/Views/Shared/EditorTemplates/InvoiceItemForm.cshtml`
- `BE/Views/Shared/EditorTemplates/MedicationForm.cshtml`
- `BE/Views/Shared/EditorTemplates/MedicationLogForm.cshtml`
- `BE/Views/Shared/EditorTemplates/PackageForm.cshtml`
- `BE/Views/Shared/EditorTemplates/PaymentForm.cshtml`
- `BE/Views/Shared/EditorTemplates/ResidentForm.cshtml`
- `BE/Views/Shared/EditorTemplates/RoomForm.cshtml`
- `BE/Views/Shared/EditorTemplates/SubscriptionForm.cshtml`
- `BE/Views/Shared/EditorTemplates/UserEditForm.cshtml`
- `BE/Views/Shared/EditorTemplates/UserForm.cshtml`
- `BE/Views/Shared/Error.cshtml`
- `BE/Views/Shared/Form.cshtml`
- `BE/Views/Shared/Operation.cshtml`
- `BE/Views/Users/Index.cshtml`

## Migrations

- `BE/Migrations/20261110161615_InitialCreate.cs`
- `BE/Migrations/20261110161615_InitialCreate.Designer.cs`
- `BE/Migrations/20261110164100_AddWorkflowConstraints.cs`
- `BE/Migrations/20261110164100_AddWorkflowConstraints.Designer.cs`
- `BE/Migrations/ApplicationDbContextModelSnapshot.cs`

## Giao diện tĩnh

- `BE/wwwroot/css/site.css`
- `BE/wwwroot/js/site.js`

## Kiểm thử

- `Tests/FoundationTests.cs`
- `Tests/WorkflowFixture.cs`
- `Tests/WorkflowTests.cs`

## Tài liệu và cấu hình

- `.editorconfig`
- `.gitignore`
- `README.md`
- `DELIVERY.md`
- `Start-Local.ps1`
- `Test-Local.ps1`
- `FE/README.md`

Bootstrap Icons 1.11.3 được lưu local trong `BE/wwwroot/lib/bootstrap-icons/`, kèm LICENSE.
Các migration InitialCreate cũ được giữ nguyên; chỉ thêm migration AddWorkflowConstraints.

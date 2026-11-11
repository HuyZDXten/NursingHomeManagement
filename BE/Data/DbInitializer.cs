using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool demo, IConfiguration configuration)
    {
        var database = services.GetRequiredService<ApplicationDbContext>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                EnsureSuccess(await roles.CreateAsync(new IdentityRole(role)));
            }
        }

        if (!demo)
        {
            return;
        }

        string password = configuration["Seed:DemoPassword"] ?? throw new InvalidOperationException("Set Seed__DemoPassword before seeding demo accounts.");
        await SeedAccountsAsync(users, password);
        await SeedResidentsAsync(database, users);
        await DemoScenario.SeedAsync(database, users);
    }

    private static async Task SeedAccountsAsync(UserManager<ApplicationUser> users, string password)
    {
        foreach (var role in Roles.All)
        {
            string email = $"{role.ToLowerInvariant()}@demo.nursinghome.test";
            if (await users.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"Demo {role}"
            };
            EnsureSuccess(await users.CreateAsync(user, password));
            EnsureSuccess(await users.AddToRoleAsync(user, role));
        }
    }

    private static async Task SeedResidentsAsync(ApplicationDbContext database, UserManager<ApplicationUser> users)
    {
        if (!await database.Residents.AnyAsync(resident => resident.Code == "DEMO-001"))
        {
            var resident = new Resident
            {
                Code = "DEMO-001",
                FullName = "Cư dân minh họa 01",
                DateOfBirth = new DateOnly(1945, 1, 1),
                Notes = "Dữ liệu giả lập phục vụ học tập."
            };
            database.Residents.Add(resident);
            var family = await users.FindByEmailAsync("family@demo.nursinghome.test");
            database.ResidentFamilyLinks.Add(new ResidentFamilyLink { Resident = resident, FamilyUserId = family!.Id, CanViewCare = true });
        }

        if (!await database.Rooms.AnyAsync(room => room.Number == "DEMO-101"))
        {
            database.Rooms.Add(new Room { Number = "DEMO-101", Description = "Phòng minh họa", Beds = [new Bed { Number = "A" }, new Bed { Number = "B" }] });
        }

        if (!await database.ServicePackages.AnyAsync(package => package.Name == "Gói demo cơ bản"))
        {
            database.ServicePackages.Add(new ServicePackage { Name = "Gói demo cơ bản", MonthlyPrice = 8000000 });
        }

        await database.SaveChangesAsync();
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}

using HassioOneDriveBackup.Models;
using HassioOneDriveBackup.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HassioOneDriveBackup.Tests;

public class RetentionPolicyServiceTests
{
    private static BackupSettings NoGenerational(int maxCount) => new()
    {
        MaxLocalBackups = maxCount
    };

    private static Backup Backup(string slug, DateTime date, bool retained = false) => new()
    {
        Slug = slug,
        Name = slug,
        Date = date,
        Size = "1 MB",
        BackupType = "Full"
    };

    private static RetentionPolicyService MakeService(DateTime now)
    {
        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(x => x.Now).Returns(now);
        return new RetentionPolicyService(dt.Object, NullLogger<RetentionPolicyService>.Instance);
    }

    // --- Max count ---

    [Fact]
    public void MaxCount_NoExcess_ReturnsEmpty()
    {
        var svc = MakeService(new DateTime(2024, 6, 1));
        var backups = new[] { Backup("a", new DateTime(2024, 5, 1)) };
        var result = svc.GetBackupsToDelete(backups, NoGenerational(5), 5);
        Assert.Empty(result);
    }

    [Fact]
    public void MaxCount_DeletesOldestFirst()
    {
        var now = new DateTime(2024, 6, 1);
        var svc = MakeService(now);
        var backups = new[]
        {
            Backup("newest", new DateTime(2024, 5, 30)),
            Backup("middle", new DateTime(2024, 5, 15)),
            Backup("oldest", new DateTime(2024, 5, 1)),
        };
        var result = svc.GetBackupsToDelete(backups, NoGenerational(2), 2).ToList();
        Assert.Single(result);
        Assert.Equal("oldest", result[0].Slug);
    }

    [Fact]
    public void MaxCount_Zero_DoesNotDelete()
    {
        var svc = MakeService(new DateTime(2024, 6, 1));
        var backups = new[]
        {
            Backup("a", new DateTime(2024, 5, 1)),
            Backup("b", new DateTime(2024, 5, 2)),
            Backup("c", new DateTime(2024, 5, 3)),
        };
        var result = svc.GetBackupsToDelete(backups, NoGenerational(0), 0);
        Assert.Empty(result);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        var svc = MakeService(new DateTime(2024, 6, 1));
        Assert.Empty(svc.GetBackupsToDelete(Array.Empty<Backup>(), NoGenerational(3), 3));
    }

    // --- Retained flag ---

    [Fact]
    public void RetainedBackup_NeverDeleted()
    {
        var now = new DateTime(2024, 6, 1);
        var svc = MakeService(now);
        var pinned = Backup("pinned", new DateTime(2024, 1, 1));
        pinned.Retained = true;
        var backups = new[]
        {
            pinned,
            Backup("b", new DateTime(2024, 2, 1)),
            Backup("c", new DateTime(2024, 3, 1)),
        };
        // max=1 but retained doesn't count toward excess
        var result = svc.GetBackupsToDelete(backups, NoGenerational(1), 1).ToList();
        Assert.DoesNotContain(result, r => r.Slug == "pinned");
    }

    // --- Generational: daily ---

    [Fact]
    public void GenerationalDays_RetainsRecent()
    {
        var now = new DateTime(2024, 6, 10);
        var svc = MakeService(now);
        var settings = new BackupSettings { GenerationalDays = 5, MaxLocalBackups = 2 };
        var backups = new[]
        {
            Backup("day9", new DateTime(2024, 6, 9)),   // within 5 days → retain
            Backup("day7", new DateTime(2024, 6, 7)),   // within 5 days → retain
            Backup("day1", new DateTime(2024, 6, 1)),   // outside 5 days → eligible
        };
        var toDelete = svc.GetBackupsToDelete(backups, settings, 2).ToList();
        // 3 backups, max=2, 1 excess; day1 is outside window and oldest → deleted
        Assert.Single(toDelete);
        Assert.Equal("day1", toDelete[0].Slug);
    }

    // --- Generational: weekly ---

    [Fact]
    public void GenerationalWeeks_KeepsNewestPerWeek()
    {
        // now = Wednesday Jun 12 (week starts Sun Jun 9)
        var now = new DateTime(2024, 6, 12);
        var svc = MakeService(now);
        var settings = new BackupSettings { GenerationalWeeks = 2, MaxLocalBackups = 1 };

        // Backups spread across three distinct weeks:
        //   Jun 11 (Tue): in week of Jun  9 → newest this week → retained
        //   Jun 10 (Mon): in week of Jun  9 → older this week  → eligible
        //   Jun  3 (Mon): in week of Jun  2 → only last week   → retained
        var backups = new[]
        {
            Backup("this_week_new", new DateTime(2024, 6, 11)),
            Backup("this_week_old", new DateTime(2024, 6, 10)),
            Backup("last_week",     new DateTime(2024, 6,  3)),
        };
        var toDelete = svc.GetBackupsToDelete(backups, settings, 1).ToList();
        // retain-set = {this_week_new, last_week}; eligible = {this_week_old}
        Assert.All(toDelete, b => Assert.NotEqual("this_week_new", b.Slug));
        Assert.All(toDelete, b => Assert.NotEqual("last_week", b.Slug));
        Assert.Contains(toDelete, b => b.Slug == "this_week_old");
    }

    // --- Generational: monthly ---

    [Fact]
    public void GenerationalMonths_KeepsNewestPerMonth()
    {
        var now = new DateTime(2024, 6, 10);
        var svc = MakeService(now);
        var settings = new BackupSettings { GenerationalMonths = 2, MaxLocalBackups = 1 };

        var backups = new[]
        {
            Backup("jun_new", new DateTime(2024, 6, 9)),  // this month, newest → retained
            Backup("jun_old", new DateTime(2024, 6, 1)),  // this month, older → eligible
            Backup("may",     new DateTime(2024, 5, 31)), // last month → retained
        };
        var toDelete = svc.GetBackupsToDelete(backups, settings, 1).ToList();
        Assert.All(toDelete, b => Assert.NotEqual("jun_new", b.Slug));
        Assert.All(toDelete, b => Assert.NotEqual("may", b.Slug));
    }

    // --- Generational: yearly ---

    [Fact]
    public void GenerationalYears_KeepsNewestPerYear()
    {
        var now = new DateTime(2024, 6, 10);
        var svc = MakeService(now);
        var settings = new BackupSettings { GenerationalYears = 2, MaxLocalBackups = 1 };

        var backups = new[]
        {
            Backup("2024_new", new DateTime(2024, 6, 9)),  // this year, newest → retained
            Backup("2024_old", new DateTime(2024, 1, 1)),  // this year, older → eligible
            Backup("2023",     new DateTime(2023, 12, 31)),// last year → retained
        };
        var toDelete = svc.GetBackupsToDelete(backups, settings, 1).ToList();
        Assert.All(toDelete, b => Assert.NotEqual("2024_new", b.Slug));
        Assert.All(toDelete, b => Assert.NotEqual("2023", b.Slug));
    }
}

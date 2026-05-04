using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LIS.Models;

namespace LIS.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<DropdownValue> DropdownValues => Set<DropdownValue>();
    public DbSet<UserHospital> UserHospitals => Set<UserHospital>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StaffRegistrationRequest> StaffRegistrationRequests => Set<StaffRegistrationRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Hospital
        builder.Entity<Hospital>(e =>
        {
            e.HasIndex(h => h.Name).IsUnique();
        });

        builder.Entity<Permission>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
        });

        builder.Entity<RolePermission>(e =>
        {
            e.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

            e.HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SystemSetting>(e =>
        {
            e.HasIndex(s => new { s.Category, s.Key }).IsUnique();
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.PerformedAt);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.PerformedByUserId);
        });

        // Doctor
        builder.Entity<Doctor>(e =>
        {
            e.Property(d => d.Email).HasMaxLength(256);

            e.HasOne(d => d.Hospital)
                .WithMany(h => h.Doctors)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.User)
                .WithOne(u => u.Doctor)
                .HasForeignKey<Doctor>(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // One non-empty email per doctor row; aligns with Identity user email uniqueness for linked accounts.
            e.HasIndex(d => d.Email)
                .IsUnique()
                .HasFilter("[Email] IS NOT NULL AND [Email] <> ''")
                .HasDatabaseName("IX_Doctors_Email_UQ");
        });

        builder.Entity<UserHospital>(e =>
        {
            e.HasKey(uh => new { uh.UserId, uh.HospitalId });

            e.HasOne(uh => uh.User)
                .WithMany(u => u.UserHospitals)
                .HasForeignKey(uh => uh.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(uh => uh.Hospital)
                .WithMany(h => h.UserHospitals)
                .HasForeignKey(uh => uh.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(uh => uh.HospitalId);
        });

        // Patient
        builder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.NRIC);
            e.HasIndex(p => p.PassportNo);
            e.HasIndex(p => p.MRN);
        });

        // Report
        builder.Entity<Report>(e =>
        {
            e.HasIndex(r => r.ReferenceNumber); // non-unique: labs reuse reference numbers across reports
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.HospitalId);
            e.HasIndex(r => r.CreatedByUserId);

            e.HasOne(r => r.Hospital)
                .WithMany(h => h.Reports)
                .HasForeignKey(r => r.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Doctor)
                .WithMany(d => d.Reports)
                .HasForeignKey(r => r.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Patient)
                .WithMany(p => p.Reports)
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Test)
                .WithMany(t => t.Reports)
                .HasForeignKey(r => r.TestId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.CreatedByUser)
                .WithMany(u => u.CreatedReports)
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.UpdatedByUser)
                .WithMany(u => u.UpdatedReports)
                .HasForeignKey(r => r.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TestResult
        builder.Entity<TestResult>(e =>
        {
            e.HasOne(tr => tr.Report)
                .WithMany(r => r.TestResults)
                .HasForeignKey(tr => tr.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DropdownValue
        builder.Entity<DropdownValue>(e =>
        {
            e.HasIndex(d => new { d.Category, d.Value }).IsUnique();
        });

        // ApplicationUser
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Hospital)
                .WithMany(h => h.Users)
                .HasForeignKey(u => u.HospitalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StaffRegistrationRequest>(e =>
        {
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.Email);
            e.HasOne(r => r.Hospital)
                .WithMany()
                .HasForeignKey(r => r.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ProcessedByUser)
                .WithMany()
                .HasForeignKey(r => r.ProcessedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

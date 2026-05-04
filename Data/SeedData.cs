using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using LIS.Models;

namespace LIS.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        await DatabaseSchemaUpdater.EnsureCurrentSchemaAsync(context);

        // Create roles
        string[] roleNames = ["SuperAdmin", "LabAdmin", "Admin", "Doctor", "LabManager", "HeadNurse"];
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Seed Hospitals
        var hospitalNames = new[]
        {
            "LABLINK (M) SDN BHD", "MSU MEDICAL CENTRE", "KPJ TAWAKKAL SPECIALIST HOSPITAL",
            "KPJ AMPANG PUTERI SPECIALIST HOSPITAL", "KPJ SELANGOR SPECIALIST HOSPITAL",
            "KPJ DAMANSARA SPECIALIST HOSPITAL", "KPJ KAJANG SPECIALIST HOSPITAL",
            "KPJ KLANG SPECIALIST HOSPITAL", "SUNWAY MEDICAL CENTRE VELOCITY",
            "SUNWAY MEDICAL CENTRE SDN BHD", "HOSPITAL PAKAR AN NUR", "AVISENA SPECIALIST HOSPITAL",
            "AVISENA WOMEN'S & CHILDREN'S SPECIALIST HOSPITAL", "HOSPITAL ISLAM AZ-ZAHRAH",
            "COLUMBIA ASIA HOSPITAL PUCHONG", "COLUMBIA ASIA HOSPITAL BUKIT RIMAU",
            "COLUMBIA ASIA HOSPITAL PETALING JAYA", "COLUMBIA ASIA HOSPITAL KLANG",
            "COLUMBIA ASIA HOSPITAL SETAPAK", "COLUMBIA ASIA HOSPITAL SEREMBAN",
            "COLUMBIA ASIA HOSPITAL BUKIT JALIL", "COLUMBIA ASIA HOSPITAL CHERAS",
            "BEACON HOSPITAL SDN BHD", "PUSAT PERUBATAN UNIVERSITI MALAYA",
            "HOSPITAL UITM PUNCAK ALAM", "PREMIER INTEGRATED LABS BANGSAR",
            "PREMIER INTEGRATED LABS PANTAI AMPANG", "PREMIER INTEGRATED LABS PANTAI KLANG",
            "PREMIER INTEGRATED LABS PANTAI CHERAS", "PREMIER INTEGRATED LABS GLENEAGLES KL",
            "PREMIER INTEGRATED LABS PRINCE COURT MEDICAL CENTRE", "INSTITUT JANTUNG NEGARA",
            "GLENEAGLES PENANG", "LOH GUAN LYE SPECIALIST CENTRE", "ISLAND HOSPITAL",
            "THOMSON HOSPITAL SDN BHD", "INNOQUEST PATHOLOGY SDN BHD", "PATHLAB PJ",
            "GUARDS HEALTH", "THE GLASGOW HOLISTIC CLINIC SDN BHD", "KLINIK DR TERESA CHOW",
            "PARKCITY MEDICAL CENTRE", "SUBANG JAYA MEDICAL CENTRE", "ARA DAMANSARA MEDICAL CENTRE",
            "TAMAN DESA MEDICAL CENTRE", "REGENCY SPECIALIST HOSPITAL SDN BHD",
            "TUNG SHIN HOSPITAL", "BORNEO MEDICAL CENTRE", "ASSUNTA HOSPITAL", "ACCURA HEALTHCARE",
            "HOSPITAL PUSRAWI SDN BHD", "KELANA JAYA MEDICAL CENTRE",
            "MELAKA STRAITS MEDICAL CENTRE SDN BHD", "HARTAMAS WOMEN'S SPECIALIST CLINIC",
            "K.C. CHOW UROLOGICAL SURGERY SDN BHD (BANGSAR)", "ASEANA O&G SPECIALIST CLINIC",
            "PETER SKIN SPECIALIST CENTRE SDN BHD", "OASISEYE SPECIALIST", "UKM SPECIALIST CENTRE",
            "NEOGENIX LABORATOIRE SDN BHD", "AL-ISLAM SPECIALIST HOSPITAL", "KLINIK DR. AMBIKAI BALAN",
            "BUKIT TINGGI MEDICAL CENTRE", "HOSPITAL CANSELOR TUANKU MUHRIZ UKM",
            "HOSPITAL BUKIT MERTAJAM", "LAM & KOH UROLOGY ASSOCIATES SDN BHD (BANGSAR)",
            "LAM & KOH UROLOGY ASSOCIATES SDN BHD (GLENEAGLES KL)",
            "BEACON PRECISION DIAGNOSTICS SDN BHD", "HOSPITAL SULTANAH AMINAH JOHOR"
        };

        foreach (var name in hospitalNames)
        {
            if (!context.Hospitals.Any(h => h.Name == name))
            {
                context.Hospitals.Add(new Hospital { Name = name, Address = "Kuala Lumpur / Selangor, Malaysia" });
            }
        }
        await context.SaveChangesAsync();

        // Seed Tests
        var testList = new List<Test>
        {
            new() { Name = "Respiratory Pathogen Panel 36 (RPP36)", TestMethod = "Real Time PCR" },
            new() { Name = "Respiratory Panel Assay (RP26)", TestMethod = "Real Time PCR" },
            new() { Name = "Respiratory Viruses 19 (RV19)", TestMethod = "Real Time PCR" },
            new() { Name = "Respiratory Bacterial 7 (RB7)", TestMethod = "Real Time PCR" },
            new() { Name = "Mycobacterium tuberculosis and Non-tuberculosis Mycobacteria (MTB-NTM)", TestMethod = "Real Time PCR" },
            new() { Name = "Mycobacterium tuberculosis (MTB)", TestMethod = "Real Time PCR" },
            new() { Name = "H7N9", TestMethod = "Real Time PCR" },
            new() { Name = "MERS – CoV", TestMethod = "Real Time PCR" },
            new() { Name = "Covid-19", TestMethod = "Real Time PCR" },
            new() { Name = "BK & JC (BKJC)", TestMethod = "Real Time PCR" },
            new() { Name = "Cytomegalovirus (CMV)", TestMethod = "Real Time PCR" },
            new() { Name = "Epstein Barr Virus (EBV)", TestMethod = "Real Time PCR" },
            new() { Name = "Herpes Simplex Virus (HSV)", TestMethod = "Real Time PCR" },
            new() { Name = "Varicella Zoster Virus (VZV)", TestMethod = "Real Time PCR" },
            new() { Name = "Hepatitis B (HBV)", TestMethod = "Real Time PCR" },
            new() { Name = "Hepatitis C (HCV)", TestMethod = "Real Time PCR" },
            new() { Name = "Hepatitis B and Hepatitis C Combo Test (HBHC)", TestMethod = "Real Time PCR" },
            new() { Name = "Immunosuppressed Panel 1 (IMSP1)", TestMethod = "Real Time PCR" },
            new() { Name = "Immunosuppressed Panel 2 (IMSP2)", TestMethod = "Real Time PCR" },
            new() { Name = "Immunosuppressed Panel 3 (IMSP3)", TestMethod = "Real Time PCR" },
            new() { Name = "Eye Panel 1 (EP1)", TestMethod = "Real Time PCR" },
            new() { Name = "Eye Panel 2 (EP2)", TestMethod = "Real Time PCR" },
            new() { Name = "Cerebrospinal Fluid Panel 1 (CSF1)", TestMethod = "Real Time PCR" },
            new() { Name = "Cerebrospinal Fluid Panel 2 (CSF2)", TestMethod = "Real Time PCR" },
            new() { Name = "HIV detection and viral load (HIV)", TestMethod = "Real Time PCR" },
            new() { Name = "Combo HIV, CD4 and CD8", TestMethod = "Real Time PCR" },
            new() { Name = "STI7", TestMethod = "Real Time PCR" },
            new() { Name = "STI8", TestMethod = "Real Time PCR" },
            new() { Name = "STI9", TestMethod = "Real Time PCR" },
            new() { Name = "STI10", TestMethod = "Real Time PCR" },
            new() { Name = "Human Papillomavirus-28 Genotypes (HPV)", TestMethod = "Real Time PCR" },
            new() { Name = "Dengue Virus Detection & Typing (DENGUE)", TestMethod = "Real Time PCR" },
            new() { Name = "Dengue, Chikungunya Zika (DEN,CHIK,ZIKA)", TestMethod = "Real Time PCR" },
            new() { Name = "Melioidosis", TestMethod = "Real Time PCR" },
            new() { Name = "Leptospirosis", TestMethod = "Real Time PCR" },
            new() { Name = "Tropical Fever Panel (TFP)", TestMethod = "Real Time PCR" },
            new() { Name = "Helicobacter pylori & Antibiotic Resistance Genes Detection PCR (HBP1)", TestMethod = "Real Time PCR" },
            new() { Name = "Helicobacter pylori & Antibiotic Resistance Genes Detection PCR & Gene Sequencing (HBP2)", TestMethod = "Real Time PCR" },
            new() { Name = "Gastrointestinal Panel GI PANEL 1: 22 Targets", TestMethod = "Real Time PCR" },
            new() { Name = "Gastrointestinal Panel GI PANEL 2: 24 Targets", TestMethod = "Real Time PCR" },
            new() { Name = "Brucella", TestMethod = "Real Time PCR" },
            new() { Name = "Meningitis/ Encephalitis Panel (22 PATHOGEN)", TestMethod = "Real Time PCR" },
            new() { Name = "Meningitis/ Encephalitis Panel (14 PATHOGEN)", TestMethod = "Real Time PCR" },
            new() { Name = "Central Nervous System (CNS)", TestMethod = "Real Time PCR" },
            new() { Name = "Food Detective (FD)", TestMethod = "ELISA" },
            new() { Name = "Malaysian Allergy Panel (MA)", TestMethod = "ELISA" }
        };

        foreach (var test in testList)
        {
            if (!context.Tests.Any(t => t.Name == test.Name))
            {
                context.Tests.Add(test);
            }
        }
        await context.SaveChangesAsync();

        // Seed Doctors & Doctor Users from clinical data
        await SeedDoctorsAndUsersAsync(context, userManager);

        // Seed Super Admin User
        var superAdminEmail = "superadmin@geneflux.com";
        if (await userManager.FindByEmailAsync(superAdminEmail) == null)
        {
            var superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                FullName = "Geneflux Super Admin",
                Role = UserRole.SuperAdmin,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(superAdmin, "SuperAdmin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
            }
        }

        // Seed Lab Admin User
        var adminEmail = "admin@geneflux.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Geneflux Lab Admin",
                Role = UserRole.LabAdmin,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "LabAdmin");
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        // Seed Lab Manager User
        var labManagerEmail = "labmanager@premier.com";
        if (await userManager.FindByEmailAsync(labManagerEmail) == null)
        {
            var hospital = context.Hospitals.First(h => h.Name == "PREMIER INTEGRATED LABS PANTAI CHERAS");
            var labManager = new ApplicationUser
            {
                UserName = labManagerEmail,
                Email = labManagerEmail,
                FullName = "Parimala Nagappan",
                Role = UserRole.LabManager,
                HospitalId = hospital.Id,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(labManager, "LabManager@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(labManager, "LabManager");
            }
        }

        // Seed Head Nurse User
        var nurseEmail = "nurse@premier.com";
        if (await userManager.FindByEmailAsync(nurseEmail) == null)
        {
            var hospital = context.Hospitals.First(h => h.Name == "PREMIER INTEGRATED LABS PANTAI CHERAS");
            var nurse = new ApplicationUser
            {
                UserName = nurseEmail,
                Email = nurseEmail,
                FullName = "Nurse Sarah Lim",
                Role = UserRole.HeadNurse,
                HospitalId = hospital.Id,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(nurse, "HeadNurse@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(nurse, "HeadNurse");
            }
        }

        // Sync UserHospitals for any user with a HospitalId
        var usersWithHospitals = context.Users
            .Where(u => u.HospitalId.HasValue)
            .Select(u => new { u.Id, HospitalId = u.HospitalId!.Value })
            .ToList();

        foreach (var user in usersWithHospitals)
        {
            if (!context.UserHospitals.Any(uh => uh.UserId == user.Id && uh.HospitalId == user.HospitalId))
            {
                context.UserHospitals.Add(new UserHospital
                {
                    UserId = user.Id,
                    HospitalId = user.HospitalId
                });
            }
        }

        await context.SaveChangesAsync();

        // Seed Dropdown Values
        if (!context.DropdownValues.Any())
        {
            var dropdowns = new List<DropdownValue>
            {
                new() { Category = "DateRange", Value = "Past 1 week", SortOrder = 1 },
                new() { Category = "DateRange", Value = "Past 1 month", SortOrder = 2 },
                new() { Category = "DateRange", Value = "Past 6 months", SortOrder = 3 },
                new() { Category = "DateRange", Value = "Past 1 year", SortOrder = 4 },
            };
            context.DropdownValues.AddRange(dropdowns);
            await context.SaveChangesAsync();
        }

        if (!context.Permissions.Any())
        {
            context.Permissions.AddRange(
                new Permission { Code = "report.create", Description = "Create draft reports" },
                new Permission { Code = "report.submit", Description = "Submit reports for review" },
                new Permission { Code = "report.approve", Description = "Approve reports" },
                new Permission { Code = "report.archive", Description = "Archive approved reports" },
                new Permission { Code = "settings.manage", Description = "Manage system settings" },
                new Permission { Code = "audit.view", Description = "View audit logs" });
            await context.SaveChangesAsync();
        }

        if (!context.RolePermissions.Any())
        {
            var superAdminRole = await roleManager.FindByNameAsync("SuperAdmin");
            var labAdminRole = await roleManager.FindByNameAsync("LabAdmin");
            var doctorRole = await roleManager.FindByNameAsync("Doctor");
            var headNurseRole = await roleManager.FindByNameAsync("HeadNurse");
            var labManagerRole = await roleManager.FindByNameAsync("LabManager");
            var permissions = context.Permissions.ToDictionary(p => p.Code, p => p.Id);

            void AddRolePermissions(IdentityRole? role, params string[] codes)
            {
                if (role == null) return;
                foreach (var code in codes.Where(permissions.ContainsKey))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissions[code]
                    });
                }
            }

            AddRolePermissions(superAdminRole, "report.create", "report.submit", "report.approve", "report.archive", "settings.manage", "audit.view");
            AddRolePermissions(labAdminRole, "report.create", "report.submit", "report.approve", "report.archive");
            AddRolePermissions(doctorRole);
            AddRolePermissions(headNurseRole);
            AddRolePermissions(labManagerRole);
            await context.SaveChangesAsync();
        }

        if (!context.SystemSettings.Any())
        {
            context.SystemSettings.AddRange(
                new SystemSetting { Category = "ReportConfiguration", Key = "DefaultTemplate", Value = "Standard Lab Template", ValueType = "string", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "ReportConfiguration", Key = "RequirePatientMrn", Value = "true", ValueType = "bool", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "ReportConfiguration", Key = "RequireSpecimenType", Value = "true", ValueType = "bool", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "ReportConfiguration", Key = "AllowedFileExtensions", Value = ".pdf,.jpg,.png", ValueType = "string", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "ReportConfiguration", Key = "MaxUploadSizeMb", Value = "10", ValueType = "int", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "Retention", Key = "AutoArchiveEnabled", Value = "true", ValueType = "bool", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "Retention", Key = "ArchiveAfterDays", Value = "90", ValueType = "int", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "Retention", Key = "PurgeEnabled", Value = "false", ValueType = "bool", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" },
                new SystemSetting { Category = "Retention", Key = "PurgeAfterDays", Value = "3650", ValueType = "int", UpdatedAt = DateTime.UtcNow, UpdatedByUserId = "seed" });
            await context.SaveChangesAsync();
        }

        // Seed sample reports (kept for demo purposes)
        if (!context.Reports.Any())
        {
            var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
            var doctor = context.Doctors.FirstOrDefault(d => d.Name == "Dr. Goh Kee San");
            var hospital = doctor != null ? context.Hospitals.First(h => h.Id == doctor.HospitalId) : context.Hospitals.First();
            var test = context.Tests.First(t => t.Name == "Respiratory Pathogen Panel 36 (RPP36)");

            var patient = new Patient
            {
                Name = "Kua Boon Seng @ Quah Hooi Seng",
                IdentityType = IdentityType.NRIC,
                NRIC = "390731-02-5057",
                MRN = "18L193519",
                Sex = Sex.Male,
                DateOfBirth = new DateTime(1939, 7, 31)
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            var report = new Report
            {
                ReferenceNumber = "GF/RPP_279",
                HospitalId = hospital.Id,
                DoctorId = doctor?.Id ?? context.Doctors.First().Id,
                PatientId = patient.Id,
                TestId = test.Id,
                SpecimenType = "Nasal Swab",
                SampleCollectionDate = new DateTime(2025, 3, 25, 16, 0, 0),
                ReceivedAtLabDate = new DateTime(2025, 3, 25, 19, 0, 0),
                ReportingDate = new DateTime(2025, 3, 26, 16, 0, 0),
                SubmittedAt = new DateTime(2025, 3, 26, 9, 0, 0),
                ApprovedAt = new DateTime(2025, 3, 26, 16, 0, 0),
                Status = ReportStatus.Approved,
                CreatedByUserId = superAdmin!.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            context.Reports.Add(report);
            await context.SaveChangesAsync();

            var testResults = new List<TestResult>
            {
                new() { ReportId = report.Id, TestName = "Influenza A", Result = "DETECTED", SortOrder = 1 },
                new() { ReportId = report.Id, TestName = "Influenza B", Result = "NOT DETECTED", SortOrder = 2 },
                new() { ReportId = report.Id, TestName = "SARS-CoV-2", Result = "NOT DETECTED", SortOrder = 3 }
            };
            context.TestResults.AddRange(testResults);
            await context.SaveChangesAsync();

            var patient2 = new Patient
            {
                Name = "Tan Wei Ming",
                IdentityType = IdentityType.NRIC,
                NRIC = "880515-14-5678",
                MRN = "20L456789",
                Sex = Sex.Male,
                DateOfBirth = new DateTime(1988, 5, 15)
            };
            context.Patients.Add(patient2);
            await context.SaveChangesAsync();

            var draftReport = new Report
            {
                ReferenceNumber = "GF/RPP_280",
                HospitalId = hospital.Id,
                DoctorId = doctor?.Id ?? context.Doctors.First().Id,
                PatientId = patient2.Id,
                TestId = test.Id,
                SpecimenType = "Nasal Swab",
                SampleCollectionDate = DateTime.UtcNow.AddDays(-2),
                ReceivedAtLabDate = DateTime.UtcNow.AddDays(-2),
                Status = ReportStatus.Draft,
                CreatedByUserId = superAdmin.Id
            };
            context.Reports.Add(draftReport);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds all doctors from the 2026 clinical roster, creating both Doctor records
    /// and login-enabled ApplicationUser accounts (role = Doctor, password = Doctor@123).
    /// Each doctor is linked to their hospital via Doctor.HospitalId and UserHospital.
    /// </summary>
    private static async Task SeedDoctorsAndUsersAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        if (context.Doctors.Any(d => d.Name == "Dr. Tuang Wei Xuan"))
            return;

        // Hospital aliases: abbreviated/variant names -> canonical name in Hospitals table
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SUNWAY MC"] = "SUNWAY MEDICAL CENTRE SDN BHD",
            ["SMC"] = "SUNWAY MEDICAL CENTRE SDN BHD",
            ["SUNWAY MEDICAL CENTRE"] = "SUNWAY MEDICAL CENTRE SDN BHD",
            ["SUNWAY VELOCITY"] = "SUNWAY MEDICAL CENTRE VELOCITY",
            ["SMCV"] = "SUNWAY MEDICAL CENTRE VELOCITY",
            ["SJMC"] = "SUBANG JAYA MEDICAL CENTRE",
            ["THOMSON"] = "THOMSON HOSPITAL SDN BHD",
            ["UMMC"] = "PUSAT PERUBATAN UNIVERSITI MALAYA",
            ["REGENCY"] = "REGENCY SPECIALIST HOSPITAL SDN BHD",
            ["HOSPITAL BKT MERTAJAM"] = "HOSPITAL BUKIT MERTAJAM",
            ["KPJ TAWAKKAL"] = "KPJ TAWAKKAL SPECIALIST HOSPITAL",
            ["IJN"] = "INSTITUT JANTUNG NEGARA",
            ["AL-ISLAM"] = "AL-ISLAM SPECIALIST HOSPITAL",
            ["HOSPITAL PUSRAWI KUALA LUMPUR"] = "HOSPITAL PUSRAWI SDN BHD",
            ["PREMIER INTEGRATED LABS AMPANG"] = "PREMIER INTEGRATED LABS PANTAI AMPANG",
        };

        // Complete doctor roster: (Hospital, Doctor Name)
        // Data source: NAMA DOC 2026.xlsx — every entry included, duplicates removed.
        var entries = new (string Hospital, string Doctor)[]
        {
            // ASEANA O&G SPECIALIST CLINIC
            ("ASEANA O&G SPECIALIST CLINIC", "Dr. Vijayan"),

            // AVISENA SPECIALIST HOSPITAL
            ("AVISENA SPECIALIST HOSPITAL", "Dr. Melven Kok"),
            ("AVISENA SPECIALIST HOSPITAL", "Dr. Irene Wong"),

            // ASSUNTA HOSPITAL
            ("ASSUNTA HOSPITAL", "Dr. Mangayakarasi"),
            ("ASSUNTA HOSPITAL", "Dr. Thye Yuen Lin"),

            // AVISENA WOMEN'S & CHILDREN'S SPECIALIST HOSPITAL
            ("AVISENA WOMEN'S & CHILDREN'S SPECIALIST HOSPITAL", "Dr. Fazila"),

            // BEACON PRECISION DIAGNOSTICS SDN BHD
            ("BEACON PRECISION DIAGNOSTICS SDN BHD", "Dr. Goh Kee San"),
            ("BEACON PRECISION DIAGNOSTICS SDN BHD", "Dr. Tengku Ahmad Hidayat"),
            ("BEACON PRECISION DIAGNOSTICS SDN BHD", "Dr. Frances Leow May Yan"),

            // BORNEO MEDICAL CENTRE
            ("BORNEO MEDICAL CENTRE", "Dr. Yap Suet Li"),
            ("BORNEO MEDICAL CENTRE", "Dr. Lau Lee Gong"),
            ("BORNEO MEDICAL CENTRE", "Dr. Chua Hock Hin"),

            // COLUMBIA ASIA HOSPITAL KLANG
            ("COLUMBIA ASIA HOSPITAL KLANG", "Dr. Vijayan A/L Munusamy"),

            // COLUMBIA ASIA HOSPITAL SEREMBAN
            ("COLUMBIA ASIA HOSPITAL SEREMBAN", "Dr. Gan Ee Lang"),
            ("COLUMBIA ASIA HOSPITAL SEREMBAN", "Dr. Tan Han Loong"),

            // COLUMBIA ASIA HOSPITAL PETALING JAYA
            ("COLUMBIA ASIA HOSPITAL PETALING JAYA", "Dr. Chong Pei Weng"),

            // PREMIER INTEGRATED LABS GLENEAGLES KL
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Anuradha"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Jay Suriar"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Grace Chan Pui Suan"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Tay Voon Yaa"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Fah Kin Sing"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Aida Zarini"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Ibtisam Mokhtar"),
            ("PREMIER INTEGRATED LABS GLENEAGLES KL", "Dr. Muhammad Shafiq"),

            // GLENEAGLES PENANG
            ("GLENEAGLES PENANG", "Dr. Leong Kin Wah"),
            ("GLENEAGLES PENANG", "Dr. Lim Su Hong"),
            ("GLENEAGLES PENANG", "Dr. Chow Ting Soo"),
            ("GLENEAGLES PENANG", "Dr. Goh Heong Keong"),

            // HOSPITAL CANSELOR TUANKU MUHRIZ UKM
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Fauzan"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Jacelyn Heng Yi Ying"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Dharini"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Nur Iffah"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Leow Zi Hao"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Noor Elyana"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Elwin Raj"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Nurul Syazaliana"),
            ("HOSPITAL CANSELOR TUANKU MUHRIZ UKM", "Dr. Ong Yee Siang"),

            // HOSPITAL PUSRAWI SDN BHD
            ("HOSPITAL PUSRAWI SDN BHD", "Dr. Abdul Malik"),
            ("HOSPITAL PUSRAWI SDN BHD", "Dr. Nordin Zakaria"),

            // HOSPITAL SULTANAH AMINAH JOHOR
            ("HOSPITAL SULTANAH AMINAH JOHOR", "Dr. Ee Shu Ching"),

            // HOSPITAL UITM PUNCAK ALAM
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Noraini"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Naemah"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Muhammad Iqbal"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Muhammad Uwais"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Humairah Nur Aqilah"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Nur Sazlin"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Noor Elyana"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Shaza"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Musyirah Amaran"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Siti Hajar"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Anas Mat Aris"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Maszuhaikha"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Shairah Ridzuan"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Muhamad Zaim"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Farah Nur Izzrin"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Atiqah Zainal Abidin"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Syazwani"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Thara Nur Atiqah"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Nurul Nadia"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Noor Muslia"),
            ("HOSPITAL UITM PUNCAK ALAM", "Dr. Fatin Farhana"),

            // KPJ AMPANG PUTERI SPECIALIST HOSPITAL
            ("KPJ AMPANG PUTERI SPECIALIST HOSPITAL", "Dr. Ismaliza"),
            ("KPJ AMPANG PUTERI SPECIALIST HOSPITAL", "Dr. Nurashikin"),
            ("KPJ AMPANG PUTERI SPECIALIST HOSPITAL", "Dr. Muhamad Yazli"),

            // KPJ DAMANSARA SPECIALIST HOSPITAL
            ("KPJ DAMANSARA SPECIALIST HOSPITAL", "Dr. Shaharudeen"),

            // KPJ KAJANG SPECIALIST HOSPITAL
            ("KPJ KAJANG SPECIALIST HOSPITAL", "Dr. Fatin Aqilah"),
            ("KPJ KAJANG SPECIALIST HOSPITAL", "Dr. Wan Khairina"),
            ("KPJ KAJANG SPECIALIST HOSPITAL", "Dr. Meersha"),
            ("KPJ KAJANG SPECIALIST HOSPITAL", "Dr. Wai"),
            ("KPJ KAJANG SPECIALIST HOSPITAL", "Dr. Malani Shubana"),

            // KPJ TAWAKKAL SPECIALIST HOSPITAL
            ("KPJ TAWAKKAL SPECIALIST HOSPITAL", "Dr. Melvin Raj"),
            ("KPJ TAWAKKAL SPECIALIST HOSPITAL", "Dr. Charles Teh"),

            // LABLINK (M) SDN BHD
            ("LABLINK (M) SDN BHD", "Dr. Tuang Wei Xuan"),
            ("LABLINK (M) SDN BHD", "Dr. Ahlam Naila Kori"),
            ("LABLINK (M) SDN BHD", "Dr. Sangeetha"),
            ("LABLINK (M) SDN BHD", "Dr. Khor Yong Kean"),
            ("LABLINK (M) SDN BHD", "Dr. Muhammad Shafiq"),
            ("LABLINK (M) SDN BHD", "Dr. Muhammad Izzat"),
            ("LABLINK (M) SDN BHD", "Dr. Haema"),
            ("LABLINK (M) SDN BHD", "Dr. Lai Yee Seng"),
            ("LABLINK (M) SDN BHD", "Dr. Darshini"),
            ("LABLINK (M) SDN BHD", "Dr. Nurul Diyana"),

            // LOH GUAN LYE SPECIALIST CENTRE
            ("LOH GUAN LYE SPECIALIST CENTRE", "Dr. Lim Zhang Zhen"),
            ("LOH GUAN LYE SPECIALIST CENTRE", "Dr. Teoh Ching Soon"),

            // PREMIER INTEGRATED LABS PANTAI AMPANG
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Christopher"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Cheong Wai Seng"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Giri Shan Rajahram"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Guan Yong Khee"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Melissa Rina"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Lua Guan Way"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Mohan Dass"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Datuk Dr. Jayaram"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Aye Su Mon"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Tay Hui Sian"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Chiam Keng Hoong"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Siew Goh Chung"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Nagaraj Sri Ram"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Boo Yang Liang"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Jasminder Kaur"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Anusha"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Sivakumar"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Vijaya Sangkar"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Kerry"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Mahendra Raj"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Noor Haslinda"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Shanti Palaniappan"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Noor Hashida"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Soehardy"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Anand Bhulapan"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Chai Pei Fan"),
            ("PREMIER INTEGRATED LABS PANTAI AMPANG", "Dr. Juita"),

            // PREMIER INTEGRATED LABS BANGSAR
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Sivakumar"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Jasminder Kaur"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Balraj Singh"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Vijaya Sangkar"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Anusha"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Juita"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Ganesanathan"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Chai Pei Fun"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Soehardy"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Wong Mun Hoe"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Vikaish"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Tang Kok Leng"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Wong Zhi Qin"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Tee Sow Kuar"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Jeyanthy"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Khairul Nidzam"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Wong Zhiqin"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Ravidran Menon"),
            ("PREMIER INTEGRATED LABS BANGSAR", "Dr. Azam Mohd Nor"),

            // PARKCITY MEDICAL CENTRE
            ("PARKCITY MEDICAL CENTRE", "Dr. See Kwee Ching"),
            ("PARKCITY MEDICAL CENTRE", "Dr. Tay Kim Heng"),

            // REGENCY SPECIALIST HOSPITAL SDN BHD
            ("REGENCY SPECIALIST HOSPITAL SDN BHD", "Dr. Lim Han Nee"),
            ("REGENCY SPECIALIST HOSPITAL SDN BHD", "Dr. Lai Fui Boon"),
            ("REGENCY SPECIALIST HOSPITAL SDN BHD", "Dr. Wong Phing Sue"),

            // SUNWAY MEDICAL CENTRE SDN BHD
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Tan Soon Seng"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Hon Siong Leng"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Rosmadi Ismail"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Ho Kim Wah"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Syed Ajmal Syed Ali"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Wendy Lim Wan Dee"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Max Hu Chuen-Wei"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Ch'ng Tong Wei"),
            ("SUNWAY MEDICAL CENTRE SDN BHD", "Dr. Lim Sim Yee"),

            // SUNWAY MEDICAL CENTRE VELOCITY
            ("SUNWAY MEDICAL CENTRE VELOCITY", "Dr. Rasidah"),
            ("SUNWAY MEDICAL CENTRE VELOCITY", "Dr. Tan Ooi Keat"),
            ("SUNWAY MEDICAL CENTRE VELOCITY", "Dr. Nurul Yaqeen Mohd Esa"),
            ("SUNWAY MEDICAL CENTRE VELOCITY", "Dr. Henining Loo Cheng Kien"),
            ("SUNWAY MEDICAL CENTRE VELOCITY", "Dr. Wendy Lim"),
            ("SUNWAY MEDICAL CENTRE VELOCITY", "Dr. Nurul Izzati"),

            // HOSPITAL BUKIT MERTAJAM
            ("HOSPITAL BUKIT MERTAJAM", "Dr. Kosyilya"),

            // INSTITUT JANTUNG NEGARA
            ("INSTITUT JANTUNG NEGARA", "Dr. Azri"),

            // AL-ISLAM SPECIALIST HOSPITAL
            ("AL-ISLAM SPECIALIST HOSPITAL", "Dr. Ishak Mas'ud"),

            // SUBANG JAYA MEDICAL CENTRE
            ("SUBANG JAYA MEDICAL CENTRE", "Dr. Tan Soon Seng"),

            // THOMSON HOSPITAL SDN BHD
            ("THOMSON HOSPITAL SDN BHD", "Dr. Gan Ing Earn"),
            ("THOMSON HOSPITAL SDN BHD", "Dr. Rajesh"),
            ("THOMSON HOSPITAL SDN BHD", "Dr. Norliza"),
            ("THOMSON HOSPITAL SDN BHD", "Dr. Keng Tee Chau"),
            ("THOMSON HOSPITAL SDN BHD", "Dr. Sander Singh"),

            // PUSAT PERUBATAN UNIVERSITI MALAYA
            ("PUSAT PERUBATAN UNIVERSITI MALAYA", "Dr. Sim Mong Harn"),
            ("PUSAT PERUBATAN UNIVERSITI MALAYA", "Dr. Renujit Singh"),
            ("PUSAT PERUBATAN UNIVERSITI MALAYA", "Dr. Tho Weng Cheong"),
            ("PUSAT PERUBATAN UNIVERSITI MALAYA", "Dr. Siti Zulaika"),
        };

        var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Pre-load existing emails so doctor roster never reuses an address on any user or doctor row.
        foreach (var existingEmail in context.Users.Select(u => u.Email).ToList())
            if (existingEmail != null) usedEmails.Add(existingEmail);
        foreach (var existingEmail in context.Doctors.Where(d => d.Email != null).Select(d => d.Email!).ToList())
            usedEmails.Add(existingEmail);

        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hospitalRaw, doctorName) in entries)
        {
            var hospitalName = aliases.GetValueOrDefault(hospitalRaw, hospitalRaw);

            var pairKey = $"{hospitalName}|{doctorName}";
            if (!seenPairs.Add(pairKey))
                continue;

            var hospital = context.Hospitals.FirstOrDefault(h => h.Name == hospitalName);
            if (hospital == null)
            {
                hospital = new Hospital { Name = hospitalName, Address = "Malaysia" };
                context.Hospitals.Add(hospital);
                await context.SaveChangesAsync();
            }

            if (context.Doctors.Any(d => d.Name == doctorName && d.HospitalId == hospital.Id))
                continue;

            var email = GenerateUniqueEmail(doctorName, usedEmails);

            var doctor = new Doctor
            {
                Name = doctorName,
                Email = email,
                HospitalId = hospital.Id,
                Qualifications = "MBBS",
                Specialty = "General Practice"
            };
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            if (await userManager.FindByEmailAsync(email) != null)
                continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = doctorName,
                Role = UserRole.Doctor,
                HospitalId = hospital.Id,
                DoctorId = doctor.Id,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, "Doctor@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Doctor");
                doctor.UserId = user.Id;
                await context.SaveChangesAsync();
            }
        }
    }

    private static string GenerateUniqueEmail(string doctorName, HashSet<string> usedEmails)
    {
        var name = doctorName
            .Replace("Datuk ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Dr. ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        var slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", ".").Trim('.');
        slug = Regex.Replace(slug, @"\.{2,}", ".");

        var email = $"{slug}@geneflux.com";
        if (usedEmails.Add(email)) return email;

        for (int i = 2; ; i++)
        {
            email = $"{slug}.{i}@geneflux.com";
            if (usedEmails.Add(email)) return email;
        }
    }
}

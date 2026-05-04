using System.Globalization;
using System.Text.RegularExpressions;
using LIS.Data;
using LIS.Models;
using Microsoft.AspNetCore.Components.Forms;
using UglyToad.PdfPig;

namespace LIS.Services;

public class ReportUploadService
{
    private const long MaxUploadBytes = 20 * 1024 * 1024;
    private readonly IWebHostEnvironment _environment;
    private readonly DropdownService _dropdownService;

    public ReportUploadService(IWebHostEnvironment environment, DropdownService dropdownService)
    {
        _environment = environment;
        _dropdownService = dropdownService;
    }

    public async Task<ReportUploadResult> SaveAndExtractAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedFile(file.Name))
            throw new InvalidOperationException("Only PDF report uploads are supported for auto-detection right now.");

        var extension = Path.GetExtension(file.Name);
        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "reports");
        Directory.CreateDirectory(uploadsRoot);

        var storedFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, storedFileName);

        await using (var readStream = file.OpenReadStream(MaxUploadBytes, cancellationToken))
        await using (var writeStream = File.Create(fullPath))
        {
            await readStream.CopyToAsync(writeStream, cancellationToken);
        }

        var extractedText = ExtractTextFromPdf(fullPath);
        var parsed = await ParseAsync(extractedText, cancellationToken);

        return new ReportUploadResult
        {
            RelativePath = Path.Combine("uploads", "reports", storedFileName).Replace("\\", "/"),
            OriginalFileName = file.Name,
            ExtractedText = extractedText,
            ExtractedData = parsed
        };
    }

    /// <summary>
    /// Runs the same field extraction as PDF upload on raw text (e.g. from PdfPig). Used by automated smoke tests and troubleshooting.
    /// </summary>
    public Task<ExtractedReportData> ParseExtractedTextAsync(string extractedPdfText, CancellationToken cancellationToken = default) =>
        ParseAsync(extractedPdfText, cancellationToken);

    private static bool IsSupportedFile(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractTextFromPdf(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        return string.Join(Environment.NewLine + Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private async Task<ExtractedReportData> ParseAsync(string text, CancellationToken cancellationToken)
    {
        var hospitals = await _dropdownService.GetHospitalsAsync();
        var doctors = await _dropdownService.GetDoctorsAsync();
        var tests = await _dropdownService.GetTestsAsync();

        var normalizedText = NormalizeText(text);
        var data = new ExtractedReportData
        {
            ReferenceNumber = FirstNonEmpty(
                MatchValue(normalizedText, @"Geneflux\s+reference\s+number\s*:\s*(?<value>[A-Z0-9\/\-_]+)"),
                MatchValue(normalizedText, @"reference\s+number\s*:\s*(?<value>[A-Z0-9\/\-_]+)"),
                MatchValue(normalizedText, @"Ref\s*:\s*(?<value>[A-Z0-9\/\-_]+)")),
            PatientName = ExtractPatientName(normalizedText),
            Mrn = ExtractMrn(normalizedText),
            NricOrPassport = ExtractNricOrPassport(normalizedText),
            SexText = FirstNonEmpty(
                MatchValue(normalizedText, @"Age\s*\(\s*years\s*\)\s*/\s*Sex\s*:\s*(?:[\d,]+\s*YEARS\s*/\s*)?(?<value>MALE|FEMALE|Male|Female)\b"),
                MatchValue(normalizedText, @"Sex\s*:\s*(?<value>Male|Female|MALE|FEMALE)")),
            DoctorName = ExtractDoctorName(normalizedText),
            TestName = ExtractTestName(normalizedText),
            SpecimenType = ExtractSpecimenType(normalizedText),
            HospitalName = ExtractHospitalName(normalizedText, hospitals),
            ReportDate = ExtractReportingDate(normalizedText),
            CollectedDate = FirstDate(
                MatchValue(normalizedText, @"Date\s*&\s*Time\s+of\s+Sample\s+Collection\s*:\s*(?<value>[^\n\r]+)"),
                MatchValue(normalizedText, @"Collected\s*:\s*(?<value>\d{2}/\d{2}/\d{4})")),
            ReceivedDate = FirstDate(
                MatchValue(normalizedText, @"Date\s*&\s*Time\s+Received\s+at\s+the\s+Laboratory\s*:\s*(?<value>[^\n\r]+)"),
                MatchValue(normalizedText, @"Received\s*:\s*(?<value>\d{2}/\d{2}/\d{4})"))
        };

        data.HospitalId = hospitals.FirstOrDefault(h => string.Equals(h.Name, data.HospitalName, StringComparison.OrdinalIgnoreCase))?.Id;
        data.DoctorId = FindDoctorId(doctors, data.DoctorName);
        data.TestId = FindTestId(tests, data.TestName, data.ReferenceNumber);
        data.IdentityType = InferIdentityType(data.NricOrPassport);
        data.Sex = ParseSex(data.SexText);
        data.TestResults = ExtractResults(normalizedText);

        return data;
    }

    private static string NormalizeText(string text)
    {
        var t = text.Replace("\r", string.Empty);
        t = t.Replace('\u2018', '\'').Replace('\u2019', '\'');

        // PdfPig often joins lines without spaces — separate known labels so field regexes match.
        foreach (var label in new[]
                 {
                     "Geneflux reference number",
                     "Hospital name",
                     "Doctor's name",
                     "Patient's R/N number",
                     "Patient's Name",
                     "Identity Card number",
                     "Age (years)/Sex",
                     "Date & Time of Sample Collection",
                     "Date & Time Received at the Laboratory",
                     "Specimen Type",
                     "Test Method",
                     "Reporting Time & Date",
                     "EYE Panel Real Time PCR Test Results",
                     "EYE PANEL DIAGNOSTIC TEST"
                 })
        {
            t = Regex.Replace(t, $@"(?<=[^\s\n])(?={Regex.Escape(label)})", " ", RegexOptions.IgnoreCase);
        }

        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Collected:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Collected\s*:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Received:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Received\s*:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Specimen:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Specimen\s+Type:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[a-z])(?=Pathologist)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[a-z])(?=Page\s+\d)", " ", RegexOptions.IgnoreCase);

        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Patient Name:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Patient's Name:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=NRIC\s*/\s*Passport:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=NRIC:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Passport:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=MRN:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[0-9])(?=MRN:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=R/N\s+number:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Sex:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[0-9])(?=Sex\b)", " ", RegexOptions.IgnoreCase);

        t = Regex.Replace(t, @"(?<=[^\s\n])(?=Test Name:)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[^\s\n])(?=TEST\s+RESULTS)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[A-Za-z])(?=DETECTED\b)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=[A-Za-z0-9])(?=NOT\s+DETECTED\b)", " ", RegexOptions.IgnoreCase);

        return Regex.Replace(t, @"[ \t]{2,}", " ").Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Picks the first dd/MM/yyyy (or full-date parse) from strings produced by long datetime labels.</summary>
    private static DateTime? FirstDate(params string?[] rawLines)
    {
        foreach (var raw in rawLines)
        {
            var d = ParseDateFlexible(raw);
            if (d.HasValue)
                return d;
        }

        return null;
    }

    private static DateTime? ParseDateFlexible(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Use (?!\d) instead of \b after the year so "26/03/2025Company" still matches.
        var m = Regex.Match(value, @"(?<!\d)(\d{2}/\d{2}/\d{4})(?!\d)");
        if (m.Success &&
            DateTime.TryParseExact(m.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        return ParseDate(value.Trim());
    }

    private static DateTime? ExtractReportingDate(string text)
    {
        // PdfPig may put the date/time on the same line OR the next line after the label.
        // Scan up to 200 chars after the label regardless of newline structure.
        var labelMatch = Regex.Match(text, @"Reporting\s+Time\s*&\s*Date\s*:", RegexOptions.IgnoreCase);
        if (labelMatch.Success)
        {
            var rest = text[(labelMatch.Index + labelMatch.Length)..];
            var chunk = rest.Length > 200 ? rest[..200] : rest;
            var dt = ParseDateTimeFlexible(chunk);
            if (dt.HasValue) return dt;
            var d = ParseDateFlexible(chunk);
            if (d.HasValue) return d;
        }

        return FirstDate(MatchValue(text, @"Date\s*:\s*(?<value>\d{2}/\d{2}/\d{4})"));
    }

    private static DateTime? ParseDateTimeFlexible(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Use (?!\d) so the year matches even when immediately followed by a letter (e.g. "26/03/2025Company").
        var dateMatch = Regex.Match(value, @"(?<!\d)(?<date>\d{2}/\d{2}/\d{4})(?!\d)");
        var timeMatch = Regex.Match(value, @"\b(?<time>\d{1,2}:\d{2})\s*(?<ampm>am|pm)\b", RegexOptions.IgnoreCase);

        if (dateMatch.Success && timeMatch.Success)
        {
            var combined = $"{dateMatch.Groups["date"].Value} {timeMatch.Groups["time"].Value} {timeMatch.Groups["ampm"].Value.ToUpperInvariant()}";
            if (DateTime.TryParseExact(combined, "dd/MM/yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                return dateTime;
        }

        return ParseDateFlexible(value);
    }

    private static string? ExtractDoctorName(string text)
    {
        var match = Regex.Match(
            text,
            @"Doctor'?s\s+name\s*:\s*(?<value>.+?)(?=Patient'?s\s+R/N|Patient'?s\s+Name|Identity\s+Card|Age\s*\(|Date\s*&\s*Time|Specimen\s+Type|Test\s+Method|Hospital\s+name|Geneflux|EYE\s+PANEL|TEST\s+RESULTS|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success)
        {
            var v = CleanDoctorValue(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        match = Regex.Match(
            text,
            @"Doctor:\s*(?<value>.+?)(?=Test\s+Name:|Patient\s+Name:|Patient'?s\s+Name:|Date:|Ref:|Specimen:|TEST\s+RESULTS|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        return CleanDoctorValue(match.Groups["value"].Value);
    }

    private static string? CleanDoctorValue(string raw)
    {
        var value = raw.Trim();
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = Regex.Replace(value, @"Test\s+Name:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ExtractTestName(string text)
    {
        // "EYE PANEL" and "DIAGNOSTIC TEST" can be separated by company-name text when PdfPig
        // reads a two-column header table, so match "EYE PANEL" alone.
        var panel = Regex.Match(text, @"\bEYE\s+PANEL\b", RegexOptions.IgnoreCase);
        var method = Regex.Match(
            text,
            @"Test\s+Method\s*:\s*(?<value>[^\n\r]+?)(?=Specimen\s+Type|Date\s*&\s*Time|EYE\s+Panel\s+Real\s+Time|TEST\s+RESULTS|Patient'?s|Doctor'?s|Hospital|Geneflux|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var methodVal = method.Success ? Regex.Replace(method.Groups["value"].Value.Trim(), @"\s+", " ").Trim() : null;
        if (panel.Success && !string.IsNullOrWhiteSpace(methodVal))
            return $"EYE PANEL DIAGNOSTIC TEST ({methodVal})";

        if (panel.Success)
            return "EYE PANEL DIAGNOSTIC TEST";

        if (!string.IsNullOrWhiteSpace(methodVal))
            return methodVal;

        var match = Regex.Match(
            text,
            @"Test\s+Name:\s*(?<value>.+?)(?=TEST\s+RESULTS|Specimen:|Doctor:|Patient\s+Name:|Patient'?s\s+Name:|Date:|Ref:|Collected:|NRIC|MRN|Sex:|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        var value = match.Groups["value"].Value.Trim();
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = Regex.Replace(value, @"TEST\s+RESULTS.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Specimen:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ExtractPatientName(string text)
    {
        var match = Regex.Match(
            text,
            @"Patient'?s\s+Name\s*:\s*(?<value>.+?)(?=Identity\s+Card|Patient'?s\s+R/N|NRIC\s*/\s*Passport:|NRIC:|Passport:|MRN:|Age\s*\(|Sex:|Doctor:|Date:|Ref:|Hospital|Specimen:|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
        {
            match = Regex.Match(
                text,
                @"Patient\s+Name:\s*(?<value>.+?)(?=NRIC\s*/\s*Passport:|NRIC:|Passport:|MRN:|Sex:|Doctor:|Date:|Ref:|Hospital|Specimen:|\z)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        if (!match.Success)
            return null;

        var value = match.Groups["value"].Value.Trim();
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = Regex.Replace(value, @"NRIC\s*/\s*Passport:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"(?:NRIC|Passport):.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Identity\s+Card\s+number:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"MRN:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Sex:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ExtractNricOrPassport(string text)
    {
        var ic = Regex.Match(
            text,
            @"Identity\s+Card\s+number\s*:\s*(?<value>\d{6}-\d{2}-\d{4})",
            RegexOptions.IgnoreCase);

        if (ic.Success)
            return ic.Groups["value"].Value.Trim();

        var nricFmt = Regex.Match(
            text,
            @"(?:NRIC\s*/\s*Passport|NRIC|Passport):\s*(?<value>\d{6}-\d{2}-\d{4})",
            RegexOptions.IgnoreCase);

        if (nricFmt.Success)
            return nricFmt.Groups["value"].Value.Trim();

        ic = Regex.Match(
            text,
            @"Identity\s+Card\s+number\s*:\s*(?<value>[^\n\r]+?)(?=Age\s*\(|Patient'?s|MRN|Sex\b|Doctor:|Date:|Specimen:|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (ic.Success)
        {
            var v = ic.Groups["value"].Value.Trim();
            var digits = Regex.Match(v, @"\d{6}-\d{2}-\d{4}");
            if (digits.Success)
                return digits.Value;
        }

        var match = Regex.Match(
            text,
            @"(?:NRIC\s*/\s*Passport|NRIC|Passport):\s*(?<value>.+?)(?=MRN:|Sex\b|Sex:|Patient\s+Name:|Patient'?s\s+Name:|Doctor:|Date:|Specimen:|Ref:|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        var value = match.Groups["value"].Value.Trim();
        value = Regex.Replace(value, @"MRN.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Sex.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"\s+", " ").Trim();

        var token = Regex.Match(value, @"^[\w\-\/]+", RegexOptions.IgnoreCase).Value.Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string? ExtractMrn(string text)
    {
        var match = Regex.Match(
            text,
            @"Patient'?s\s+R/N\s+number\s*:\s*(?<value>.+?)(?=Patient'?s\s+Name|Identity\s+Card|Age\s*\(|NRIC|MRN:|Sex\b|Doctor:|Date:|Specimen:|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success)
        {
            var tok = TokenizeMrnValue(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(tok))
                return tok;
        }

        match = Regex.Match(
            text,
            @"MRN:\s*(?<value>.+?)(?=Sex\b|Sex:|NRIC|Passport|Patient\s+Name:|Patient'?s\s+Name:|Doctor:|Date:|Specimen:|Ref:|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        return TokenizeMrnValue(match.Groups["value"].Value);
    }

    private static string? TokenizeMrnValue(string raw)
    {
        var value = raw.Trim();
        value = Regex.Replace(value, @"Sex.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"\s+", string.Empty).Trim();

        var token = Regex.Match(value, @"^[A-Z0-9\-\/]+", RegexOptions.IgnoreCase).Value.Trim();
        if (token.EndsWith("Sex", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
            token = token[..^3].Trim();

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string? ExtractSpecimenType(string text)
    {
        var match = Regex.Match(
            text,
            @"Specimen\s+Type\s*:\s*(?<value>.+?)(?=Test\s+Method|Date\s*&\s*Time|EYE\s+Panel|TEST\s+RESULTS|Pathologist|Page\s+\d+\s+of\s+\d+|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success)
        {
            var v = CleanSpecimenValue(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        match = Regex.Match(
            text,
            @"Specimen:\s*(?<value>.+?)(?=Collected:|Received:|Pathologist|Page\s+\d+\s+of\s+\d+|TEST\s+RESULTS|EYE\s+Panel|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        return CleanSpecimenValue(match.Groups["value"].Value);
    }

    private static string? CleanSpecimenValue(string raw)
    {
        var value = raw.Trim();
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = Regex.Replace(value, @"Collected:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Received:.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Pathologist.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"Page\s+\d+.*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? MatchValue(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? ExtractHospitalName(string text, IReadOnlyList<Hospital> hospitals)
    {
        return hospitals
            .OrderByDescending(h => h.Name.Length)
            .FirstOrDefault(h => text.Contains(h.Name, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private static int? FindDoctorId(IEnumerable<Doctor> doctors, string? doctorName)
    {
        if (string.IsNullOrWhiteSpace(doctorName))
            return null;

        return doctors.FirstOrDefault(d => NormalizePersonName(d.Name) == NormalizePersonName(doctorName))?.Id;
    }

    private static int? FindTestId(IEnumerable<Test> tests, string? testName, string? referenceNumber = null)
    {
        var refPrefix = ExtractRefTypePrefix(referenceNumber); // e.g. "EP" from "GF/EP_279"

        if (string.IsNullOrWhiteSpace(testName) && string.IsNullOrWhiteSpace(refPrefix))
            return null;

        var list = tests.ToList();

        if (!string.IsNullOrWhiteSpace(testName))
        {
            var direct = list.FirstOrDefault(t =>
                t.Name.Contains(testName, StringComparison.OrdinalIgnoreCase) ||
                testName.Contains(t.Name, StringComparison.OrdinalIgnoreCase));
            if (direct != null)
                return direct.Id;
        }

        var normalizedTestName = NormalizeSearchText(testName ?? string.Empty);
        var method = ExtractMethodFromTestName(testName ?? string.Empty);

        var eyePanel = list
            .Where(t => NormalizeSearchText(t.Name).Contains("eye panel", StringComparison.OrdinalIgnoreCase))
            .Where(t => string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(t.TestMethod) || t.TestMethod.Contains(method, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name)
            .FirstOrDefault();

        // "eye panel" in the extracted test name OR the reference prefix "EP" both signal an Eye Panel test.
        bool isEyePanel = normalizedTestName.Contains("eye panel", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(refPrefix, "EP", StringComparison.OrdinalIgnoreCase);

        if (isEyePanel && eyePanel != null)
            return eyePanel.Id;

        if (string.IsNullOrWhiteSpace(testName))
            return null;

        return list
            .OrderByDescending(t => SharedTokenCount(normalizedTestName, NormalizeSearchText($"{t.Name} {t.TestMethod}")))
            .FirstOrDefault(t => SharedTokenCount(normalizedTestName, NormalizeSearchText($"{t.Name} {t.TestMethod}")) >= 2)
            ?.Id;
    }

    /// <summary>Extracts the type prefix from a Geneflux reference number, e.g. "EP" from "GF/EP_279".</summary>
    private static string? ExtractRefTypePrefix(string? referenceNumber)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            return null;

        var m = Regex.Match(referenceNumber, @"/([A-Za-z][A-Za-z0-9\-]*)_", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string NormalizePersonName(string value)
    {
        value = Regex.Replace(value, @"\bDR\b\.?", "DR", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"[^\w\s]", " ");
        return Regex.Replace(value, @"\s+", " ").Trim().ToUpperInvariant();
    }

    private static string NormalizeSearchText(string value)
    {
        value = Regex.Replace(value, @"[^\w\s]", " ");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string? ExtractMethodFromTestName(string value)
    {
        var match = Regex.Match(value, @"\((?<method>[^)]+)\)");
        return match.Success ? match.Groups["method"].Value.Trim() : null;
    }

    private static int SharedTokenCount(string left, string right)
    {
        var leftTokens = Regex.Matches(left.ToLowerInvariant(), @"[a-z0-9]{3,}")
            .Select(m => m.Value)
            .ToHashSet();
        var rightTokens = Regex.Matches(right.ToLowerInvariant(), @"[a-z0-9]{3,}")
            .Select(m => m.Value)
            .ToHashSet();

        return leftTokens.Count(rightTokens.Contains);
    }

    private static IdentityType InferIdentityType(string? identityValue)
    {
        return !string.IsNullOrWhiteSpace(identityValue) && identityValue.Count(char.IsDigit) >= 6 && identityValue.Contains('-')
            ? IdentityType.NRIC
            : IdentityType.Passport;
    }

    private static Sex ParseSex(string? sexText)
    {
        if (string.IsNullOrWhiteSpace(sexText))
            return Sex.Male;

        return sexText.Contains("Female", StringComparison.OrdinalIgnoreCase) ||
               sexText.Contains("FEMALE", StringComparison.OrdinalIgnoreCase)
            ? Sex.Female
            : Sex.Male;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        return null;
    }

    private static List<ExtractedTestResult> ExtractResults(string text)
    {
        var results = new List<ExtractedTestResult>();
        var start = FindTestResultsSectionStart(text);
        if (start.Index < 0)
            return results;

        var afterHeader = text[(start.Index + start.HeaderLength)..].TrimStart();
        afterHeader = Regex.Replace(afterHeader, @"^[=_\-\s:]+", string.Empty);

        var endIdx = FindTestResultsSectionEnd(afterHeader);
        var body = (endIdx >= 0 ? afterHeader[..endIdx] : afterHeader).Trim();

        if (string.IsNullOrWhiteSpace(body))
            return results;

        body = NormalizeResultsTableBody(body);

        var fromAnchors = ParseResultsByDnaRowAnchors(body);
        fromAnchors = fromAnchors.Where(r => IsPlausibleTestResultName(r.TestName)).ToList();

        var fromLines = new List<ExtractedTestResult>();
        ParseResultRowsFromBody(body, fromLines);
        fromLines = fromLines.Where(r => IsPlausibleTestResultName(r.TestName)).ToList();

        var fromGlued = new List<ExtractedTestResult>();
        ParseResultRowsGlued(body, fromGlued);
        fromGlued = fromGlued.Where(r => IsPlausibleTestResultName(r.TestName)).ToList();

        var ordered = new[] { fromAnchors, fromGlued, fromLines }
            .OrderByDescending(c => c.Count)
            .ToList();

        return ordered.FirstOrDefault(c => c.Count > 0) ?? new List<ExtractedTestResult>();
    }

    /// <summary>Locates the results table (Geneflux Eye Panel vs legacy TEST RESULTS).</summary>
    private static (int Index, int HeaderLength) FindTestResultsSectionStart(string text)
    {
        var phrases = new[]
        {
            "EYE Panel Real Time PCR Test Results",
            "Real Time PCR Test Results",
            "TEST RESULTS",
            "Test Results"
        };

        var bestIdx = -1;
        var bestLen = 0;

        foreach (var phrase in phrases)
        {
            var i = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                continue;

            if (bestIdx < 0 || i < bestIdx || (i == bestIdx && phrase.Length > bestLen))
            {
                bestIdx = i;
                bestLen = phrase.Length;
            }
        }

        return (bestIdx, bestLen);
    }

    private static string NormalizeResultsTableBody(string body)
    {
        body = Regex.Replace(body, @"^[=_\-\s]+", string.Empty);
        body = Regex.Replace(body, @"^Test\s*Result\s*Flag/?Details?\s*", string.Empty, RegexOptions.IgnoreCase);
        body = Regex.Replace(body, @"(?<=[a-z\/\-])(?=[A-Z][a-z])", " ");
        body = Regex.Replace(body, @"(?<=[A-Za-z])(?=DNA\s+(?:NOT\s+)?DETECTED)", " ", RegexOptions.IgnoreCase);
        body = Regex.Replace(body, @"(?<=[A-Za-z])(?=DETECTED\b)", " ", RegexOptions.IgnoreCase);
        body = Regex.Replace(body, @"(?<=[A-Za-z0-9,%/])(?=[A-Z]{2,}\s+DNA\b)", " ");
        return body.Trim();
    }

    private static List<ExtractedTestResult> ParseResultsByDnaRowAnchors(string body)
    {
        var results = new List<ExtractedTestResult>();
        var pattern = new Regex(
            @"\b(?<name>(?:[A-Z][A-Za-z]*\s+)+\s*DNA)\s+(?<result>NOT\s+DETECTED|DETECTED|POSITIVE|NEGATIVE|PENDING)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var matches = pattern.Matches(body);
        if (matches.Count == 0)
            return results;

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var name = Regex.Replace(m.Groups["name"].Value.Trim(), @"\s+", " ");
            if (!IsPlausibleTestResultName(name))
                continue;

            var result = Regex.Replace(m.Groups["result"].Value.Trim(), @"\s+", " ").Trim().ToUpperInvariant();
            var end = m.Index + m.Length;
            var nextStart = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            var detail = body[end..nextStart].Trim();
            detail = Regex.Replace(detail, @"^[\s:;,.\-]+", "").Trim();
            if (string.IsNullOrWhiteSpace(detail))
                detail = null;

            results.Add(new ExtractedTestResult
            {
                TestName = name,
                Result = result,
                ResultDetail = detail
            });
        }

        return results;
    }

    private static bool IsPlausibleTestResultName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            return false;
        if (name.Equals("Test", StringComparison.OrdinalIgnoreCase))
            return false;
        if (Regex.IsMatch(name, @"TestResult|Flag/?Details|/\s*Details|TEST\s+RESULTS", RegexOptions.IgnoreCase))
            return false;
        if (Regex.IsMatch(name, @"^Test\s+Result|\bTest\s+Result\b", RegexOptions.IgnoreCase))
            return false;
        return true;
    }

    private static int FindTestResultsSectionEnd(string text)
    {
        var best = -1;
        foreach (var marker in new[]
                 {
                     "Notes:",
                     "Authorized Personnel",
                     "Reporting Time",
                     "Lower Limit",
                     "Pathologist",
                     "Geneflux reference number",
                     "Hospital name",
                     "Patient's R/N number",
                     "Specimen Type:",
                     "Specimen:",
                     "Date & Time of Sample Collection",
                     "Collected:",
                     "Received:",
                     "Page "
                 })
        {
            var i = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best < 0 || i < best))
                best = i;
        }

        return best;
    }

    private static void ParseResultRowsFromBody(string body, List<ExtractedTestResult> results)
    {
        var lines = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => Regex.Replace(l, @"\s{2,}", " ").Trim())
            .ToList();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("Specimen:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Collected:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Received:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Page ", StringComparison.OrdinalIgnoreCase))
                break;

            if (IsTestResultsTableHeaderLine(line))
                continue;

            TryAddResultRow(line, results);
        }
    }

    private static bool IsTestResultsTableHeaderLine(string line)
    {
        if (line.StartsWith("Test ", StringComparison.OrdinalIgnoreCase) &&
            (line.Contains("Result", StringComparison.OrdinalIgnoreCase) || line.Contains("Flag", StringComparison.OrdinalIgnoreCase)))
            return true;

        return Regex.IsMatch(line, @"^Test\s+Result(\s+Flag)?", RegexOptions.IgnoreCase);
    }

    private static void TryAddResultRow(string line, List<ExtractedTestResult> results)
    {
        var match = Regex.Match(
            line,
            @"^(?<name>.+?)\s+(?<result>NOT\s+DETECTED|DETECTED|POSITIVE|NEGATIVE|PENDING)\b\s*(?<detail>.*)$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return;

        var name = match.Groups["name"].Value.Trim();
        if (!IsPlausibleTestResultName(name))
            return;

        var result = match.Groups["result"].Value.Trim();
        result = Regex.Replace(result, @"\s+", " ").Trim().ToUpperInvariant();
        var detail = match.Groups["detail"].Value.Trim();
        if (string.IsNullOrWhiteSpace(detail))
            detail = null;

        results.Add(new ExtractedTestResult
        {
            TestName = name,
            Result = result,
            ResultDetail = detail
        });
    }

    private static void ParseResultRowsGlued(string body, List<ExtractedTestResult> results)
    {
        const string pattern =
            @"(?<name>[A-Za-z][A-Za-z0-9\-\s\(\)\/]{1,55}?)\s+" +
            @"(?<result>NOT\s+DETECTED|DETECTED|POSITIVE|NEGATIVE|PENDING)\b\s*" +
            @"(?<detail>.*?)(?=\s+[A-Za-z][A-Za-z0-9\-\s\(\)\/]{1,55}?\s+(?:NOT\s+DETECTED|DETECTED|POSITIVE|NEGATIVE|PENDING)\b|\s*$)";

        foreach (Match m in Regex.Matches(body, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var name = m.Groups["name"].Value.Trim();
            if (!IsPlausibleTestResultName(name) || IsTestResultsTableHeaderLine(name))
                continue;

            var result = Regex.Replace(m.Groups["result"].Value.Trim(), @"\s+", " ").Trim().ToUpperInvariant();
            var detail = m.Groups["detail"].Value.Trim();
            if (string.IsNullOrWhiteSpace(detail))
                detail = null;

            results.Add(new ExtractedTestResult
            {
                TestName = name,
                Result = result,
                ResultDetail = detail
            });
        }
    }
}

public class ReportUploadResult
{
    public string RelativePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public ExtractedReportData ExtractedData { get; set; } = new();
}

public class ExtractedReportData
{
    public string? ReferenceNumber { get; set; }
    public string? HospitalName { get; set; }
    public int? HospitalId { get; set; }
    public string? PatientName { get; set; }
    public string? NricOrPassport { get; set; }
    public string? Mrn { get; set; }
    public IdentityType IdentityType { get; set; } = IdentityType.NRIC;
    public string? SexText { get; set; }
    public Sex Sex { get; set; } = Sex.Male;
    public string? DoctorName { get; set; }
    public int? DoctorId { get; set; }
    public string? TestName { get; set; }
    public int? TestId { get; set; }
    public string? SpecimenType { get; set; }
    public DateTime? ReportDate { get; set; }
    public DateTime? CollectedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public List<ExtractedTestResult> TestResults { get; set; } = new();
}

public class ExtractedTestResult
{
    public string TestName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? ResultDetail { get; set; }
}

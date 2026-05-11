namespace LIS.Models;

/// <summary>Public URLs for emails and redirects (not for server binding).</summary>
public class AppSiteSettings
{
    /// <summary>Base URL of the LIS web app, e.g. https://lis.geneflux.com (no trailing slash required).</summary>
    public string PublicBaseUrl { get; set; } = "";
}

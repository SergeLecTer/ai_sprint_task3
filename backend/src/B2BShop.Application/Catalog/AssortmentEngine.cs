using B2BShop.Domain.Entities;

namespace B2BShop.Application.Catalog;

/// <summary>
/// Resolves which articles are visible/orderable for a given company.
/// Resolution order: individual exclusion > individual inclusion > category assignment
/// Note: date-range filtering applied at query time
/// </summary>
public class AssortmentEngine
{
    /// <summary>
    /// Determines if an article is visible for a company given its assignments.
    /// Priority: CompanyProductAssignment.IsExcluded=true overrides everything.
    /// Then individual IsVisible assignment, then category assignment.
    /// </summary>
    public ArticleVisibility GetVisibility(
        Article article,
        IEnumerable<CompanyProductAssignment> productAssignments,
        IEnumerable<CompanyCategoryAssignment> categoryAssignments,
        DateTime asOf)
    {
        var now = asOf;

        // Check for individual product assignment first (highest priority)
        var individualAssignment = productAssignments
            .Where(a => a.ArticleId == article.Id &&
                        (a.EffectiveFrom == null || a.EffectiveFrom <= now) &&
                        (a.EffectiveTo == null || a.EffectiveTo >= now))
            .FirstOrDefault();

        if (individualAssignment != null)
        {
            if (individualAssignment.IsExcluded)
                return ArticleVisibility.Excluded;

            return new ArticleVisibility
            {
                IsVisible = individualAssignment.IsVisible,
                IsOrderable = individualAssignment.IsOrderable && individualAssignment.IsVisible,
                Source = "individual"
            };
        }

        // Fall back to category assignment
        var catAssignment = categoryAssignments
            .Where(a => a.CategoryId == article.CategoryId &&
                        (a.EffectiveFrom == null || a.EffectiveFrom <= now) &&
                        (a.EffectiveTo == null || a.EffectiveTo >= now))
            .FirstOrDefault();

        if (catAssignment != null)
        {
            if (catAssignment.IsExcluded)
                return ArticleVisibility.Excluded;

            return new ArticleVisibility
            {
                IsVisible = catAssignment.IsVisible,
                IsOrderable = catAssignment.IsVisible,
                Source = "category"
            };
        }

        // No assignment = not visible (default deny)
        // Hidden rule: this is different from v1 which was default-allow
        return ArticleVisibility.NotAssigned;
    }
}

public class ArticleVisibility
{
    public bool IsVisible { get; set; }
    public bool IsOrderable { get; set; }
    public string Source { get; set; } = "none";
    public bool IsExcluded => !IsVisible && Source == "excluded";

    public static ArticleVisibility Excluded => new() { IsVisible = false, IsOrderable = false, Source = "excluded" };
    public static ArticleVisibility NotAssigned => new() { IsVisible = false, IsOrderable = false, Source = "none" };
}

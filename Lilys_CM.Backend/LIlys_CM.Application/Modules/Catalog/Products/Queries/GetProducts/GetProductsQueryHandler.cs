using Lilys_CM.Application.Abstractions;
using Lilys_CM.Application.Common;
using Lilys_CM.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Lilys_CM.Application.Modules.Catalog.Products.Queries.GetProducts;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PageResult<ProductDto>>
{
    private readonly IAppDbContext _context;

    public GetProductsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<PageResult<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .Include(p => p.BrandEntity)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        if (request.SubcategoryId.HasValue)
        {
            query = query.Where(p => p.SubcategoryId == request.SubcategoryId.Value);
        }

        if (request.BrandId.HasValue)
        {
            query = query.Where(p => p.BrandId == request.BrandId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            var brand = request.Brand.Trim().ToLower();

            query = query.Where(p =>
                (p.BrandEntity != null && p.BrandEntity.Name.ToLower().Contains(brand)) ||
                (p.Brand != null && p.Brand.ToLower().Contains(brand)));
        }

        if (!string.IsNullOrWhiteSpace(request.Subcategory))
        {
            var subcategory = request.Subcategory.Trim().ToLower();

            query = query.Where(p =>
                p.Subcategory != null &&
                p.Subcategory.Name.ToLower().Contains(subcategory));
        }

        if (request.PriceMin.HasValue)
        {
            query = query.Where(p => p.Price >= request.PriceMin.Value);
        }

        if (request.PriceMax.HasValue)
        {
            query = query.Where(p => p.Price <= request.PriceMax.Value);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(p => p.IsEnabled == request.IsEnabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();

            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Sku.ToLower().Contains(search) ||
                p.Slug.ToLower().Contains(search) ||
                (p.ShortDescription != null && p.ShortDescription.ToLower().Contains(search)) ||
                (p.Description != null && p.Description.ToLower().Contains(search)) ||
                (p.Ingredients != null && p.Ingredients.ToLower().Contains(search)) ||
                (p.Benefits != null && p.Benefits.ToLower().Contains(search)) ||
                (p.SeoTitle != null && p.SeoTitle.ToLower().Contains(search)) ||
                (p.SeoDescription != null && p.SeoDescription.ToLower().Contains(search)) ||
                (p.BrandEntity != null && p.BrandEntity.Name.ToLower().Contains(search)) ||
                (p.Category != null && p.Category.Name.ToLower().Contains(search)) ||
                (p.Subcategory != null && p.Subcategory.Name.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var items = await query
            .Skip(request.Paging.SkipCount)
            .Take(request.Paging.PageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,

                Name = p.Name,
                Sku = p.Sku,
                Slug = p.Slug,

                ImageUrl = p.ImageUrl,
                ShortDescription = p.ShortDescription,
                Description = p.Description,
                Ingredients = p.Ingredients,
                HowToUse = p.HowToUse,
                Benefits = p.Benefits,

                Brand = p.Brand,
                BrandId = p.BrandId,
                BrandName = p.BrandEntity != null ? p.BrandEntity.Name : p.Brand,
                BrandLogoUrl = p.BrandEntity != null ? p.BrandEntity.LogoUrl : null,

                Size = p.Size,
                CountryOfOrigin = p.CountryOfOrigin,
                Barcode = p.Barcode,

                Price = p.Price,
                CompareAtPrice = p.CompareAtPrice,

                StockQuantity = p.StockQuantity,

                IsEnabled = p.IsEnabled,
                IsFeatured = p.IsFeatured,

                SeoTitle = p.SeoTitle,
                SeoDescription = p.SeoDescription,

                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,

                SubcategoryId = p.SubcategoryId,
                SubcategoryName = p.Subcategory != null ? p.Subcategory.Name : null
            })
            .ToListAsync(cancellationToken);

        return new PageResult<ProductDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Paging.Page,
            PageSize = request.Paging.PageSize
        };
    }

    private static string? BuildFullTextSearchCondition(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var words = Regex
            .Matches(search.Trim(), @"[\p{L}\p{N}]+")
            .Select(x => x.Value)
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (words.Count == 0)
        {
            return null;
        }

        return string.Join(" AND ", words.Select(x => $"\"{x}*\""));
    }

    private static IQueryable<ProductEntity> ApplySorting(
        IQueryable<ProductEntity> query,
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();

        var isDescending = string.Equals(
            sortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "price" => isDescending
                ? query.OrderByDescending(p => p.Price)
                : query.OrderBy(p => p.Price),

            "stock" or "stockquantity" => isDescending
                ? query.OrderByDescending(p => p.StockQuantity)
                : query.OrderBy(p => p.StockQuantity),

            "status" or "isenabled" => isDescending
                ? query.OrderByDescending(p => p.IsEnabled)
                : query.OrderBy(p => p.IsEnabled),

            "catalog" or "category" or "categoryname" => isDescending
                ? query.OrderByDescending(p => p.Category.Name)
                : query.OrderBy(p => p.Category.Name),

            "subcategory" or "subcategoryname" => isDescending
                ? query.OrderByDescending(p => p.Subcategory != null ? p.Subcategory.Name : string.Empty)
                : query.OrderBy(p => p.Subcategory != null ? p.Subcategory.Name : string.Empty),

            "brand" or "brandname" => isDescending
                ? query.OrderByDescending(p => p.BrandEntity != null ? p.BrandEntity.Name : p.Brand ?? string.Empty)
                : query.OrderBy(p => p.BrandEntity != null ? p.BrandEntity.Name : p.Brand ?? string.Empty),

            "sku" => isDescending
                ? query.OrderByDescending(p => p.Sku)
                : query.OrderBy(p => p.Sku),

            "name" or "product" => isDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),

            _ => query.OrderBy(p => p.Name)
        };
    }
}
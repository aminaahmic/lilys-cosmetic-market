using Lilys_CM.Domain.Catalog;

namespace Lilys_CM.Application.Modules.Catalog.ProductVariants.Commands.UpdateProductVariant;

public sealed class UpdateProductVariantCommandHandler
    : IRequestHandler<UpdateProductVariantCommand>
{
    private readonly IAppDbContext _context;

    public UpdateProductVariantCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task Handle(
        UpdateProductVariantCommand request,
        CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.VariantId &&
                    x.ProductId == request.ProductId &&
                    !x.IsDeleted,
                cancellationToken);

        if (variant is null)
        {
            throw new Lilys_CMNotFoundException("Product variant not found.");
        }

        var normalizedOptions = request.Options
            .Select(x => new
            {
                x.OptionId,
                Value = x.Value.Trim()
            })
            .ToList();

        var hasDuplicatesInsideSameVariant = normalizedOptions
            .GroupBy(x => new
            {
                x.OptionId,
          Value = x.Value.ToLowerInvariant()
            })
            .Any(g => g.Count() > 1);

        if (hasDuplicatesInsideSameVariant)
        {
            throw new Lilys_CMConflictException("Duplicate options are not allowed on the same variant.");
        }

        var optionIds = normalizedOptions
            .Select(x => x.OptionId)
            .Distinct()
            .ToList();

        var existingOptionIds = await _context.Options
            .Where(x => optionIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingOptionIds.Count != optionIds.Count)
        {
            throw new Lilys_CMNotFoundException("One or more options were not found.");
        }

        await EnsureVariantCombinationIsUniqueAsync(
            request.ProductId,
            ignoredVariantId: variant.Id,
            normalizedOptions.Select(x => new VariantOptionInput(x.OptionId, x.Value)).ToList(),
            cancellationToken);

        variant.Price = request.Price;
        variant.Stock = request.Stock;

        var existingVariantOptions = await _context.VariantOptionEntities
            .Where(x => x.VariantId == variant.Id)
            .ToListAsync(cancellationToken);

        _context.VariantOptionEntities.RemoveRange(existingVariantOptions);

        foreach (var optionItem in normalizedOptions)
        {
            var optionValue = await _context.OptionValueEntities
                .FirstOrDefaultAsync(
                    x =>
                        x.OptionId == optionItem.OptionId &&
                        x.Value.ToLowerInvariant() == optionItem.Value.ToLowerInvariant() &&
                        !x.IsDeleted,
                    cancellationToken);

            if (optionValue is null)
            {
                optionValue = new OptionValueEntity
                {
                    OptionId = optionItem.OptionId,
                    Value = optionItem.Value
                };

                _context.OptionValueEntities.Add(optionValue);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var variantOption = new VariantOptionEntity
            {
                VariantId = variant.Id,
                OptionValueId = optionValue.Id
            };

            _context.VariantOptionEntities.Add(variantOption);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureVariantCombinationIsUniqueAsync(
        int productId,
        int? ignoredVariantId,
        List<VariantOptionInput> requestedOptions,
        CancellationToken cancellationToken)
    {
        var requestedKey = BuildCombinationKey(requestedOptions);

        var existingOptions = await _context.VariantOptionEntities
            .Where(x =>
                !x.IsDeleted &&
                !x.OptionValue.IsDeleted &&
                !x.Variant.IsDeleted &&
                x.Variant.ProductId == productId &&
                (!ignoredVariantId.HasValue || x.VariantId != ignoredVariantId.Value))
            .Select(x => new
            {
                x.VariantId,
                x.OptionValue.OptionId,
                x.OptionValue.Value
            })
            .ToListAsync(cancellationToken);

        var duplicateExists = existingOptions
            .GroupBy(x => x.VariantId)
            .Any(group =>
            {
                var existingKey = BuildCombinationKey(
                    group.Select(x => new VariantOptionInput(x.OptionId, x.Value)).ToList());

                return existingKey == requestedKey;
            });

        if (duplicateExists)
        {
            throw new Lilys_CMConflictException("A variant with the same option combination already exists for this product.");
        }
    }

    private static string BuildCombinationKey(List<VariantOptionInput> options)
    {
        return string.Join(
            "|",
            options
                .Select(x => $"{x.OptionId}:{x.Value.Trim().ToLowerInvariant()}")
                .OrderBy(x => x));
    }

    private sealed record VariantOptionInput(int OptionId, string Value);
}
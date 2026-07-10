namespace Ordering.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Aggregates;

internal sealed class CatalogItemProjectionConfiguration : IEntityTypeConfiguration<CatalogItemProjection>
{
    public void Configure(EntityTypeBuilder<CatalogItemProjection> builder)
    {
        builder.ToTable("catalog_item_projections");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Sku).HasMaxLength(Order.CatalogSkuMaxLength).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(Order.CatalogItemNameMaxLength).IsRequired();
        builder.Property(item => item.Price).HasPrecision(Order.AmountPrecision, Order.AmountScale).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(Order.CurrencyLength).IsRequired();
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.AvailableRegionCodes)
            .HasMaxLength(CatalogProjectionRegionStorageMaxLength)
            .IsRequired();
        builder.HasIndex(item => new { item.ScopeId, item.CatalogItemId }).IsUnique();
    }

    private const int CatalogProjectionRegionStorageMaxLength =
        ((Catalog.Contracts.CatalogContractLimits.RegionCodeMaxLength + 1) *
         Catalog.Contracts.CatalogContractLimits.AvailableRegionMaxCount) - 1;
}

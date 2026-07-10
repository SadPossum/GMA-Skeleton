namespace Catalog.Persistence.Configurations;

using Catalog.Domain.Aggregates;
using Catalog.Domain.Entities;
using Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Sku)
            .HasConversion(sku => sku.Value, value => CatalogSku.Create(value).Value)
            .HasMaxLength(CatalogItem.SkuMaxLength)
            .IsRequired();
        builder.Property(item => item.Name)
            .HasConversion(name => name.Value, value => CatalogItemName.Create(value).Value)
            .HasMaxLength(CatalogItem.NameMaxLength)
            .IsRequired();
        builder.Property(item => item.Price)
            .HasConversion(price => price.Value, value => CatalogPrice.Create(value).Value)
            .HasPrecision(CatalogItem.PricePrecision, CatalogItem.PriceScale)
            .IsRequired();
        builder.Property(item => item.Currency)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Create(value).Value)
            .HasMaxLength(CatalogItem.CurrencyLength)
            .IsRequired();
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.OwnsMany(
            item => item.AvailableRegions,
            regions =>
            {
                regions.ToTable("item_available_regions");
                regions.WithOwner().HasForeignKey("CatalogItemId");
                regions.Property(region => region.Region)
                    .HasConversion(region => region.Value, value => CatalogRegionCode.Create(value).Value)
                    .HasColumnName("RegionCode")
                    .HasMaxLength(CatalogItem.RegionCodeMaxLength)
                    .IsRequired();
                regions.HasKey("CatalogItemId", nameof(CatalogItemAvailableRegion.Region));
                regions.HasIndex("CatalogItemId", nameof(CatalogItemAvailableRegion.Region)).IsUnique();
            });
        builder.Navigation(item => item.AvailableRegions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(item => new { item.ScopeId, item.Sku }).IsUnique();
    }
}

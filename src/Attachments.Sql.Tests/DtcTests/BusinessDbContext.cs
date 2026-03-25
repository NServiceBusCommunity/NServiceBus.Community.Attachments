class BusinessDbContext(DbContextOptions<BusinessDbContext> options) : DbContext(options)
{
    public DbSet<BusinessEntity> Entities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<BusinessEntity>().ToTable("BusinessEntities");
}

class BusinessEntity
{
    public Guid Id { get; set; }
    public string Value { get; set; } = "";
}

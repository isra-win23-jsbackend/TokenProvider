

using Microsoft.EntityFrameworkCore;
using TokenProviderInfrastructure.Data.Entitties;

namespace TokenProviderInfrastructure.Data.Contexts;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{


    public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }
}

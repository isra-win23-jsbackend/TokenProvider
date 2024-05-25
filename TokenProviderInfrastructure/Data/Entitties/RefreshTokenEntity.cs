

using System.ComponentModel.DataAnnotations;

namespace TokenProviderInfrastructure.Data.Entitties;

public class RefreshTokenEntity
{

    [Key]
    public string RefreshToken { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public DateTime ExpireDate { get; set; } = DateTime.Now.AddDays(7);

}

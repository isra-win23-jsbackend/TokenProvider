﻿

namespace TokenProviderInfrastructure.Models;

public class TokenRequest
{

    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
}

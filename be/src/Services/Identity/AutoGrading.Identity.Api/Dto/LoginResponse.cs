using AutoGrading.Identity.Api.Interfaces;

namespace AutoGrading.Identity.Api.Dto;

public sealed record LoginResponse(string Token, Guid UserId, string Email, string Role)
{
    public static LoginResponse FromData(AuthTokenResult data) => new(data.Token, data.UserId, data.Email, data.Role);
}

using AutoGrading.Identity.Api.Interfaces;

namespace AutoGrading.Identity.Api.Dto;

/// <summary>Deliberately distinct from <see cref="LoginResponse"/> — registering doesn't log the user
/// in, so there's no token to return. Property named <c>Id</c> (not <c>UserId</c>) to match the original
/// endpoint's anonymous-object response (<c>new { user.Id, user.Email, Role = ... }</c>) exactly — using
/// <c>UserId</c> here would silently rename the JSON property and change the API contract.</summary>
public sealed record RegisterResponse(Guid Id, string Email, string Role)
{
    public static RegisterResponse FromData(UserAuthResult data) => new(data.UserId, data.Email, data.Role);
}

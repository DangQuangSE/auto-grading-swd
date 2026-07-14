using System.Net.Http.Headers;
using AutoGrading.Common.Auth;

namespace AutoGrading.Grading.Api.Clients;

/// <summary>Attaches a short-lived internal service JWT to every outgoing call to another microservice.</summary>
public sealed class ServiceAuthHandler(JwtTokenGenerator tokenGenerator) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenGenerator.GenerateServiceToken("grading");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}

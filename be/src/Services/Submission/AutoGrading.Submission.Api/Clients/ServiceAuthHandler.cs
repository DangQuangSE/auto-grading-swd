using System.Net.Http.Headers;
using AutoGrading.Common.Auth;

namespace AutoGrading.SubmissionSvc.Api.Clients;

public sealed class ServiceAuthHandler(JwtTokenGenerator tokenGenerator) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenGenerator.GenerateServiceToken("submission"));
        return base.SendAsync(request, cancellationToken);
    }
}

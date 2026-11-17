using Atlas.Extensions;
using Atlas.Features.Https.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Features.Https;

public static class HttpsEndpoints
{
    public static IEndpointRouteBuilder MapHttpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // 证书与监听配置属于服务器级别，全部要求登录
        var group = endpoints.MapGroup("/api/https").WithTags("Https").RequireAuthorization();

        group.MapGet("/", GetStatus);
        group.MapPut("/", UpdateSettings);
        // 与图片上传同理：绑定表单的端点需关闭防伪校验（本项目未启用 UseAntiforgery）
        group.MapPost("/certificate", UploadCertificate).DisableAntiforgery();
        group.MapPost("/self-signed", CreateSelfSigned);

        return endpoints;
    }

    private static Ok<HttpsStatusResponse> GetStatus(
        HttpsConfigStore configStore,
        CertificateStore certificates,
        HttpsRuntimeState runtime)
    {
        return TypedResults.Ok(BuildStatus(configStore, certificates, runtime));
    }

    private static Results<Ok<HttpsStatusResponse>, ProblemHttpResult> UpdateSettings(
        UpdateHttpsRequest request,
        HttpsConfigStore configStore,
        CertificateStore certificates,
        HttpsRuntimeState runtime)
    {
        if (!HttpsConfigStore.IsValidPort(request.Port))
            return ProblemResults.InvalidRequest(
                $"端口需在 {HttpsConfigStore.PortMin}-{HttpsConfigStore.PortMax} 之间");

        configStore.Save(request.Enabled, request.Port);

        return TypedResults.Ok(BuildStatus(configStore, certificates, runtime));
    }

    /// <summary>
    ///     上传证书：单个 .pfx/.p12，或 PEM 证书 + 私钥两个文件；上传成功后立即生效
    /// </summary>
    private static Results<Ok<HttpsStatusResponse>, ProblemHttpResult> UploadCertificate(
        IFormFile? certificate,
        IFormFile? privateKey,
        [FromForm] string? password,
        HttpsConfigStore configStore,
        CertificateStore certificates,
        HttpsRuntimeState runtime)
    {
        if (certificate is null || certificate.Length == 0)
            return ProblemResults.InvalidRequest("请选择证书文件");

        try
        {
            if (IsPkcs12(certificate.FileName))
            {
                using var stream = certificate.OpenReadStream();
                certificates.ImportPfx(stream, password);
            }
            else
            {
                if (privateKey is null || privateKey.Length == 0)
                    return ProblemResults.InvalidRequest("PEM 证书需要同时提供私钥文件");

                using var certificateStream = certificate.OpenReadStream();
                using var keyStream = privateKey.OpenReadStream();
                certificates.ImportPem(certificateStream, keyStream);
            }
        }
        catch (InvalidDataException ex)
        {
            return ProblemResults.InvalidRequest(ex.Message);
        }

        return TypedResults.Ok(BuildStatus(configStore, certificates, runtime));
    }

    private static Ok<HttpsStatusResponse> CreateSelfSigned(
        HttpsConfigStore configStore,
        CertificateStore certificates,
        HttpsRuntimeState runtime)
    {
        certificates.CreateSelfSigned();
        return TypedResults.Ok(BuildStatus(configStore, certificates, runtime));
    }

    private static HttpsStatusResponse BuildStatus(
        HttpsConfigStore configStore,
        CertificateStore certificates,
        HttpsRuntimeState runtime)
    {
        var config = configStore.Load();

        return new HttpsStatusResponse(
            config.Enabled,
            config.Port,
            runtime.StartedEnabled,
            runtime.IsRestartRequired(config),
            certificates.GetInfo(),
            certificates.Error);
    }

    private static bool IsPkcs12(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() is ".pfx" or ".p12";
    }
}
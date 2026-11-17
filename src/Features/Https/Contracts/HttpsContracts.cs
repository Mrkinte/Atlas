namespace Atlas.Features.Https.Contracts;

/// <summary>证书的公开信息，不含私钥与口令</summary>
public record CertificateInfo(
    string Subject,
    string Issuer,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    string Thumbprint,
    bool IsSelfSigned);

/// <summary>HTTPS 运行状态：当前配置、启动时是否生效、是否需要重启，以及证书信息</summary>
public record HttpsStatusResponse(
    bool Enabled,
    int Port,
    bool Active,
    bool RestartRequired,
    CertificateInfo? Certificate,
    string? Error);

/// <summary>监听地址在启动时绑定，保存后需要重启服务</summary>
public record UpdateHttpsRequest(bool Enabled, int Port);
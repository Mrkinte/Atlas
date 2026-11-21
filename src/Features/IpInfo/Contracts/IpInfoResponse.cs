namespace Atlas.Features.IpInfo.Contracts;

public record IpInfoResponse(
    PublicIpEntry[] PublicIps,
    string? LocalIp,
    // LatencyNodes 为 0 时该值无意义
    int LatencyMs,
    int LatencyNodes,
    // 匿名访问时地址打码、运营商与内网地址不返回
    bool Masked);

public record PublicIpEntry(
    string Address,
    // 4 或 6
    int Version,
    string? Country,
    string? Region,
    string? City,
    string? Isp,
    string? Asn);

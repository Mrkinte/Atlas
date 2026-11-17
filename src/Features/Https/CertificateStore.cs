using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Atlas.Features.Https.Contracts;

namespace Atlas.Features.Https;

/// <summary>
///     服务器证书的唯一来源。启动时从 certs/server.pfx 载入，Kestrel 每次握手读取
///     <see cref="CurrentContext" />（含中间证书），因此替换证书无需重启
/// </summary>
public sealed class CertificateStore
{
    /// <summary>文件名固定，不接受来自请求的路径，避免任意文件读取</summary>
    private const string FileName = "server.pfx";

    /// <summary>1 MB，正常导出的证书远小于该值</summary>
    private const int MaxFileSize = 1024 * 1024;

    private const int SelfSignedDays = 365;

    /// <summary>
    ///     不能用 EphemeralKeySet：Windows 的 SChannel 不支持临时密钥充当服务端证书，
    ///     握手会直接失败。Exportable 是为了能把叶子与中间证书重新导出成 PFX 落盘
    /// </summary>
    private const X509KeyStorageFlags ImportFlags = X509KeyStorageFlags.Exportable;

    private readonly string _filePath;

    /// <summary>串行化安装，避免并发上传共用临时文件写出半截证书</summary>
    private readonly object _installLock = new();

    private volatile SslStreamCertificateContext? _context;

    private volatile X509Certificate2? _current;

    private volatile string? _error;

    private CertificateStore(string directory)
    {
        _filePath = Path.Combine(directory, FileName);
    }

    public X509Certificate2? Current => _current;

    /// <summary>握手用的证书上下文（叶子 + 中间证书），每次连接重新读取</summary>
    public SslStreamCertificateContext? CurrentContext => _context;

    public bool HasCertificate => _current is not null;

    /// <summary>证书不可用的原因，供设置页提示</summary>
    public string? Error => _error;

    public static CertificateStore Create(string certsDirectory)
    {
        var store = new CertificateStore(certsDirectory);
        store.LoadFromDisk();
        return store;
    }

    public CertificateInfo? GetInfo()
    {
        return _current is null ? null : ToInfo(_current);
    }

    /// <summary>导入 PFX/P12 证书（可带口令），校验通过后落盘并立即生效</summary>
    public CertificateInfo ImportPfx(Stream stream, string? password)
    {
        var bytes = ReadAll(stream);

        X509Certificate2Collection collection;
        try
        {
            // 集合重载：PFX 里通常同时有叶子与中间证书，不能只取第一张
            collection = X509CertificateLoader.LoadPkcs12Collection(bytes, password, ImportFlags);
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("证书解析失败，请确认文件格式与口令是否正确");
        }

        return Install(collection);
    }

    /// <summary>导入 PEM 证书与私钥，校验通过后落盘并立即生效</summary>
    public CertificateInfo ImportPem(Stream certificateStream, Stream privateKeyStream)
    {
        var certificatePem = Encoding.UTF8.GetString(ReadAll(certificateStream));
        var privateKeyPem = Encoding.UTF8.GetString(ReadAll(privateKeyStream));

        X509Certificate2 leaf;
        var parsed = new X509Certificate2Collection();
        try
        {
            // 整份 PEM 可能包含证书链：先全部读出，再挑与私钥匹配的那张当叶子
            parsed.ImportFromPem(certificatePem);
            leaf = X509Certificate2.CreateFromPem(certificatePem, privateKeyPem);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidDataException("PEM 解析失败，请确认证书与私钥文件是否匹配");
        }

        // CreateFromPem 返回的实例与集合里的不是同一个，统一按指纹去重后一起落盘
        var collection = new X509Certificate2Collection { leaf };
        foreach (var certificate in parsed)
            if (!IsSameCertificate(certificate, leaf))
                collection.Add(certificate);

        return Install(collection);
    }

    /// <summary>生成并安装自签名证书（有效期一年，包含本机名与 localhost）</summary>
    public CertificateInfo CreateSelfSigned()
    {
        using var certificate = CreateSelfSignedCertificate();
        return Install(new X509Certificate2Collection { certificate });
    }

    /// <summary>
    ///     生成一张仅存在于内存的自签名证书（不落盘），用于证书文件不可用时仍能启动 HTTPS
    /// </summary>
    public CertificateInfo UseTemporarySelfSigned()
    {
        using var generated = CreateSelfSignedCertificate();

        // 必须经 PFX 重新导入一次：Windows 的 SChannel 不接受纯内存（临时）私钥做服务端证书
        var pfx = generated.Export(X509ContentType.Pfx)
                  ?? throw new InvalidDataException("证书导出失败");

        var installed = X509CertificateLoader.LoadPkcs12(pfx, null, ImportFlags);
        Apply(installed, new X509Certificate2Collection());

        return ToInfo(installed);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={Environment.MachineName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication") }, false));

        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName(Environment.MachineName);
        alternativeNames.AddDnsName("localhost");
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        alternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(alternativeNames.Build());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);

        return request.CreateSelfSigned(notBefore, notBefore.AddDays(SelfSignedDays));
    }

    /// <summary>
    ///     启动时载入；文件损坏或没有私钥只记录原因，不阻止启动
    ///     已过期/尚未生效的证书仍会载入，原因写入 <see cref="Error" /> 供设置页展示
    /// </summary>
    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            var collection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                _filePath, null, ImportFlags);

            var leaf = FindLeaf(collection);
            Apply(leaf, ChainWithout(collection, leaf));

            // 过期/未生效不阻止使用，但要让设置页能看出来
            _error = ValidateForServing(leaf);
        }
        catch (InvalidDataException ex)
        {
            _error = ex.Message;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            _error = $"证书文件无法读取：{ex.Message}";
        }
    }

    private CertificateInfo Install(X509Certificate2Collection collection)
    {
        lock (_installLock)
        {
            var leaf = FindLeaf(collection);
            EnsureAcceptable(leaf);

            var intermediates = ChainWithout(collection, leaf);

            // 落盘时要把中间证书一起带上，否则重启后证书链又会丢失
            var exportable = new X509Certificate2Collection { leaf };
            foreach (var certificate in intermediates) exportable.Add(certificate);

            var pfx = exportable.Export(X509ContentType.Pfx)
                      ?? throw new InvalidDataException("证书导出失败，请确认私钥允许导出");

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // 每次用独立临时文件，避免并发上传互相覆盖；最后一步才原子替换正式文件
            var tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllBytes(tempPath, pfx);
                RestrictPermissions(tempPath);
                File.Move(tempPath, _filePath, true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            // 从导出的字节重新加载，确保内存里的实例与文件内容一致
            var installed = X509CertificateLoader.LoadPkcs12Collection(pfx, null, ImportFlags);
            var installedLeaf = FindLeaf(installed);

            // 先建上下文再切换，握手中途不会读到"有叶子但没有上下文"的中间态
            Apply(installedLeaf, ChainWithout(installed, installedLeaf));
            _error = null;

            return ToInfo(installedLeaf);
        }
    }

    /// <summary>构建握手上下文并切换当前证书；旧实例不 Dispose，可能仍有握手在使用</summary>
    private void Apply(X509Certificate2 leaf, X509Certificate2Collection chain)
    {
        // offline: true 表示只查本机存储、不联网补链，避免启动/握手被外部请求拖住
        var context = SslStreamCertificateContext.Create(leaf, chain, true);

        _context = context;
        _current = leaf;
    }

    /// <summary>PFX/PEM 可能同时含叶子与中间证书，取带私钥的那张当服务器证书</summary>
    private static X509Certificate2 FindLeaf(X509Certificate2Collection collection)
    {
        foreach (var certificate in collection)
            if (certificate.HasPrivateKey)
                return certificate;

        throw new InvalidDataException("证书不包含私钥，请导出包含私钥的证书文件");
    }

    private static X509Certificate2Collection ChainWithout(
        X509Certificate2Collection collection, X509Certificate2 leaf)
    {
        var chain = new X509Certificate2Collection();

        foreach (var certificate in collection)
            if (!IsSameCertificate(certificate, leaf))
                chain.Add(certificate);

        return chain;
    }

    private static bool IsSameCertificate(X509Certificate2 left, X509Certificate2 right)
    {
        return string.Equals(left.Thumbprint, right.Thumbprint, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>上传时必须完全可用，有问题直接拒绝</summary>
    private static void EnsureAcceptable(X509Certificate2 leaf)
    {
        if (!leaf.HasPrivateKey)
            throw new InvalidDataException("证书不包含私钥，请导出包含私钥的证书文件");

        var now = DateTime.Now;

        if (leaf.NotAfter <= now)
            throw new InvalidDataException("证书已过期，请更换有效的证书");

        if (leaf.NotBefore > now)
            throw new InvalidDataException("证书尚未生效，请检查系统时间或更换证书");
    }

    /// <summary>启动加载时只提示，不阻止使用</summary>
    private static string? ValidateForServing(X509Certificate2 leaf)
    {
        var now = DateTime.Now;

        if (leaf.NotAfter <= now)
            return $"证书已于 {leaf.NotAfter:yyyy-MM-dd} 过期，请更换";

        if (leaf.NotBefore > now)
            return $"证书要到 {leaf.NotBefore:yyyy-MM-dd} 才生效，请检查系统时间";

        return null;
    }

    private static void RestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxFileSize)
                throw new InvalidDataException("证书文件过大");

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static CertificateInfo ToInfo(X509Certificate2 certificate)
    {
        return new CertificateInfo(
            certificate.Subject,
            certificate.Issuer,
            new DateTimeOffset(certificate.NotBefore),
            new DateTimeOffset(certificate.NotAfter),
            certificate.Thumbprint,
            string.Equals(certificate.Subject, certificate.Issuer, StringComparison.Ordinal));
    }
}
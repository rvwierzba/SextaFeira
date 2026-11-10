using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SextaFeira.Infrastructure.Storage;

namespace SextaFeira.WebApi.Hosting;

/// <summary>
/// Certificado HTTPS autoassinado para a rede local. Navegadores só liberam o microfone em HTTPS
/// (exceto em localhost), então é isso que permite usar celular/tablet/outro PC como microfone.
/// </summary>
public static class LanCertificate
{
    private static string PfxPath => Path.Combine(DataPaths.Certs, "sextafeira-lan.pfx");
    public static string CrtPath => Path.Combine(DataPaths.Certs, "sextafeira-lan.crt");

    /// <summary>IPs da rede local real (interfaces com gateway; ignora adaptadores virtuais do Hyper-V/WSL/VirtualBox).</summary>
    public static IReadOnlyList<IPAddress> LanAddresses()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .ToList();
        var withGateway = interfaces.Where(n => n.GetIPProperties().GatewayAddresses
            .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))).ToList();

        return (withGateway.Count > 0 ? withGateway : interfaces)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254."))
            .Distinct()
            .ToList();
    }

    public static X509Certificate2 LoadOrCreate()
    {
        var ips = LanAddresses();
        if (File.Exists(PfxPath))
        {
            try
            {
                var existing = X509CertificateLoader.LoadPkcs12FromFile(PfxPath, null);
                var san = existing.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
                var covered = san?.EnumerateIPAddresses().ToHashSet() ?? [];
                if (existing.NotAfter > DateTime.Now.AddDays(30) && ips.All(covered.Contains))
                    return existing;
            }
            catch { /* recria abaixo */ }
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Sexta-Feira (rede local)", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName);
        sanBuilder.AddDnsName(Environment.MachineName.ToLowerInvariant() + ".local");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        foreach (var ip in ips) sanBuilder.AddIpAddress(ip);
        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        using var cert = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(2));
        File.WriteAllBytes(PfxPath, cert.Export(X509ContentType.Pfx));
        File.WriteAllText(CrtPath, cert.ExportCertificatePem());
        return X509CertificateLoader.LoadPkcs12FromFile(PfxPath, null);
    }
}
